using NorthStar.Camera;
using NorthStar.Conversion;
using NorthStar.Decoding;
using NorthStar.Formats;
using NorthStar.Frames;

using System;
using System.Diagnostics;

namespace NorthStar.Processing
{
    // Coordinates camera capture, frame decoding, and image
    // conversion for individual camera frames.
    //
    // This class processes one frame at a time. It does not own
    // tracking, background processing, or application lifecycle.
    public sealed class NorthStarPipeline
    {
        // ============================================================
        // Dependencies
        // ============================================================

        private readonly ICamera camera;
        private readonly FrameDecoderRegistry decoderRegistry;
        private readonly ImageConverterRegistry converterRegistry;


        // ============================================================
        // Constructor
        // ============================================================

        public NorthStarPipeline(
            ICamera camera,
            FrameDecoderRegistry decoderRegistry,
            ImageConverterRegistry converterRegistry)
        {
            this.camera =
                camera;

            this.decoderRegistry =
                decoderRegistry;

            this.converterRegistry =
                converterRegistry;
        }


        // ============================================================
        // Capture
        // ============================================================

        // Captures the next frame from the camera.
        public NorthStarFrame CaptureFrame()
        {
            return camera.GetFrame();
        }


        // ============================================================
        // Decode
        // ============================================================

        // Finds the decoder required for a camera frame.
        public IFrameDecoder GetDecoder(
            NorthStarFrame frame)
        {
            return decoderRegistry.GetDecoder(
                frame.Subtype);
        }

        // Captures and decodes the next camera frame.
        public NorthStarImage DecodeNextFrame()
        {
            return DecodeNextFrame(
                out _);
        }

        // Captures and decodes the next camera frame while
        // reporting the time spent on the decode stage.
        public NorthStarImage DecodeNextFrame(
            out double decodeMilliseconds)
        {
            long startTimestamp =
                Stopwatch.GetTimestamp();

            NorthStarFrame frame =
                CaptureFrame();

            IFrameDecoder decoder =
                GetDecoder(frame);

            NorthStarImage image =
                decoder.Decode(frame);

            decodeMilliseconds =
                GetElapsedMilliseconds(
                    startTimestamp);

            return image;
        }


        // ============================================================
        // Conversion
        // ============================================================

        // Captures, decodes, and converts the next camera frame
        // to the requested pixel format.
        public NorthStarImage ConvertNextFrameTo(
            NorthStarPixelFormat destinationFormat)
        {
            NorthStarImage image =
                DecodeNextFrame();

            return ConvertImage(
                image,
                destinationFormat);
        }

        // Converts an already-decoded image to the requested
        // pixel format.
        public NorthStarImage ConvertImage(
            NorthStarImage image,
            NorthStarPixelFormat destinationFormat)
        {
            if (image == null)
            {
                throw new ArgumentNullException(
                    nameof(image));
            }

            if (image.PixelFormat ==
                destinationFormat)
            {
                return image;
            }

            IImageConverter converter =
                converterRegistry.GetConverter(
                    image.PixelFormat,
                    destinationFormat);

            return converter.Convert(
                image,
                destinationFormat);
        }


        // ============================================================
        // Frame processing
        // ============================================================

        // Captures and prepares the next camera frame as a
        // BGR24 image for downstream consumers.
        public NorthStarImage ProcessNextFrame()
        {
            return ProcessNextFrame(
                out _);
        }

        // Captures and prepares the next camera frame as a
        // BGR24 image while reporting timing information for
        // each stage.
        public NorthStarImage ProcessNextFrame(
            out NorthStarPipelineMetrics metrics)
        {
            long totalStartTimestamp =
                Stopwatch.GetTimestamp();

            long captureStartTimestamp =
                Stopwatch.GetTimestamp();

            NorthStarFrame frame =
                CaptureFrame();

            double captureMilliseconds =
                GetElapsedMilliseconds(
                    captureStartTimestamp);

            long decodeStartTimestamp =
                Stopwatch.GetTimestamp();

            IFrameDecoder decoder =
                GetDecoder(frame);

            NorthStarImage image =
                decoder.Decode(frame);

            double decodeMilliseconds =
                GetElapsedMilliseconds(
                    decodeStartTimestamp);

            long conversionStartTimestamp =
                Stopwatch.GetTimestamp();

            NorthStarImage convertedImage =
                ConvertImage(
                    image,
                    NorthStarPixelFormat.BGR24);

            double conversionMilliseconds =
                GetElapsedMilliseconds(
                    conversionStartTimestamp);

            double totalMilliseconds =
                GetElapsedMilliseconds(
                    totalStartTimestamp);

            metrics =
                new NorthStarPipelineMetrics(
                    captureMilliseconds,
                    decodeMilliseconds,
                    conversionMilliseconds,
                    totalMilliseconds);

            return convertedImage;
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
    }
}