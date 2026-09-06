using NorthStar.Camera;
using NorthStar.Conversion;
using NorthStar.Decoding;
using NorthStar.Formats;
using NorthStar.Frames;
using NorthStar.Processing;
using NorthStar.Tracking;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar
{
    // Owns the lifetime of the active NorthStar processing engine.
    //
    // The runtime coordinates camera capture, image processing,
    // preview output, and optional tracking.
    //
    // The runtime does not own the UI and does not decide which
    // camera or capability the user selects.
    public sealed class NorthStarRuntime : IDisposable
    {
        // ============================================================
        // Configuration
        // ============================================================

        private readonly string modelPath;


        // ============================================================
        // Processing dependencies
        // ============================================================

        private CameraDevice? camera;

        private NorthStarPipeline? pipeline;

        private PoseModel? poseModel;

        private TrackingProcessor? processor;


        // ============================================================
        // Processing state
        // ============================================================

        private CancellationTokenSource? cancellationSource;

        private Task? processingTask;

        private bool running;

        private bool disposed;


        // ============================================================
        // Latest image
        // ============================================================

        private readonly object imageLock =
            new object();

        private NorthStarImage? latestImage;


        // ============================================================
        // Constructor
        // ============================================================

        public NorthStarRuntime(
            string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException(
                    "Model path cannot be empty.",
                    nameof(modelPath));
            }

            this.modelPath =
                modelPath;
        }


        // ============================================================
        // State
        // ============================================================

        public bool IsRunning =>
            running;

        public bool IsTracking =>
            processor?.IsRunning == true;


        // ============================================================
        // Latest preview image
        // ============================================================

        public NorthStarImage? LatestImage
        {
            get
            {
                lock (imageLock)
                {
                    return latestImage;
                }
            }
        }


        // ============================================================
        // Latest tracking result
        // ============================================================

        public NorthStarTrackingFrame? LatestFrame =>
            processor?.LatestFrame;


        // ============================================================
        // Start
        // ============================================================

        public void Start(
            CameraDescriptor cameraDescriptor,
            CameraCapability capability)
        {
            ThrowIfDisposed();

            if (running)
            {
                throw new InvalidOperationException(
                    "NorthStar is already running.");
            }

            if (cameraDescriptor == null)
            {
                throw new ArgumentNullException(
                    nameof(cameraDescriptor));
            }

            if (capability == null)
            {
                throw new ArgumentNullException(
                    nameof(capability));
            }

            WindowsCameraDeviceFactory factory =
                new WindowsCameraDeviceFactory();

            CameraDevice newCamera =
                factory.Open(
                    cameraDescriptor);

            NorthStarPipeline? newPipeline =
                null;

            CancellationTokenSource? newCancellationSource =
                null;

            try
            {
                newCamera.Configure(
                    capability);

                FrameDecoderRegistry decoderRegistry =
                    new FrameDecoderRegistry();

                decoderRegistry.Register(
                    new NV12Decoder());

                ImageConverterRegistry converterRegistry =
                    new ImageConverterRegistry();

                converterRegistry.Register(
                    new NV12Converter());

                newPipeline =
                    new NorthStarPipeline(
                        newCamera,
                        decoderRegistry,
                        converterRegistry);

                newCancellationSource =
                    new CancellationTokenSource();

                camera =
                    newCamera;

                pipeline =
                    newPipeline;

                cancellationSource =
                    newCancellationSource;

                running =
                    true;

                processingTask =
                    Task.Run(
                        ProcessingLoop);

                newCamera = null!;
                newPipeline = null;
                newCancellationSource = null;
            }
            catch
            {
                newCancellationSource?.Dispose();
                newCamera.Dispose();

                throw;
            }
        }


        // ============================================================
        // Tracking control
        // ============================================================

        public void StartTracking()
        {
            ThrowIfDisposed();

            if (!running)
            {
                throw new InvalidOperationException(
                    "NorthStar must be running before tracking can start.");
            }

            if (processor != null)
            {
                throw new InvalidOperationException(
                    "Tracking is already active.");
            }

            PoseModel newPoseModel =
                new PoseModel(
                    modelPath);

            TrackingProcessor newProcessor =
                new TrackingProcessor(
                    newPoseModel);

            try
            {
                poseModel =
                    newPoseModel;

                processor =
                    newProcessor;

                newProcessor.Start();
            }
            catch
            {
                newProcessor.Dispose();
                newPoseModel.Dispose();

                poseModel = null;
                processor = null;

                throw;
            }
        }

        public void StopTracking()
        {
            if (disposed)
            {
                return;
            }

            TrackingProcessor? currentProcessor =
                processor;

            PoseModel? currentPoseModel =
                poseModel;

            processor = null;
            poseModel = null;

            currentProcessor?.Dispose();
            currentPoseModel?.Dispose();
        }


        // ============================================================
        // Camera processing loop
        // ============================================================

        private void ProcessingLoop()
        {
            CancellationToken token =
                cancellationSource!.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    NorthStarImage image =
                        pipeline!.ProcessNextFrame();

                    lock (imageLock)
                    {
                        latestImage =
                            image;
                    }

                    processor?.SubmitFrame(
                        image);
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
        // Events
        // ============================================================

        public event EventHandler<Exception>? ProcessingFailed;


        // ============================================================
        // Stop
        // ============================================================

        public void Stop()
        {
            if (!running)
            {
                return;
            }

            running =
                false;

            StopTracking();

            cancellationSource?.Cancel();

            Task? task =
                processingTask;

            if (task != null)
            {
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

            processingTask =
                null;

            cancellationSource?.Dispose();
            cancellationSource = null;

            camera?.Dispose();
            camera = null;

            pipeline = null;

            lock (imageLock)
            {
                latestImage =
                    null;
            }
        }


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
