using NorthStar.Camera;
using NorthStar.Conversion;
using NorthStar.Decoding;
using NorthStar.Formats;
using NorthStar.Frames;
using NorthStar.Processing;
using NorthStar.Tracking;

using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar
{
    // Owns the lifetime of the active NorthStar processing engine.
    //
    // Camera acquisition and frame processing operate independently.
    // Captured frames are exchanged through a latest-frame buffer.
    //
    // The runtime does not own the UI or decide which camera or
    // capability the user selects.
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

        private Task? captureTask;

        private Task? processingTask;

        private LatestFrameBuffer? frameBuffer;

        private bool running;

        private bool disposed;


        // ============================================================
        // Latest image
        // ============================================================

        private readonly object imageLock =
            new object();

        private NorthStarImage? latestImage;


        // ============================================================
        // Latest pipeline metrics
        // ============================================================

        private readonly object pipelineMetricsLock =
            new object();

        private NorthStarPipelineMetrics? latestPipelineMetrics;


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

            this.modelPath = modelPath;
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
        // Latest pipeline metrics
        // ============================================================

        public NorthStarPipelineMetrics? LatestPipelineMetrics
        {
            get
            {
                lock (pipelineMetricsLock)
                {
                    return latestPipelineMetrics;
                }
            }
        }


        // ============================================================
        // Latest pose-model metrics
        // ============================================================

        public PoseModelMetrics? LatestPoseModelMetrics =>
            processor?.LatestPoseModelMetrics;


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
                factory.Open(cameraDescriptor);

            NorthStarPipeline? newPipeline = null;

            CancellationTokenSource? newCancellationSource = null;

            LatestFrameBuffer? newFrameBuffer = null;

            try
            {
                newCamera.Configure(capability);

                FrameDecoderRegistry decoderRegistry =
                    new FrameDecoderRegistry();

                decoderRegistry.Register(
                    new NV12Decoder());

                decoderRegistry.Register(
                    new YUY2Decoder());

                ImageConverterRegistry converterRegistry =
                    new ImageConverterRegistry();

                converterRegistry.Register(
                    new NV12Converter());

                converterRegistry.Register(
                    new YUY2Converter());


                newPipeline =
                    new NorthStarPipeline(
                        newCamera,
                        decoderRegistry,
                        converterRegistry);

                newFrameBuffer =
                    new LatestFrameBuffer();

                newCancellationSource =
                    new CancellationTokenSource();


                // Preserve references for both workers.
                NorthStarPipeline activePipeline =
                    newPipeline;

                LatestFrameBuffer activeFrameBuffer =
                    newFrameBuffer;

                CancellationTokenSource activeCancellationSource =
                    newCancellationSource;


                camera = newCamera;
                pipeline = activePipeline;
                frameBuffer = activeFrameBuffer;
                cancellationSource = activeCancellationSource;

                running = true;


                // Start asynchronous camera acquisition independently.
                captureTask =
                    Task.Run(
                        () => CaptureLoopAsync(
                            activePipeline,
                            activeFrameBuffer,
                            activeCancellationSource));


                // Start image processing independently.
                processingTask =
                    Task.Run(
                        () => ProcessingLoop(
                            activePipeline,
                            activeFrameBuffer,
                            activeCancellationSource));


                // Ownership has transferred to the runtime.
                newCamera = null!;
                newPipeline = null;
                newFrameBuffer = null;
                newCancellationSource = null;
            }
            catch
            {
                running = false;

                newFrameBuffer?.Dispose();

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
                new PoseModel(modelPath);

            TrackingProcessor newProcessor =
                new TrackingProcessor(newPoseModel);

            try
            {
                poseModel = newPoseModel;
                processor = newProcessor;

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
        // Camera capture worker
        // ============================================================

        private async Task CaptureLoopAsync(
            NorthStarPipeline currentPipeline,
            LatestFrameBuffer currentFrameBuffer,
            CancellationTokenSource currentCancellationSource)
        {
            CancellationToken token =
                currentCancellationSource.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    long captureStartTimestamp =
                        Stopwatch.GetTimestamp();

                    // Wait asynchronously for the next camera frame.
                    (
                        NorthStarFrame Frame,
                        CameraCaptureMetrics CaptureMetrics
                    ) capturedResult =
                        await currentPipeline.CaptureFrameAsync(token)
                            .ConfigureAwait(false);

                    double captureMilliseconds =
                        GetElapsedMilliseconds(
                            captureStartTimestamp);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    CapturedFrame capturedFrame =
                        new CapturedFrame(
                            capturedResult.Frame,
                            capturedResult.CaptureMetrics,
                            captureMilliseconds);

                    currentFrameBuffer.Publish(
                        capturedFrame);
                }
            }
            catch (OperationCanceledException)
                when (token.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                {
                    currentCancellationSource.Cancel();

                    ProcessingFailed?.Invoke(
                        this,
                        exception);
                }
            }
            finally
            {
                // Notify the processing worker that no more
                // camera frames will be produced.
                currentFrameBuffer.Complete();
            }
        }


        // ============================================================
        // Frame processing worker
        // ============================================================

        private void ProcessingLoop(
            NorthStarPipeline currentPipeline,
            LatestFrameBuffer currentFrameBuffer,
            CancellationTokenSource currentCancellationSource)
        {
            CancellationToken token =
                currentCancellationSource.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    CapturedFrame? capturedFrame =
                        currentFrameBuffer.Take(token);

                    if (capturedFrame == null)
                    {
                        break;
                    }

                    NorthStarImage image =
                        currentPipeline.ProcessCapturedFrame(
                            capturedFrame.Frame,
                            capturedFrame.CaptureMetrics,
                            capturedFrame.CaptureMilliseconds,
                            out NorthStarPipelineMetrics metrics);


                    lock (imageLock)
                    {
                        latestImage = image;
                    }

                    lock (pipelineMetricsLock)
                    {
                        latestPipelineMetrics = metrics;
                    }

                    processor?.SubmitFrame(image);
                }
            }
            catch (OperationCanceledException)
                when (token.IsCancellationRequested)
            {
                // Normal worker shutdown.
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                {
                    currentCancellationSource.Cancel();

                    ProcessingFailed?.Invoke(
                        this,
                        exception);
                }
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

            running = false;

            // Cancelling the runtime token also cancels the
            // outstanding asynchronous camera read.
            cancellationSource?.Cancel();

            Task? currentCaptureTask =
                captureTask;

            Task? currentProcessingTask =
                processingTask;

            // Wait for both workers before disposing any resources
            // they may still be using.
            try
            {
                Task[] activeTasks =
                    new[] {
                        currentCaptureTask,
                        currentProcessingTask
                    }
                    .OfType<Task>()
                    .ToArray();

                Task.WaitAll(activeTasks);
            }
            catch (AggregateException exception)
            {
                exception.Handle(
                    innerException =>
                        innerException is OperationCanceledException);
            }

            // Tracking can now be safely stopped because the
            // processing worker is no longer submitting frames.
            StopTracking();

            captureTask = null;
            processingTask = null;

            frameBuffer?.Dispose();
            frameBuffer = null;

            cancellationSource?.Dispose();
            cancellationSource = null;

            camera?.Dispose();
            camera = null;

            pipeline = null;

            lock (imageLock)
            {
                latestImage = null;
            }

            lock (pipelineMetricsLock)
            {
                latestPipelineMetrics = null;
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

            disposed = true;

            GC.SuppressFinalize(this);
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
                disposed,
                this);
        }
    }
}
