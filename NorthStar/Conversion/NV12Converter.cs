using NorthStar.Formats;
using NorthStar.Frames;
using System;

namespace NorthStar.Conversion
{
    public sealed class NV12Converter : IImageConverter
    {
        public bool CanConvert(
            NorthStarPixelFormat sourceFormat,
            NorthStarPixelFormat destinationFormat)
        {
            return sourceFormat ==
                       NorthStarPixelFormat.NV12 &&
                   destinationFormat ==
                       NorthStarPixelFormat.BGR24;
        }

        public NorthStarImage Convert(
            NorthStarImage image,
            NorthStarPixelFormat destinationFormat)
        {
            if (image == null)
            {
                throw new ArgumentNullException(
                    nameof(image));
            }

            if (!CanConvert(
                image.PixelFormat,
                destinationFormat))
            {
                throw new InvalidOperationException(
                    $"Unsupported conversion: " +
                    $"{image.PixelFormat} -> " +
                    $"{destinationFormat}.");
            }

            int width =
                image.Width;

            int height =
                image.Height;

            int stride =
                image.Stride;

            if (stride <= 0)
            {
                throw new InvalidOperationException(
                    "NV12 image must have a positive stride.");
            }

            if (stride < width)
            {
                throw new InvalidOperationException(
                    "NV12 image stride cannot be smaller " +
                    "than its width.");
            }

            // NV12 contains:
            //
            // Y plane:
            //     height rows
            //     stride bytes per row
            //
            // UV plane:
            //     height / 2 rows
            //     stride bytes per row
            //
            // Therefore the minimum required buffer size is:
            //
            //     stride * height +
            //     stride * ceil(height / 2)

            int yPlaneSize =
                checked(
                    stride *
                    height);

            int uvPlaneSize =
                checked(
                    stride *
                    ((height + 1) / 2));

            int expectedSize =
                checked(
                    yPlaneSize +
                    uvPlaneSize);

            if (image.Data.Length <
                expectedSize)
            {
                throw new InvalidOperationException(
                    "NV12 image does not contain enough data " +
                    "for its declared dimensions and stride.");
            }

            int outputStride =
                checked(
                    width * 3);

            byte[] output =
                new byte[
                    checked(
                        outputStride *
                        height)];

            ReadOnlySpan<byte> input =
                image.Data.Span;

            for (int y = 0;
                 y < height;
                 y++)
            {
                int yRow =
                    y * stride;

                int uvRow =
                    yPlaneSize +
                    ((y / 2) * stride);

                int outputRow =
                    y * outputStride;

                for (int x = 0;
                     x < width;
                     x++)
                {
                    int yIndex =
                        yRow + x;

                    int uvIndex =
                        uvRow +
                        (x & ~1);

                    int yValue =
                        input[yIndex];

                    int u =
                        input[uvIndex];

                    int v =
                        input[uvIndex + 1];

                    double c =
                        yValue - 16.0;

                    double d =
                        u - 128.0;

                    double e =
                        v - 128.0;

                    double red =
                        1.164383 * c +
                        1.792741 * e;

                    double green =
                        1.164383 * c -
                        0.213249 * d -
                        0.532909 * e;

                    double blue =
                        1.164383 * c +
                        2.112402 * d;

                    int outputIndex =
                        outputRow +
                        (x * 3);

                    output[outputIndex] =
                        ClampToByte(blue);

                    output[outputIndex + 1] =
                        ClampToByte(green);

                    output[outputIndex + 2] =
                        ClampToByte(red);
                }
            }

            return new NorthStarImage(
                width,
                height,
                outputStride,
                image.SourceFormat,
                NorthStarPixelFormat.BGR24,
                image.ColorSpace,
                output);
        }

        private static byte ClampToByte(
            double value)
        {
            if (value <= 0)
            {
                return 0;
            }

            if (value >= 255)
            {
                return 255;
            }

            return (byte)Math.Round(
                value);
        }
    }
}
