
using NorthStar.Camera;
using NorthStar.Frames;

using System;
using System.Threading;

namespace NorthStar.Processing;

// Contains a captured frame and the measurements associated with it.
internal sealed record CapturedFrame(
    NorthStarFrame Frame,
    CameraCaptureMetrics CaptureMetrics,
    double CaptureMilliseconds);


// Thread-safe, single-frame buffer.
//
// New frames replace older frames that have not yet been consumed.
// Consumers wait for a frame instead of continuously polling.
internal sealed class LatestFrameBuffer : IDisposable
{
    private readonly object sync =
        new object();

    private readonly SemaphoreSlim frameAvailable =
        new SemaphoreSlim(0, 1);

    private CapturedFrame? latestFrame;

    private bool completed;

    private bool disposed;


    // ============================================================
    // Publish
    // ============================================================

    public void Publish(
        CapturedFrame frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(
                nameof(frame));
        }

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            if (completed)
            {
                throw new InvalidOperationException(
                    "The frame buffer has been completed.");
            }

            // Replace any frame that has not yet been consumed.
            latestFrame =
                frame;

            // Only one notification is needed because the buffer
            // stores only the latest available frame.
            if (frameAvailable.CurrentCount == 0)
            {
                frameAvailable.Release();
            }
        }
    }


    // ============================================================
    // Take
    // ============================================================

    public CapturedFrame? Take(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            // Wait until a frame becomes available or cancellation
            // is requested.
            frameAvailable.Wait(
                cancellationToken);

            lock (sync)
            {
                if (latestFrame != null)
                {
                    CapturedFrame frame =
                        latestFrame;

                    latestFrame =
                        null;

                    return frame;
                }

                if (completed)
                {
                    return null;
                }
            }
        }
    }


    // ============================================================
    // Completion
    // ============================================================

    public void Complete()
    {
        lock (sync)
        {
            if (disposed || completed)
            {
                return;
            }

            completed =
                true;

            latestFrame =
                null;

            // Wake a waiting consumer so it can observe completion.
            if (frameAvailable.CurrentCount == 0)
            {
                frameAvailable.Release();
            }
        }
    }


    // ============================================================
    // Disposal
    // ============================================================

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            completed =
                true;

            latestFrame =
                null;

            disposed =
                true;
        }

        frameAvailable.Dispose();

        GC.SuppressFinalize(this);
    }
}
