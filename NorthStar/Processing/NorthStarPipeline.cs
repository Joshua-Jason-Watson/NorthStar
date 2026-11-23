
using NorthStar.Camera;
using NorthStar.Conversion;
using NorthStar.Decoding;
using NorthStar.Formats;
using NorthStar.Frames;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar.Processing
{
    // Coordinates camera capture, frame decoding, and image
    // conversion for individual camera frames.
    //
    // Camera acquisition is asynchronous. Frame decoding and
    // image conversion are performed synchronously after capture.
    //
    // This class does not own tracking, background processing,
    // or application lifecycle.
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
                camera ?? throw new ArgumentNullException(nameof(camera));

            this.decoderRegistry =
                decoderRegistry
                ?? throw new ArgumentNullException(nameof(decoderRegistry));

            this.converterRegistry =
                converterRegistry
                ?? throw new ArgumentNullException(nameof(converterRegistry));
        }


        // ============================================================
        // Capture
        // ============================================================

        // Asynchronously captures the next frame and returns
        // its camera acquisition measurements.
        public Task<(
            NorthStarFrame Frame,
            CameraCaptureMetrics CaptureMetrics)> CaptureFrameAsync(
                CancellationToken cancellationToken = default)
        {
            return camera.GetFrameAsync(
                cancellationToken);
        }


        // ============================================================
        // Decode
        // ============================================================

        // Finds the decoder required for a camera frame.
        public IFrameDecoder GetDecoder(
            NorthStarFrame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);

            return decoderRegistry.GetDecoder(
                frame.Subtype);
        }

        // Asynchronously captures and decodes the next camera frame.
        public async Task<NorthStarImage> DecodeNextFrameAsync(
            CancellationToken cancellationToken = default)
        {
            (NorthStarFrame Frame, CameraCaptureMetrics CaptureMetrics)
                capturedFrame =
                    await CaptureFrameAsync(cancellationToken)
                        .ConfigureAwait(false);

            IFrameDecoder decoder =
                GetDecoder(capturedFrame.Frame);

            return decoder.Decode(
                capturedFrame.Frame);
        }


        // ============================================================
        // Conversion
        // ============================================================

        // Asynchronously captures, decodes, and converts the next
        // camera frame to the requested pixel format.
        public async Task<NorthStarImage> ConvertNextFrameToAsync(
            NorthStarPixelFormat destinationFormat,
            CancellationToken cancellationToken = default)
        {
            NorthStarImage image =
                await DecodeNextFrameAsync(cancellationToken)
                    .ConfigureAwait(false);

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
            ArgumentNullException.ThrowIfNull(image);

            if (image.PixelFormat == destinationFormat)
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

        // Asynchronously captures and prepares the next camera
        // frame as a BGR24 image.
        //
        // Returns both the processed image and stage measurements.
        public async Task<(
            NorthStarImage Image,
            NorthStarPipelineMetrics Metrics)> ProcessNextFrameAsync(
                CancellationToken cancellationToken = default)
        {
            long captureStartTimestamp =
                Stopwatch.GetTimestamp();

            (
                NorthStarFrame Frame,
                CameraCaptureMetrics CaptureMetrics
            ) capturedFrame =
                await CaptureFrameAsync(cancellationToken)
                    .ConfigureAwait(false);

            double captureMilliseconds =
                GetElapsedMilliseconds(
                    captureStartTimestamp);

            NorthStarImage image =
                ProcessCapturedFrame(
                    capturedFrame.Frame,
                    capturedFrame.CaptureMetrics,
                    captureMilliseconds,
                    out NorthStarPipelineMetrics metrics);

            return (image, metrics);
        }


        // Processes an already-captured frame.
        //
        // This allows camera acquisition and frame processing
        // to operate independently.
        public NorthStarImage ProcessCapturedFrame(
            NorthStarFrame frame,
            CameraCaptureMetrics cameraCaptureMetrics,
            double captureMilliseconds,
            out NorthStarPipelineMetrics metrics)
        {
            ArgumentNullException.ThrowIfNull(frame);

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
                captureMilliseconds +
                decodeMilliseconds +
                conversionMilliseconds;

            metrics =
                new NorthStarPipelineMetrics(
                    captureMilliseconds,
                    decodeMilliseconds,
                    conversionMilliseconds,
                    totalMilliseconds,
                    cameraCaptureMetrics);

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
