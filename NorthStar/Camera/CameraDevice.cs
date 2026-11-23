
using NorthStar.Camera.Interop;
using NorthStar.Frames;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace NorthStar.Camera
{
    // Represents an opened camera and provides access to its
    // Media Foundation capabilities and continuously acquired frames.
    //
    // Camera acquisition operates independently of frame consumption.
    // Samples are received through SourceReaderCallback and stored
    // in its bounded queue.
    public sealed class CameraDevice : ICamera, IDisposable
    {
        // ============================================================
        // Camera information
        // ============================================================

        public string Name { get; }

        public int Width =>
            currentCapability?.Width ?? 0;

        public int Height =>
            currentCapability?.Height ?? 0;

        public double FPS =>
            currentCapability?.FPS ?? 0;

        public long DroppedFrameCount =>
            sourceReaderCallback?.DroppedFrameCount ?? 0;

        public int PendingFrameCount =>
            sourceReaderCallback?.PendingFrameCount ?? 0;


        // ============================================================
        // Native resources
        // ============================================================

        private nint activatePointer;

        private nint mediaSourcePointer;

        private RawSourceReader? sourceReader;

        private SourceReaderCallback? sourceReaderCallback;

        private CameraCapability? currentCapability;

        private RawMediaType? currentMediaType;

        private int currentStride;


        // ============================================================
        // Capture state
        // ============================================================

        // NorthStar currently has one consumer per camera.
        private readonly SemaphoreSlim frameReadGate =
            new(1, 1);

        private readonly object captureLock =
            new();

        private bool captureStarted;

        private Task? flushTask;


        // ============================================================
        // Frame timing
        // ============================================================

        private long? previousFrameTimestamp;


        // ============================================================
        // Disposal
        // ============================================================

        private int disposed;


        // ============================================================
        // Media Foundation constants
        // ============================================================

        private const uint FirstVideoStream =
            0xFFFFFFFC;

        private const uint EndOfStreamFlag =
            0x00000001;

        private const int MfENoMoreTypes =
            unchecked((int)0xC00D36B9);


        // ============================================================
        // Constructor
        // ============================================================

        internal unsafe CameraDevice(
            string name,
            IMFActivate_unmanaged* activate)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Camera name cannot be empty.",
                    nameof(name));
            }

            if (activate == null)
            {
                throw new ArgumentNullException(
                    nameof(activate));
            }

            Name = name;

            activatePointer = (nint)activate;

            SourceReaderCallback callback =
                new();

            void* callbackPointer =
                ComInterfaceMarshaller<IMFSourceReaderCallback>
                    .ConvertToUnmanaged(callback);

            void* sourceReaderPointer = null;

            bool callbackTransferred = false;

            try
            {
                Guid mediaSourceGuid =
                    new("279A808D-AEC7-40C8-9C6B-A6B492C78A66");

                // Activate the camera's native IMFMediaSource.
                void* activatedMediaSource =
                    activate->ActivateObject(
                        mediaSourceGuid);

                if (activatedMediaSource == null)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned a null camera media source.");
                }

                mediaSourcePointer =
                    (nint)activatedMediaSource;


                // Create Source Reader attributes.
                PInvoke.MFCreateAttributes(
                    out var readerAttributes,
                    2);

                try
                {
                    // Enable low-latency processing.
                    Guid lowLatencyKey =
                        PInvoke.MF_LOW_LATENCY;

                    readerAttributes.SetUINT32(
                        &lowLatencyKey,
                        1);


                    // Register the asynchronous Source Reader callback.
                    Guid callbackKey =
                        PInvoke.MF_SOURCE_READER_ASYNC_CALLBACK;

                    SetAttributeUnknown(
                        readerAttributes,
                        callbackKey,
                        callbackPointer);


                    nint readerAttributesPointer =
                        Marshal.GetComInterfaceForObject(
                            readerAttributes,
                            typeof(IMFAttributes));

                    try
                    {
                        HRESULT hr =
                            MediaFoundationNative
                                .MFCreateSourceReaderFromMediaSource(
                                    (void*)mediaSourcePointer,
                                    (void*)readerAttributesPointer,
                                    &sourceReaderPointer);

                        hr.ThrowOnFailure();
                    }
                    finally
                    {
                        Marshal.Release(
                            readerAttributesPointer);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(
                        readerAttributes);
                }


                if (sourceReaderPointer == null)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned a null source reader.");
                }


                // Transfer ownership of the Source Reader pointer.
                void* createdSourceReaderPointer =
                    sourceReaderPointer;

                sourceReaderPointer = null;

                sourceReader =
                    new RawSourceReader(
                        createdSourceReaderPointer);


                // Keep the callback alive for the reader's lifetime.
                sourceReaderCallback =
                    callback;

                callbackTransferred = true;
            }
            catch
            {
                ReleaseComPointer(
                    (nint)sourceReaderPointer);

                Dispose();

                throw;
            }
            finally
            {
                ComInterfaceMarshaller<IMFSourceReaderCallback>.Free(
                    callbackPointer);

                if (!callbackTransferred)
                {
                    callback.Dispose();
                }
            }
        }


        // ============================================================
        // Media Foundation attribute helpers
        // ============================================================

        private static unsafe void SetAttributeUnknown(
            IMFAttributes attributes,
            Guid key,
            void* unknown)
        {
            nint attributesPointer =
                Marshal.GetComInterfaceForObject(
                    attributes,
                    typeof(IMFAttributes));

            try
            {
                void** vtable =
                    *(void***)attributesPointer;

                HRESULT hr =
                    ((delegate* unmanaged[Stdcall]<
                        void*,
                        Guid*,
                        void*,
                        HRESULT>)
                    vtable[27])(
                        (void*)attributesPointer,
                        &key,
                        unknown);

                hr.ThrowOnFailure();
            }
            finally
            {
                Marshal.Release(
                    attributesPointer);
            }
        }


        // ============================================================
        // Camera capabilities
        // ============================================================

        public unsafe IReadOnlyList<CameraCapability> GetCapabilities()
        {
            ThrowIfDisposed();

            List<CameraCapability> capabilities = new();

            uint mediaTypeIndex = 0;

            while (true)
            {
                void* mediaTypePointer = null;

                HRESULT hr =
                    sourceReader!.GetNativeMediaType(
                        FirstVideoStream,
                        mediaTypeIndex,
                        out mediaTypePointer);

                if (hr.Value == MfENoMoreTypes)
                {
                    break;
                }

                hr.ThrowOnFailure();

                using RawMediaType mediaType =
                    new(mediaTypePointer);

                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_SIZE,
                    out ulong frameSize);

                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_RATE,
                    out ulong frameRate);

                mediaType.GetGUID(
                    PInvoke.MF_MT_SUBTYPE,
                    out Guid subtype);

                int width =
                    (int)(frameSize >> 32);

                int height =
                    (int)(frameSize & 0xFFFFFFFF);

                uint frameRateNumerator =
                    (uint)(frameRate >> 32);

                uint frameRateDenominator =
                    (uint)(frameRate & 0xFFFFFFFF);

                capabilities.Add(
                    new CameraCapability(
                        mediaTypeIndex,
                        width,
                        height,
                        frameRateNumerator,
                        frameRateDenominator,
                        subtype));

                mediaTypeIndex++;
            }

            return capabilities.AsReadOnly();
        }


        public CameraCapability SelectCapability(
            int width,
            int height,
            double fps)
        {
            ThrowIfDisposed();

            foreach (
                CameraCapability capability
                in GetCapabilities())
            {
                if (capability.Width == width &&
                    capability.Height == height &&
                    Math.Abs(capability.FPS - fps) < 0.01)
                {
                    return capability;
                }
            }

            throw new InvalidOperationException(
                $"Camera does not support " +
                $"{width}x{height} @ {fps:F2} FPS.");
        }


        // ============================================================
        // Camera configuration
        // ============================================================

        public unsafe void Configure(
            CameraCapability capability)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(capability);

            if (currentCapability != null)
            {
                throw new InvalidOperationException(
                    "The camera has already been configured. " +
                    "Create a new CameraDevice to select a different capability.");
            }

            void* mediaTypePointer = null;

            HRESULT hr =
                sourceReader!.GetNativeMediaType(
                    FirstVideoStream,
                    capability.MediaTypeIndex,
                    out mediaTypePointer);

            hr.ThrowOnFailure();

            if (mediaTypePointer == null)
            {
                throw new InvalidOperationException(
                    "Media Foundation returned a null media type.");
            }

            RawMediaType mediaType =
                new(mediaTypePointer);

            try
            {
                hr =
                    sourceReader.SetNativeMediaType(
                        FirstVideoStream,
                        mediaType.Pointer,
                        out _);

                hr.ThrowOnFailure();

                int stride =
                    DetermineStride(
                        mediaType,
                        capability);

                currentMediaType = mediaType;

                currentCapability = capability;

                currentStride = stride;

                previousFrameTimestamp = null;

                // Transfer ownership to CameraDevice.
                mediaType = null!;
            }
            finally
            {
                mediaType?.Dispose();
            }
        }


        // ============================================================
        // Stride calculation
        // ============================================================

        private int DetermineStride(
            RawMediaType mediaType,
            CameraCapability capability)
        {
            Guid strideKey =
                PInvoke.MF_MT_DEFAULT_STRIDE;

            HRESULT hr =
                mediaType.GetUINT32(
                    strideKey,
                    out uint unsignedStride);

            if (hr.Succeeded)
            {
                int stride =
                    unchecked((int)unsignedStride);

                if (stride == int.MinValue)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned an invalid stride.");
                }

                stride = Math.Abs(stride);

                if (stride > 0)
                {
                    return stride;
                }
            }

            byte[] subtypeBytes =
                capability.Subtype.ToByteArray();

            uint format =
                BitConverter.ToUInt32(
                    subtypeBytes,
                    0);

            hr =
                PInvoke.MFGetStrideForBitmapInfoHeader(
                    format,
                    (uint)capability.Width,
                    out int calculatedStride);

            hr.ThrowOnFailure();

            if (calculatedStride == int.MinValue)
            {
                throw new InvalidOperationException(
                    "Media Foundation returned an invalid calculated stride.");
            }

            calculatedStride =
                Math.Abs(calculatedStride);

            if (calculatedStride <= 0)
            {
                throw new InvalidOperationException(
                    $"Media Foundation could not determine a valid " +
                    $"stride for {capability.Width}x{capability.Height} " +
                    $"format {capability.Subtype}.");
            }

            return calculatedStride;
        }


        // ============================================================
        // Continuous capture startup
        // ============================================================

        private void EnsureContinuousCaptureStarted()
        {
            lock (captureLock)
            {
                ThrowIfDisposed();

                if (captureStarted)
                {
                    return;
                }

                if (sourceReader == null ||
                    sourceReaderCallback == null)
                {
                    throw new InvalidOperationException(
                        "The camera's Source Reader has not been initialized.");
                }

                // Mark the reader as started before issuing the first
                // request so disposal can flush it if startup fails.
                captureStarted = true;

                sourceReader.StartContinuousCapture(
                    FirstVideoStream,
                    sourceReaderCallback);
            }
        }


        // ============================================================
        // Camera flush
        // ============================================================

        // Flush is a stream-lifecycle operation.
        //
        // Cancelling an individual frame consumer does not call this.
        // The continuous stream is flushed when the camera is disposed.
        private Task RequestFlushAsync()
        {
            lock (captureLock)
            {
                if (!captureStarted)
                {
                    return Task.CompletedTask;
                }

                if (flushTask != null)
                {
                    return flushTask;
                }

                if (sourceReader == null ||
                    sourceReaderCallback == null)
                {
                    return Task.CompletedTask;
                }

                try
                {
                    flushTask =
                        sourceReader.FlushAsync(
                            FirstVideoStream,
                            sourceReaderCallback);
                }
                catch (Exception exception)
                {
                    flushTask =
                        Task.FromException(exception);
                }

                return flushTask;
            }
        }


        // ============================================================
        // Asynchronous frame acquisition
        // ============================================================

        public async Task<(
            NorthStarFrame Frame,
            CameraCaptureMetrics CaptureMetrics)> GetFrameAsync(
                CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await frameReadGate
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                ThrowIfDisposed();

                cancellationToken.ThrowIfCancellationRequested();

                CameraCapability capability =
                    currentCapability
                    ?? throw new InvalidOperationException(
                        "The camera has not been configured.");

                if (currentStride <= 0)
                {
                    throw new InvalidOperationException(
                        "The configured camera does not have a valid stride.");
                }

                EnsureContinuousCaptureStarted();


                // Time spent waiting for an already-running
                // continuous capture stream to provide a sample.
                long queueWaitStart =
                    Stopwatch.GetTimestamp();

                SourceReaderSampleResult result =
                    await sourceReaderCallback!.TakeSampleAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                double sampleQueueWaitMilliseconds =
                    GetElapsedMilliseconds(queueWaitStart);


                bool sampleOwnershipTransferred = false;

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ThrowIfDisposed();

                    if (result.SamplePointer == 0)
                    {
                        if ((result.StreamFlags & EndOfStreamFlag) != 0)
                        {
                            throw new InvalidOperationException(
                                "Media Foundation reached the end " +
                                "of the camera stream.");
                        }

                        throw new InvalidOperationException(
                            "Media Foundation returned a sample without a buffer.");
                    }


                    // CreateFrameFromSample assumes ownership of
                    // the retained COM sample reference.
                    sampleOwnershipTransferred = true;

                    (
                        NorthStarFrame Frame,
                        CameraCaptureMetrics CaptureMetrics
                    ) capturedFrame =
                        CreateFrameFromSample(
                            result,
                            capability,
                            sampleQueueWaitMilliseconds);


                    cancellationToken.ThrowIfCancellationRequested();

                    ThrowIfDisposed();

                    return capturedFrame;
                }
                finally
                {
                    if (!sampleOwnershipTransferred &&
                        result.SamplePointer != 0)
                    {
                        ReleaseSample(
                            result.SamplePointer);
                    }
                }
            }
            finally
            {
                frameReadGate.Release();
            }
        }


        // ============================================================
        // Sample processing
        // ============================================================

        private unsafe (
            NorthStarFrame Frame,
            CameraCaptureMetrics CaptureMetrics) CreateFrameFromSample(
                SourceReaderSampleResult result,
                CameraCapability capability,
                double sampleQueueWaitMilliseconds)
        {
            IMFSample_unmanaged* sample =
                (IMFSample_unmanaged*)result.SamplePointer;

            double contiguousBufferMilliseconds = 0;

            double bufferLockMilliseconds = 0;

            double bufferAllocationMilliseconds = 0;

            double bufferCopyMilliseconds = 0;

            double frameTimestampIntervalMilliseconds = 0;

            try
            {
                IMFMediaBuffer_unmanaged* buffer = null;

                try
                {
                    long contiguousBufferStart =
                        Stopwatch.GetTimestamp();

                    sample->ConvertToContiguousBuffer(
                        out buffer);

                    contiguousBufferMilliseconds =
                        GetElapsedMilliseconds(
                            contiguousBufferStart);

                    if (buffer == null)
                    {
                        throw new InvalidOperationException(
                            "Media Foundation returned a null media buffer.");
                    }


                    long bufferLockStart =
                        Stopwatch.GetTimestamp();

                    buffer->Lock(
                        out byte* data,
                        out uint maxLength,
                        out uint currentLength);

                    bufferLockMilliseconds =
                        GetElapsedMilliseconds(
                            bufferLockStart);

                    try
                    {
                        if (currentLength > int.MaxValue ||
                            currentLength > maxLength)
                        {
                            throw new InvalidOperationException(
                                "Media Foundation returned an invalid sample buffer length.");
                        }

                        long allocationStart =
                            Stopwatch.GetTimestamp();

                        byte[] dataCopy =
                            new byte[(int)currentLength];

                        bufferAllocationMilliseconds =
                            GetElapsedMilliseconds(
                                allocationStart);


                        long copyStart =
                            Stopwatch.GetTimestamp();

                        Marshal.Copy(
                            (nint)data,
                            dataCopy,
                            0,
                            (int)currentLength);

                        bufferCopyMilliseconds =
                            GetElapsedMilliseconds(copyStart);


                        NorthStarFrame frame =
                            new NorthStarFrame(
                                capability.Width,
                                capability.Height,
                                currentStride,
                                result.Timestamp,
                                capability.Subtype,
                                null,
                                dataCopy);


                        // Measures the timestamp interval between
                        // frames delivered to the consumer.
                        // Frames discarded from the queue are not included.
                        if (previousFrameTimestamp.HasValue &&
                            result.Timestamp >= previousFrameTimestamp.Value)
                        {
                            long timestampDifference =
                                result.Timestamp -
                                previousFrameTimestamp.Value;

                            frameTimestampIntervalMilliseconds =
                                timestampDifference / 10_000.0;
                        }

                        previousFrameTimestamp =
                            result.Timestamp;


                        CameraCaptureMetrics metrics =
                            new CameraCaptureMetrics(
                                sampleQueueWaitMilliseconds,
                                contiguousBufferMilliseconds,
                                bufferLockMilliseconds,
                                bufferAllocationMilliseconds,
                                bufferCopyMilliseconds,
                                frameTimestampIntervalMilliseconds);

                        return (frame, metrics);
                    }
                    finally
                    {
                        buffer->Unlock();
                    }
                }
                finally
                {
                    if (buffer != null)
                    {
                        buffer->Release();
                    }
                }
            }
            finally
            {
                // Release the retained sample reference acquired
                // in SourceReaderCallback.OnReadSample().
                sample->Release();
            }
        }


        // ============================================================
        // COM reference management
        // ============================================================

        private static unsafe void ReleaseSample(nint sample)
        {
            if (sample == 0)
            {
                return;
            }

            void* pointer =
                (void*)sample;

            void** vtable =
                *(void***)pointer;

            ((delegate* unmanaged[Stdcall]<void*, uint>)
                vtable[2])(pointer);
        }


        private static unsafe void ReleaseComPointer(nint pointer)
        {
            if (pointer == 0)
            {
                return;
            }

            void* nativePointer =
                (void*)pointer;

            void** vtable =
                *(void***)nativePointer;

            ((delegate* unmanaged[Stdcall]<void*, uint>)
                vtable[2])(nativePointer);
        }


        // ============================================================
        // Disposal
        // ============================================================

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            Exception? flushFailure = null;

            try
            {
                // Stop the continuous source and wait for OnFlush.
                RequestFlushAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                flushFailure = exception;
            }


            // Wait until any active consumer has finished using
            // its sample and native resources.
            frameReadGate.Wait();

            try
            {
                currentStride = 0;

                currentCapability = null;

                previousFrameTimestamp = null;

                currentMediaType?.Dispose();
                currentMediaType = null;


                // Release the Source Reader before its callback.
                sourceReader?.Dispose();
                sourceReader = null;

                sourceReaderCallback?.Dispose();
                sourceReaderCallback = null;


                ReleaseComPointer(mediaSourcePointer);
                mediaSourcePointer = 0;


                ReleaseComPointer(activatePointer);
                activatePointer = 0;
            }
            finally
            {
                frameReadGate.Release();
            }

            GC.SuppressFinalize(this);

            if (flushFailure != null)
            {
                throw new InvalidOperationException(
                    "The camera Source Reader could not be flushed during disposal.",
                    flushFailure);
            }
        }


        // ============================================================
        // Timing
        // ============================================================

        private static double GetElapsedMilliseconds(
            long startTimestamp)
        {
            long elapsedTimestamp =
                Stopwatch.GetTimestamp() -
                startTimestamp;

            return
                elapsedTimestamp *
                1000.0 /
                Stopwatch.Frequency;
        }


        // ============================================================
        // Validation
        // ============================================================

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref disposed) != 0,
                this);
        }
    }
}
