using NorthStar.Camera;
using NorthStar.Conversion;
using NorthStar.Decoding;
using NorthStar.Processing;
using NorthStar.Tracking;
using System;

namespace NorthStar
{
    // Owns the lifetime of the active NorthStar tracking engine.
    //
    // The runtime does not own the UI and does not decide which
    // camera or capability the user selects.
    public sealed class NorthStarRuntime : IDisposable
    {
        private readonly string modelPath;

        private CameraDevice? camera;
        private PoseModel? poseModel;
        private TrackingProcessor? processor;

        private bool running;
        private bool disposed;

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

        public bool IsRunning =>
            running;

        public NorthStarTrackingFrame? LatestFrame =>
            processor?.LatestFrame;

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

            PoseModel? newPoseModel =
                null;

            TrackingProcessor? newProcessor =
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

                newPoseModel =
                    new PoseModel(
                        modelPath);

                NorthStarPipeline pipeline =
                    new NorthStarPipeline(
                        newCamera,
                        decoderRegistry,
                        converterRegistry,
                        newPoseModel);

                newProcessor =
                    new TrackingProcessor(
                        pipeline.ProcessNextFrame);

                newProcessor.Start();

                camera =
                    newCamera;

                poseModel =
                    newPoseModel;

                processor =
                    newProcessor;

                running =
                    true;

                newCamera = null!;
                newPoseModel = null;
                newProcessor = null;
            }
            catch
            {
                newProcessor?.Dispose();
                newPoseModel?.Dispose();
                newCamera.Dispose();

                throw;
            }
        }

        public void Stop()
        {
            if (!running)
            {
                return;
            }

            running =
                false;

            processor?.Dispose();
            processor = null;

            poseModel?.Dispose();
            poseModel = null;

            camera?.Dispose();
            camera = null;
        }

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

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
        }
    }
}