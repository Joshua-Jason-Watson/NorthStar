
using NorthStar.Formats;
using NorthStar.Frames;

using System;

namespace NorthStar.Conversion
{
    // Converts NV12 images into BGR24 images.
    //
    // Uses fixed-point arithmetic and processes two adjacent
    // pixels together because NV12 shares chroma information
    // between horizontal pixel pairs.
    public sealed class NV12Converter : IImageConverter
    {
        // ============================================================
        // Conversion coefficients
        // ============================================================

        // BT.709 limited-range coefficients.
        //
        // Coefficients are scaled by 2^20 to allow accurate
        // integer arithmetic without floating-point operations.

        private const int PrecisionBits = 20;

        private const int FixedPointScale =
            1 << PrecisionBits;

        private const int RoundingOffset =
            FixedPointScale >> 1;

        private const int YCoefficient = 1220944;

        private const int RedVCoefficient = 1879825;

        private const int GreenUCoefficient = -223608;

        private const int GreenVCoefficient = -558796;

        private const int BlueUCoefficient = 2215014;


        // ============================================================
        // Format support
        // ============================================================

        public bool CanConvert(
            NorthStarPixelFormat sourceFormat,
            NorthStarPixelFormat destinationFormat)
        {
            return sourceFormat == NorthStarPixelFormat.NV12 &&
                   destinationFormat == NorthStarPixelFormat.BGR24;
        }


        // ============================================================
        // Conversion
        // ============================================================

        public NorthStarImage Convert(
            NorthStarImage image,
            NorthStarPixelFormat destinationFormat)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (!CanConvert(
                image.PixelFormat,
                destinationFormat))
            {
                throw new InvalidOperationException(
                    $"Unsupported conversion: " +
                    $"{image.PixelFormat} -> " +
                    $"{destinationFormat}.");
            }


            // ========================================================
            // Image validation
            // ========================================================

            int width = image.Width;

            int height = image.Height;

            int stride = image.Stride;

            if ((width & 1) != 0 ||
                (height & 1) != 0)
            {
                throw new InvalidOperationException(
                    "NV12 images must have even width and height.");
            }

            if (stride < width ||
                stride <= 0 ||
                (stride & 1) != 0)
            {
                throw new InvalidOperationException(
                    "NV12 image must have a valid positive even stride " +
                    "that is at least as large as its width.");
            }


            // ========================================================
            // Plane layout
            // ========================================================

            // NV12 contains two planes:
            //
            // Y plane:
            //     height rows
            //     stride bytes per row
            //
            // UV plane:
            //     height / 2 rows
            //     stride bytes per row
            //
            // Each UV pair is shared between two horizontal pixels.

            int yPlaneSize =
                checked(stride * height);

            int uvPlaneSize =
                checked(stride * (height / 2));

            int expectedSize =
                checked(yPlaneSize + uvPlaneSize);

            if (image.Data.Length < expectedSize)
            {
                throw new InvalidOperationException(
                    "NV12 image does not contain enough data " +
                    "for its declared dimensions and stride.");
            }


            // ========================================================
            // Output allocation
            // ========================================================

            int outputStride =
                checked(width * 3);

            byte[] output =
                new byte[
                    checked(outputStride * height)];

            ReadOnlySpan<byte> input =
                image.Data.Span;


            // ========================================================
            // Pixel conversion
            // ========================================================

            for (int y = 0; y < height; y++)
            {
                int yRow =
                    y * stride;

                int uvRow =
                    yPlaneSize +
                    ((y / 2) * stride);

                int outputRow =
                    y * outputStride;

                // Process two horizontal pixels at a time.
                for (int x = 0; x < width; x += 2)
                {
                    // Both pixels share these chroma values.
                    int uvIndex =
                        uvRow + x;

                    int u =
                        input[uvIndex] - 128;

                    int v =
                        input[uvIndex + 1] - 128;


                    // First pixel.
                    int firstY =
                        input[yRow + x] - 16;

                    int firstOutputIndex =
                        outputRow + (x * 3);

                    ConvertPixel(
                        output,
                        firstOutputIndex,
                        firstY,
                        u,
                        v);


                    // Second pixel.
                    int secondY =
                        input[yRow + x + 1] - 16;

                    int secondOutputIndex =
                        firstOutputIndex + 3;

                    ConvertPixel(
                        output,
                        secondOutputIndex,
                        secondY,
                        u,
                        v);
                }
            }


            // ========================================================
            // Result
            // ========================================================

            return new NorthStarImage(
                width,
                height,
                outputStride,
                image.SourceFormat,
                NorthStarPixelFormat.BGR24,
                image.ColorSpace,
                output);
        }


        // ============================================================
        // Individual pixel conversion
        // ============================================================

        private static void ConvertPixel(
            Span<byte> output,
            int outputIndex,
            int y,
            int u,
            int v)
        {
            int yComponent =
                YCoefficient * y;

            int blue =
                yComponent +
                BlueUCoefficient * u;

            int green =
                yComponent +
                GreenUCoefficient * u +
                GreenVCoefficient * v;

            int red =
                yComponent +
                RedVCoefficient * v;


            // NorthStar uses BGR24 ordering.
            output[outputIndex] =
                ClampToByte(blue);

            output[outputIndex + 1] =
                ClampToByte(green);

            output[outputIndex + 2] =
                ClampToByte(red);
        }


        // ============================================================
        // Fixed-point clamping and rounding
        // ============================================================

        private static byte ClampToByte(int value)
        {
            if (value <= 0)
            {
                return 0;
            }

            int maximum =
                255 * FixedPointScale;

            if (value >= maximum)
            {
                return 255;
            }

            // Round to nearest integer, using ties-to-even
            // to match Math.Round's previous default behaviour.
            int whole =
                value >> PrecisionBits;

            int remainder =
                value & (FixedPointScale - 1);

            if (remainder > RoundingOffset ||
                (remainder == RoundingOffset &&
                 (whole & 1) != 0))
            {
                whole++;
            }

            return (byte)whole;
        }
    }
}
