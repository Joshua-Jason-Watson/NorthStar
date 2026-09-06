using NorthStar.Frames;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar.Tracking
{
    // Continuously processes the most recent image in the background
    // and stores the most recently completed tracking result.
    //
    // The processor owns pose tracking only. It does not capture
    // camera frames or perform image decoding/conversion.
    public sealed class TrackingProcessor : IDisposable
    {
        // ============================================================
        // Dependencies
        // ============================================================

        private readonly PoseModel poseModel;


        // ============================================================
        // Processing state
        // ============================================================

        private readonly object processingLock =
            new object();

        private CancellationTokenSource? cancellationSource;

        private Task? processingTask;

        private bool disposed;


        // ============================================================
        // Latest image
        // ============================================================

        private readonly object imageLock =
            new object();

        private NorthStarImage? latestImage;


        // ============================================================
        // Latest result
        // ============================================================

        private readonly object frameLock =
            new object();

        private NorthStarTrackingFrame? latestFrame;


        // ============================================================
        // Constructor
        // ============================================================

        public TrackingProcessor(
            PoseModel poseModel)
        {
            this.poseModel =
                poseModel ??
                throw new ArgumentNullException(
                    nameof(poseModel));
        }


        // ============================================================
        // State
        // ============================================================

        public bool IsRunning
        {
            get
            {
                lock (processingLock)
                {
                    return processingTask != null;
                }
            }
        }


        // ============================================================
        // Latest tracking result
        // ============================================================

        public NorthStarTrackingFrame? LatestFrame
        {
            get
            {
                lock (frameLock)
                {
                    return latestFrame;
                }
            }
        }


        // ============================================================
        // Frame submission
        // ============================================================

        // Supplies the newest image to the tracking processor.
        //
        // If tracking is currently processing another image, that
        // image is allowed to finish. Any image waiting to be processed
        // is replaced by the newly submitted image.
        public void SubmitFrame(
            NorthStarImage image)
        {
            ThrowIfDisposed();

            if (image == null)
            {
                throw new ArgumentNullException(
                    nameof(image));
            }

            lock (imageLock)
            {
                latestImage =
                    image;
            }
        }


        // ============================================================
        // Lifecycle
        // ============================================================

        public void Start()
        {
            ThrowIfDisposed();

            lock (processingLock)
            {
                if (processingTask != null)
                {
                    throw new InvalidOperationException(
                        "Tracking processor is already running.");
                }

                cancellationSource =
                    new CancellationTokenSource();

                CancellationToken token =
                    cancellationSource.Token;

                processingTask =
                    Task.Run(
                        () => ProcessingLoop(token));
            }
        }

        public void Stop()
        {
            CancellationTokenSource? source;
            Task? task;

            lock (processingLock)
            {
                source =
                    cancellationSource;

                task =
                    processingTask;

                if (source == null ||
                    task == null)
                {
                    return;
                }

                cancellationSource =
                    null;

                processingTask =
                    null;
            }

            source.Cancel();

            try
            {
                task.Wait(
                    TimeSpan.FromSeconds(2));
            }
            catch (AggregateException exception)
            {
                exception.Handle(
                    innerException =>
                        innerException is OperationCanceledException);
            }

            source.Dispose();

            lock (imageLock)
            {
                latestImage =
                    null;
            }
        }


        // ============================================================
        // Background processing
        // ============================================================

        private void ProcessingLoop(
            CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    NorthStarImage? image =
                        TakeLatestImage();

                    if (image == null)
                    {
                        Thread.Yield();

                        continue;
                    }

                    PoseResult pose =
                        poseModel.ProcessFrame(
                            image);

                    NorthStarTrackingFrame frame =
                        new NorthStarTrackingFrame(
                            image,
                            pose);

                    lock (frameLock)
                    {
                        latestFrame =
                            frame;
                    }

                    TrackingFrameReady?.Invoke(
                        this,
                        frame);
                }
            }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                ProcessingFailed?.Invoke(
                    this,
                    exception);
            }
        }


        // ============================================================
        // Image handoff
        // ============================================================

        private NorthStarImage? TakeLatestImage()
        {
            lock (imageLock)
            {
                NorthStarImage? image =
                    latestImage;

                latestImage =
                    null;

                return image;
            }
        }


        // ============================================================
        // Events
        // ============================================================

        public event EventHandler<
            NorthStarTrackingFrame>? TrackingFrameReady;

        public event EventHandler<
            Exception>? ProcessingFailed;


        // ============================================================
        // Disposal
        // ============================================================

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Stop();

            lock (imageLock)
            {
                latestImage =
                    null;
            }

            lock (frameLock)
            {
                latestFrame =
                    null;
            }

            disposed =
                true;

            GC.SuppressFinalize(
                this);
        }


        // ============================================================
        // Validation
        // ============================================================

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
        }
    }
}
