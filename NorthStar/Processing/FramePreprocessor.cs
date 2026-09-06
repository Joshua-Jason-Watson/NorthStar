using Microsoft.ML.OnnxRuntime.Tensors;
using NorthStar.Formats;
using NorthStar.Frames;
using System;

namespace NorthStar.Processing
{
    // Converts a NorthStarImage into the tensor format required by
    // the pose model.
    public sealed class FramePreprocessor
    {
        private static readonly float[] Means =
        {
            123.675f,
            116.28f,
            103.53f
        };

        private static readonly float[] Stds =
        {
            58.395f,
            57.12f,
            57.375f
        };

        private readonly int modelWidth;
        private readonly int modelHeight;

        public FramePreprocessor(
            int modelWidth,
            int modelHeight)
        {
            if (modelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modelWidth));
            }

            if (modelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modelHeight));
            }

            this.modelWidth =
                modelWidth;

            this.modelHeight =
                modelHeight;
        }

        public PreprocessResult CreateTensor(
            NorthStarImage image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(
                    nameof(image));
            }

            if (image.PixelFormat !=
                    NorthStarPixelFormat.RGB24 &&
                image.PixelFormat !=
                    NorthStarPixelFormat.BGR24)
            {
                throw new NotSupportedException(
                    $"FramePreprocessor requires RGB24 or BGR24. " +
                    $"Received {image.PixelFormat}.");
            }

            int originalWidth =
                image.Width;

            int originalHeight =
                image.Height;

            int minimumStride =
                checked(
                    originalWidth * 3);

            if (image.Stride < minimumStride)
            {
                throw new InvalidOperationException(
                    "RGB24/BGR24 image stride is smaller " +
                    "than the required row size.");
            }

            int minimumDataSize =
                checked(
                    image.Stride *
                    originalHeight);

            if (image.Data.Length < minimumDataSize)
            {
                throw new InvalidOperationException(
                    "Image data does not contain enough bytes " +
                    "for its declared dimensions and stride.");
            }

            DenseTensor<float> tensor =
                new DenseTensor<float>(
                    new[]
                    {
                        1,
                        3,
                        modelHeight,
                        modelWidth
                    });

            float scale =
                Math.Min(
                    (float)modelWidth /
                    originalWidth,
                    (float)modelHeight /
                    originalHeight);

            int resizedWidth =
                Math.Max(
                    1,
                    (int)(originalWidth * scale));

            int resizedHeight =
                Math.Max(
                    1,
                    (int)(originalHeight * scale));

            int paddingX =
                (modelWidth - resizedWidth) / 2;

            int paddingY =
                (modelHeight - resizedHeight) / 2;

            int destinationStride =
                checked(
                    modelWidth * 3);

            byte[] paddedImage =
                new byte[
                    checked(
                        destinationStride *
                        modelHeight)];

            ResizeRgb24(
                image,
                paddedImage,
                destinationStride,
                resizedWidth,
                resizedHeight,
                paddingX,
                paddingY);

            for (int y = 0;
                 y < modelHeight;
                 y++)
            {
                int rowOffset =
                    y * destinationStride;

                for (int x = 0;
                     x < modelWidth;
                     x++)
                {
                    int pixelOffset =
                        rowOffset +
                        (x * 3);

                    byte channel0 =
                        paddedImage[pixelOffset];

                    byte channel1 =
                        paddedImage[pixelOffset + 1];

                    byte channel2 =
                        paddedImage[pixelOffset + 2];

                    if (image.PixelFormat ==
                        NorthStarPixelFormat.BGR24)
                    {
                        // BGR24 source:
                        // channel0 = B
                        // channel1 = G
                        // channel2 = R
                        //
                        // RTMPose tensor:
                        // channel 0 = R
                        // channel 1 = G
                        // channel 2 = B

                        tensor[0, 0, y, x] =
                            (channel2 - Means[0]) /
                            Stds[0];

                        tensor[0, 1, y, x] =
                            (channel1 - Means[1]) /
                            Stds[1];

                        tensor[0, 2, y, x] =
                            (channel0 - Means[2]) /
                            Stds[2];
                    }
                    else
                    {
                        // RGB24 source is already in the
                        // channel order expected by the model.

                        tensor[0, 0, y, x] =
                            (channel0 - Means[0]) /
                            Stds[0];

                        tensor[0, 1, y, x] =
                            (channel1 - Means[1]) /
                            Stds[1];

                        tensor[0, 2, y, x] =
                            (channel2 - Means[2]) /
                            Stds[2];
                    }
                }
            }

            return new PreprocessResult(
                tensor,
                scale,
                paddingX,
                paddingY,
                originalWidth,
                originalHeight);
        }

        private static void ResizeRgb24(
            NorthStarImage source,
            byte[] destination,
            int destinationStride,
            int destinationWidth,
            int destinationHeight,
            int destinationX,
            int destinationY)
        {
            ReadOnlySpan<byte> sourceData =
                source.Data.Span;

            float xScale =
                (float)source.Width /
                destinationWidth;

            float yScale =
                (float)source.Height /
                destinationHeight;

            for (int y = 0;
                 y < destinationHeight;
                 y++)
            {
                float sourceY =
                    ((y + 0.5f) * yScale) -
                    0.5f;

                int y0 =
                    (int)MathF.Floor(sourceY);

                int y1 =
                    y0 + 1;

                float yFraction =
                    sourceY - y0;

                y0 =
                    Math.Clamp(
                        y0,
                        0,
                        source.Height - 1);

                y1 =
                    Math.Clamp(
                        y1,
                        0,
                        source.Height - 1);

                for (int x = 0;
                     x < destinationWidth;
                     x++)
                {
                    float sourceX =
                        ((x + 0.5f) * xScale) -
                        0.5f;

                    int x0 =
                        (int)MathF.Floor(sourceX);

                    int x1 =
                        x0 + 1;

                    float xFraction =
                        sourceX - x0;

                    x0 =
                        Math.Clamp(
                            x0,
                            0,
                            source.Width - 1);

                    x1 =
                        Math.Clamp(
                            x1,
                            0,
                            source.Width - 1);

                    int topLeft =
                        (y0 * source.Stride) +
                        (x0 * 3);

                    int topRight =
                        (y0 * source.Stride) +
                        (x1 * 3);

                    int bottomLeft =
                        (y1 * source.Stride) +
                        (x0 * 3);

                    int bottomRight =
                        (y1 * source.Stride) +
                        (x1 * 3);

                    int outputOffset =
                        ((destinationY + y) *
                         destinationStride) +
                        ((destinationX + x) * 3);

                    for (int channel = 0;
                         channel < 3;
                         channel++)
                    {
                        float top =
                            sourceData[topLeft + channel] *
                            (1f - xFraction) +
                            sourceData[topRight + channel] *
                            xFraction;

                        float bottom =
                            sourceData[bottomLeft + channel] *
                            (1f - xFraction) +
                            sourceData[bottomRight + channel] *
                            xFraction;

                        float value =
                            top *
                            (1f - yFraction) +
                            bottom *
                            yFraction;

                        destination[
                            outputOffset + channel] =
                            (byte)Math.Clamp(
                                (int)MathF.Round(value),
                                0,
                                255);
                    }
                }
            }
        }
    }
}
