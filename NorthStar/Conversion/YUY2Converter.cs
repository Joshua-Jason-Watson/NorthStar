
using NorthStar.Formats;
using NorthStar.Frames;

using System;

namespace NorthStar.Conversion
{
    // Converts YUY2 images into BGR24 images.
    //
    // Uses fixed-point arithmetic and processes two adjacent
    // pixels together because YUY2 shares chroma information
    // between horizontal pixel pairs.
    public sealed class YUY2Converter : IImageConverter
    {
        // ============================================================
        // Conversion coefficients
        // ============================================================

        // BT.709 limited-range coefficients.
        // Values are scaled by 2^20.

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
            return sourceFormat == NorthStarPixelFormat.YUY2 &&
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
            // Input validation
            // ========================================================

            int width = image.Width;

            int height = image.Height;

            int stride = image.Stride;

            if ((width & 1) != 0)
            {
                throw new InvalidOperationException(
                    "YUY2 image width must be even.");
            }

            int minimumStride =
                checked(width * 2);

            if (stride <= 0 || stride < minimumStride)
            {
                throw new InvalidOperationException(
                    "YUY2 image stride cannot be smaller " +
                    "than width * 2.");
            }

            int expectedSize =
                checked(stride * height);

            if (image.Data.Length < expectedSize)
            {
                throw new InvalidOperationException(
                    "YUY2 image does not contain enough data " +
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
                int inputRow =
                    y * stride;

                int outputRow =
                    y * outputStride;

                for (int x = 0; x < width; x += 2)
                {
                    // YUY2 stores pixels in the following order:
                    //
                    // Y0 U0 Y1 V0
                    //
                    // Both pixels share the same U and V values.

                    int inputIndex =
                        inputRow + (x * 2);

                    int y0 =
                        input[inputIndex] - 16;

                    int u =
                        input[inputIndex + 1] - 128;

                    int y1 =
                        input[inputIndex + 2] - 16;

                    int v =
                        input[inputIndex + 3] - 128;


                    // Calculate chroma contributions once
                    // for both pixels.
                    int blueChroma =
                        BlueUCoefficient * u;

                    int greenChroma =
                        GreenUCoefficient * u +
                        GreenVCoefficient * v;

                    int redChroma =
                        RedVCoefficient * v;


                    // First pixel.
                    ConvertPixel(
                        output,
                        outputRow + (x * 3),
                        YCoefficient * y0,
                        blueChroma,
                        greenChroma,
                        redChroma);


                    // Second pixel.
                    ConvertPixel(
                        output,
                        outputRow + (x * 3) + 3,
                        YCoefficient * y1,
                        blueChroma,
                        greenChroma,
                        redChroma);
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
            int yComponent,
            int blueChroma,
            int greenChroma,
            int redChroma)
        {
            int blue =
                yComponent + blueChroma;

            int green =
                yComponent + greenChroma;

            int red =
                yComponent + redChroma;


            // NorthStar uses BGR24 channel ordering.
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

            // Match Math.Round's default ties-to-even behaviour.
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
