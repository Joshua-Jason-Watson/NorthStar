using NorthStar.Frames;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar.Tracking
{
    // Continuously processes the most recent image in the background
    // and stores the most recently completed tracking result.
    //
    // The processor owns pose tracking only. It does not capture
    // camera frames or perform image decoding/conversion.
    //
    // Landmarks below the minimum confidence threshold are excluded
    // from the tracking result.
    public sealed class TrackingProcessor : IDisposable
    {
        // ============================================================
        // Configuration
        // ============================================================

        private const float MinimumConfidence =
            0.50f;


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
        // Latest metrics
        // ============================================================

        private readonly object metricsLock =
            new object();

        private PoseModelMetrics? latestPoseModelMetrics;


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
        // Latest pose-model metrics
        // ============================================================

        public PoseModelMetrics? LatestPoseModelMetrics
        {
            get
            {
                lock (metricsLock)
                {
                    return latestPoseModelMetrics;
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
            }

            source.Cancel();

            try
            {
                task.Wait();
            }
            catch (AggregateException exception)
            {
                exception.Handle(
                    innerException =>
                        innerException is OperationCanceledException);
            }

            bool completedShutdown = false;

            lock (processingLock)
            {
                if (ReferenceEquals(
                        cancellationSource,
                        source) &&
                    ReferenceEquals(
                        processingTask,
                        task))
                {
                    cancellationSource =
                        null;

                    processingTask =
                        null;

                    completedShutdown = true;
                }
            }

            if (!completedShutdown)
            {
                return;
            }

            source.Dispose();

            lock (imageLock)
            {
                latestImage =
                    null;
            }

            lock (metricsLock)
            {
                latestPoseModelMetrics =
                    null;
            }

            lock (frameLock)
            {
                latestFrame =
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
                            image,
                            out PoseModelMetrics metrics);

                    lock (metricsLock)
                    {
                        latestPoseModelMetrics =
                            metrics;
                    }

                    PoseResult filteredPose =
                        FilterPose(
                            pose);

                    NorthStarTrackingFrame frame =
                        new NorthStarTrackingFrame(
                            image,
                            filteredPose);

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
        // Confidence filtering
        // ============================================================

        private static PoseResult FilterPose(
            PoseResult pose)
        {
            List<Landmark> filteredLandmarks =
                new List<Landmark>(
                    pose.Landmarks.Count);

            foreach (
                Landmark landmark
                in pose.Landmarks)
            {
                if (landmark.Confidence <
                    MinimumConfidence)
                {
                    continue;
                }

                filteredLandmarks.Add(
                    landmark);
            }

            return new PoseResult(
                filteredLandmarks);
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

            lock (metricsLock)
            {
                latestPoseModelMetrics =
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