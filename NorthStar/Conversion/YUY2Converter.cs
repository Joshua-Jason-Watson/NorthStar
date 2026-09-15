using NorthStar.Formats;
using NorthStar.Frames;
using System;

namespace NorthStar.Conversion
{
    public sealed class YUY2Converter : IImageConverter
    {
        public bool CanConvert(
            NorthStarPixelFormat sourceFormat,
            NorthStarPixelFormat destinationFormat)
        {
            return sourceFormat ==
                       NorthStarPixelFormat.YUY2 &&
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
                    "YUY2 image must have a positive stride.");
            }

            int minimumStride =
                checked(
                    width * 2);

            if (stride < minimumStride)
            {
                throw new InvalidOperationException(
                    "YUY2 image stride cannot be smaller " +
                    "than width * 2.");
            }

            int expectedSize =
                checked(
                    stride *
                    height);

            if (image.Data.Length <
                expectedSize)
            {
                throw new InvalidOperationException(
                    "YUY2 image does not contain enough data " +
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
                int inputRow =
                    y * stride;

                int outputRow =
                    y * outputStride;

                for (int x = 0;
                     x < width;
                     x++)
                {
                    int pairIndex =
                        x / 2;

                    int inputIndex =
                        inputRow +
                        (pairIndex * 4);

                    int yValue;

                    int u =
                        input[inputIndex + 1];

                    int v =
                        input[inputIndex + 3];

                    if ((x & 1) == 0)
                    {
                        yValue =
                            input[inputIndex];
                    }
                    else
                    {
                        yValue =
                            input[inputIndex + 2];
                    }

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