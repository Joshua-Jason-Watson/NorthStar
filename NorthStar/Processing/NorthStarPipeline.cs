using NorthStar.Camera;
using NorthStar.Conversion;
using NorthStar.Decoding;
using NorthStar.Formats;
using NorthStar.Frames;
using NorthStar.Tracking;

namespace NorthStar.Processing
{
    // Coordinates camera capture, frame decoding, image conversion,
    // and pose tracking.
    public sealed class NorthStarPipeline
    {
        private readonly ICamera camera;
        private readonly FrameDecoderRegistry decoderRegistry;
        private readonly ImageConverterRegistry converterRegistry;
        private readonly PoseModel poseModel;

        public NorthStarPipeline(
            ICamera camera,
            FrameDecoderRegistry decoderRegistry,
            ImageConverterRegistry converterRegistry,
            PoseModel poseModel)
        {
            this.camera = camera;
            this.decoderRegistry = decoderRegistry;
            this.converterRegistry = converterRegistry;
            this.poseModel = poseModel;
        }

        public NorthStarFrame CaptureFrame()
        {
            return camera.GetFrame();
        }

        public IFrameDecoder GetDecoder(
            NorthStarFrame frame)
        {
            return decoderRegistry.GetDecoder(
                frame.Subtype);
        }

        public NorthStarImage DecodeNextFrame()
        {
            Console.WriteLine("Calling camera.GetFrame...");

            NorthStarFrame frame =
                CaptureFrame();

            Console.WriteLine("camera.GetFrame returned.");

            IFrameDecoder decoder =
                GetDecoder(frame);

            return decoder.Decode(frame);
        }

        public NorthStarImage ConvertNextFrameTo(
            NorthStarPixelFormat destinationFormat)
        {
            Console.WriteLine("Starting DecodeNextFrame...");

            NorthStarImage image =
                DecodeNextFrame();

            Console.WriteLine("DecodeNextFrame returned.");

            if (image.PixelFormat ==
                destinationFormat)
            {
                return image;
            }

            Console.WriteLine(
                $"Converting {image.PixelFormat} -> {destinationFormat}...");

            IImageConverter converter =
                converterRegistry.GetConverter(
                    image.PixelFormat,
                    destinationFormat);

            NorthStarImage converted =
                converter.Convert(
                    image,
                    destinationFormat);

            Console.WriteLine("Conversion returned.");

            return converted;
        }

        // Processes one complete camera frame through the image
        // and pose-tracking pipeline.
        public NorthStarTrackingFrame ProcessNextFrame()
        {
            Console.WriteLine("Capturing frame...");

            NorthStarImage image =
                ConvertNextFrameTo(
                    NorthStarPixelFormat.BGR24);

            Console.WriteLine("Image ready.");

            PoseResult pose =
                poseModel.ProcessFrame(
                    image);

            Console.WriteLine("Pose ready.");

            return new NorthStarTrackingFrame(
                image,
                pose);
        }
    }
}