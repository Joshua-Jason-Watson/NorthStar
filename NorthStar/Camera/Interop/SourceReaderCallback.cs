
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar.Camera.Interop;

[GeneratedComInterface]
[Guid("DEEC8D99-FA1D-4D82-84C2-2C8969944867")]
internal partial interface IMFSourceReaderCallback
{
    [PreserveSig]
    int OnReadSample(
        int hrStatus,
        uint streamIndex,
        uint streamFlags,
        long timestamp,
        nint sample);

    [PreserveSig]
    int OnFlush(uint streamIndex);

    [PreserveSig]
    int OnEvent(uint streamIndex, nint mediaEvent);
}

internal readonly record struct SourceReaderSampleResult(
    int Status,
    uint StreamIndex,
    uint StreamFlags,
    long Timestamp,
    nint SamplePointer);

[GeneratedComClass]
internal sealed partial class SourceReaderCallback :
    IMFSourceReaderCallback,
    IDisposable
{
    // ============================================================
    // Configuration
    // ============================================================

    public const int MaximumQueueSize = 3;


    // ============================================================
    // Synchronization
    // ============================================================

    private readonly object sync = new();

    private TaskCompletionSource<bool> changed =
        CreateSignal();

    private TaskCompletionSource? pendingFlush;


    // ============================================================
    // Captured samples
    // ============================================================

    private readonly Queue<SourceReaderSampleResult> capturedSamples =
        new();

    private long droppedFrameCount;


    // ============================================================
    // Continuous capture
    // ============================================================

    private Func<int>? requestNextSample;

    private bool continuousCaptureStarted;

    private bool stopRequested;

    private bool endOfStream;

    private bool flushed;


    // ============================================================
    // Terminal state
    // ============================================================

    private Exception? failure;

    private SourceReaderSampleResult? terminalResult;

    private bool disposed;


    // ============================================================
    // Statistics
    // ============================================================

    public long DroppedFrameCount
    {
        get
        {
            lock (sync)
            {
                return droppedFrameCount;
            }
        }
    }

    public int PendingFrameCount
    {
        get
        {
            lock (sync)
            {
                return capturedSamples.Count;
            }
        }
    }


    // ============================================================
    // Continuous capture startup
    // ============================================================

    public void StartContinuousCapture(
        Func<int> requestNextSample,
        uint streamIndex)
    {
        ArgumentNullException.ThrowIfNull(requestNextSample);

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            if (continuousCaptureStarted)
            {
                throw new InvalidOperationException(
                    "Continuous capture has already been started.");
            }

            if (stopRequested || flushed)
            {
                throw new InvalidOperationException(
                    "Continuous capture cannot start after shutdown has begun.");
            }

            this.requestNextSample =
                requestNextSample;

            continuousCaptureStarted = true;

            RequestNextSampleLocked();

            if (failure != null)
            {
                throw failure;
            }
        }
    }


    // ============================================================
    // Media Foundation sample callback
    // ============================================================

    public int OnReadSample(
        int hrStatus,
        uint streamIndex,
        uint streamFlags,
        long timestamp,
        nint sample)
    {
        try
        {
            HandleSample(
                hrStatus,
                streamIndex,
                streamFlags,
                timestamp,
                sample);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }

        return 0;
    }


    // ============================================================
    // Continuous sample handling
    // ============================================================

    private void HandleSample(
        int hrStatus,
        uint actualStreamIndex,
        uint streamFlags,
        long timestamp,
        nint sample)
    {
        lock (sync)
        {
            if (disposed || stopRequested || flushed)
            {
                return;
            }

            if (hrStatus < 0)
            {
                FailLocked(
                    CreateMediaFoundationException(
                        hrStatus,
                        "Media Foundation failed while acquiring a camera sample."));

                return;
            }


            // Retain the sample's COM reference before the callback
            // returns to Media Foundation.
            if (sample != 0)
            {
                AddRefSample(sample);

                try
                {
                    // Match OpenCV's Media Foundation backend:
                    // discard the queued samples when capacity is reached
                    // and keep the newest incoming sample.
                    if (capturedSamples.Count >= MaximumQueueSize)
                    {
                        droppedFrameCount += capturedSamples.Count;

                        ClearQueuedSamplesLocked();
                    }

                    capturedSamples.Enqueue(
                        new SourceReaderSampleResult(
                            hrStatus,
                            actualStreamIndex,
                            streamFlags,
                            timestamp,
                            sample));
                }
                catch
                {
                    ReleaseSample(sample);
                    throw;
                }
            }


            // End-of-stream means no further samples should be requested.
            if ((streamFlags & 0x00000001) != 0)
            {
                endOfStream = true;

                terminalResult =
                    new SourceReaderSampleResult(
                        hrStatus,
                        actualStreamIndex,
                        streamFlags,
                        timestamp,
                        0);

                requestNextSample = null;
            }
            else
            {
                // Immediately schedule the next native sample request.
                // The consumer does not need to request it.
                RequestNextSampleLocked();
            }


            // Wake consumers when a frame or terminal condition arrives.
            if (sample != 0 ||
                endOfStream ||
                failure != null)
            {
                SignalWaitersLocked();
            }
        }
    }


    // ============================================================
    // Continuous read scheduling
    // ============================================================

    // Called while holding the synchronization lock.
    private void RequestNextSampleLocked()
    {
        if (disposed ||
            stopRequested ||
            flushed ||
            endOfStream ||
            failure != null)
        {
            return;
        }

        if (requestNextSample == null)
        {
            FailLocked(
                new InvalidOperationException(
                    "The continuous capture request has not been configured."));

            return;
        }

        try
        {
            int hresult =
                requestNextSample();

            if (hresult < 0)
            {
                FailLocked(
                    CreateMediaFoundationException(
                        hresult,
                        "Media Foundation could not schedule the next camera sample."));
            }
        }
        catch (Exception exception)
        {
            FailLocked(exception);
        }
    }


    // ============================================================
    // Sample consumption
    // ============================================================

    public async Task<SourceReaderSampleResult> TakeSampleAsync(
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task waitTask;

            lock (sync)
            {
                ObjectDisposedException.ThrowIf(
                    disposed,
                    this);

                if (!continuousCaptureStarted)
                {
                    throw new InvalidOperationException(
                        "Continuous capture has not been started.");
                }

                // Always deliver already-captured frames before
                // reporting an end-of-stream condition.
                if (capturedSamples.Count > 0)
                {
                    return capturedSamples.Dequeue();
                }

                if (failure != null)
                {
                    throw failure;
                }

                if (endOfStream && terminalResult.HasValue)
                {
                    return terminalResult.Value;
                }

                if (flushed || stopRequested)
                {
                    throw new OperationCanceledException(
                        "The camera capture stream has been flushed.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                waitTask = changed.Task;
            }

            await waitTask
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }


    // ============================================================
    // Flush lifecycle
    // ============================================================

    // Prepares callback state before RawSourceReader invokes
    // IMFSourceReader.Flush.
    public Task PrepareFlush()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            if (flushed)
            {
                return Task.CompletedTask;
            }

            if (pendingFlush != null)
            {
                return pendingFlush.Task;
            }

            stopRequested = true;

            requestNextSample = null;

            ClearQueuedSamplesLocked();

            pendingFlush =
                new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            SignalWaitersLocked();

            return pendingFlush.Task;
        }
    }


    // Media Foundation calls this when its asynchronous flush completes.
    public int OnFlush(uint streamIndex)
    {
        TaskCompletionSource? completion;

        lock (sync)
        {
            if (disposed)
            {
                return 0;
            }

            stopRequested = true;

            flushed = true;

            requestNextSample = null;

            ClearQueuedSamplesLocked();

            completion = pendingFlush;

            pendingFlush = null;

            SignalWaitersLocked();
        }

        completion?.TrySetResult();

        return 0;
    }


    public void FailFlush(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        TaskCompletionSource? completion;

        lock (sync)
        {
            stopRequested = true;

            requestNextSample = null;

            failure = exception;

            ClearQueuedSamplesLocked();

            completion = pendingFlush;

            pendingFlush = null;

            SignalWaitersLocked();
        }

        completion?.TrySetException(exception);
    }


    // ============================================================
    // Event callback
    // ============================================================

    public int OnEvent(
        uint streamIndex,
        nint mediaEvent)
    {
        return 0;
    }


    // ============================================================
    // Failure handling
    // ============================================================

    private void Fail(Exception exception)
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            FailLocked(exception);
        }
    }


    // Called while holding the synchronization lock.
    private void FailLocked(Exception exception)
    {
        failure = exception;

        stopRequested = true;

        requestNextSample = null;

        SignalWaitersLocked();
    }


    private static Exception CreateMediaFoundationException(
        int hresult,
        string message)
    {
        return
            Marshal.GetExceptionForHR(hresult)
            ?? new COMException(
                message,
                hresult);
    }


    // ============================================================
    // Queue management
    // ============================================================

    private void ClearQueuedSamplesLocked()
    {
        while (capturedSamples.Count > 0)
        {
            SourceReaderSampleResult sample =
                capturedSamples.Dequeue();

            ReleaseSample(sample.SamplePointer);
        }
    }


    // ============================================================
    // Waiter notification
    // ============================================================

    private static TaskCompletionSource<bool> CreateSignal()
    {
        return new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }


    // Called while holding the synchronization lock.
    private void SignalWaitersLocked()
    {
        TaskCompletionSource<bool> previous =
            changed;

        changed = CreateSignal();

        previous.TrySetResult(true);
    }


    // ============================================================
    // COM sample reference management
    // ============================================================

    private static unsafe void AddRefSample(nint sample)
    {
        if (sample == 0)
        {
            return;
        }

        void* pointer = (void*)sample;

        void** vtable = *(void***)pointer;

        ((delegate* unmanaged[Stdcall]<void*, uint>)
            vtable[1])(pointer);
    }


    private static unsafe void ReleaseSample(nint sample)
    {
        if (sample == 0)
        {
            return;
        }

        void* pointer = (void*)sample;

        void** vtable = *(void***)pointer;

        ((delegate* unmanaged[Stdcall]<void*, uint>)
            vtable[2])(pointer);
    }


    // ============================================================
    // Disposal
    // ============================================================

    public void Dispose()
    {
        TaskCompletionSource? flushCompletion;

        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            stopRequested = true;

            requestNextSample = null;

            ClearQueuedSamplesLocked();

            flushCompletion = pendingFlush;

            pendingFlush = null;

            SignalWaitersLocked();
        }

        flushCompletion?.TrySetException(
            new ObjectDisposedException(
                nameof(SourceReaderCallback)));

        GC.SuppressFinalize(this);
    }
}
