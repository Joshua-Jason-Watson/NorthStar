using System;
using NorthStar.Formats;

namespace NorthStar.Frames
{
    public sealed class NorthStarImage
    {
        public int Width { get; }

        public int Height { get; }

        public int Stride { get; }

        // Original/native format identifier from the source.
        public Guid SourceFormat { get; }

        public NorthStarPixelFormat PixelFormat { get; }

        public NorthStarColorSpace? ColorSpace { get; }

        public ReadOnlyMemory<byte> Data { get; }

        public NorthStarImage(
            int width,
            int height,
            int stride,
            Guid sourceFormat,
            NorthStarPixelFormat pixelFormat,
            NorthStarColorSpace? colorSpace,
            ReadOnlyMemory<byte> data)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height));
            }

            if (stride <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stride));
            }

            if (stride < width)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stride),
                    "Stride cannot be smaller than the image width.");
            }

            if (sourceFormat == Guid.Empty)
            {
                throw new ArgumentException(
                    "Source format cannot be empty.",
                    nameof(sourceFormat));
            }

            if (data.Length == 0)
            {
                throw new ArgumentException(
                    "Image data cannot be empty.",
                    nameof(data));
            }

            Width = width;
            Height = height;
            Stride = stride;
            SourceFormat = sourceFormat;
            PixelFormat = pixelFormat;
            ColorSpace = colorSpace;
            Data = data;
        }
    }
}