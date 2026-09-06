using NorthStar.Frames;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar.Tracking
{
    // Continuously processes frames in the background and stores
    // the most recently completed tracking frame.
    public sealed class TrackingProcessor : IDisposable
    {
        private readonly Func<NorthStarTrackingFrame> frameProvider;

        private readonly CancellationTokenSource cancellationSource;

        private readonly object frameLock =
            new object();

        private NorthStarTrackingFrame? latestFrame;

        private Task? processingTask;

        private bool disposed;

        public TrackingProcessor(
            Func<NorthStarTrackingFrame> frameProvider)
        {
            this.frameProvider =
                frameProvider ??
                throw new ArgumentNullException(
                    nameof(frameProvider));

            cancellationSource =
                new CancellationTokenSource();
        }

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

        public void Start()
        {
            ThrowIfDisposed();

            if (processingTask != null)
            {
                throw new InvalidOperationException(
                    "Tracking processor has already been started.");
            }

            processingTask =
                Task.Run(
                    ProcessingLoop);
        }

        public void Stop()
        {
            if (disposed)
            {
                return;
            }

            cancellationSource.Cancel();

            Task? task =
                processingTask;

            if (task == null)
            {
                return;
            }

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
        }

        private void ProcessingLoop()
        {
            CancellationToken token =
                cancellationSource.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    NorthStarTrackingFrame frame =
                        frameProvider();

                    lock (frameLock)
                    {
                        latestFrame =
                            frame;
                    }
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

        public event EventHandler<Exception>? ProcessingFailed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Stop();

            cancellationSource.Dispose();

            disposed =
                true;

            GC.SuppressFinalize(
                this);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
        }
    }
}
