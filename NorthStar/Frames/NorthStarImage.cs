
using System;

using NorthStar.Formats;

namespace NorthStar.Frames
{
    // Represents an image in NorthStar's internal pixel format.
    //
    // Unlike NorthStarFrame, this represents decoded pixel data
    // rather than a raw native camera sample.
    //
    // The image retains read-only access to its underlying memory.
    // It does not copy the supplied buffer.
    public sealed class NorthStarImage
    {
        // ============================================================
        // Image dimensions
        // ============================================================

        public int Width { get; }

        public int Height { get; }

        // Number of bytes between the beginning of consecutive rows.
        public int Stride { get; }


        // ============================================================
        // Image format
        // ============================================================

        // Original/native format identifier from the source.
        public Guid SourceFormat { get; }

        // Actual pixel layout represented by Data.
        public NorthStarPixelFormat PixelFormat { get; }


        // ============================================================
        // Color metadata
        // ============================================================

        public NorthStarColorSpace? ColorSpace { get; }


        // ============================================================
        // Image data
        // ============================================================

        public ReadOnlyMemory<byte> Data { get; }


        // ============================================================
        // Constructor
        // ============================================================

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

            if (sourceFormat == Guid.Empty)
            {
                throw new ArgumentException(
                    "Source format cannot be empty.",
                    nameof(sourceFormat));
            }


            // Determine the actual memory-layout requirements
            // of the declared pixel format.
            int minimumStride =
                GetMinimumStride(
                    width,
                    pixelFormat);

            if (stride < minimumStride)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stride),
                    stride,
                    $"Stride must be at least {minimumStride} bytes " +
                    $"for {pixelFormat}.");
            }


            // Determine how much image data must be available.
            int requiredDataLength =
                GetRequiredDataLength(
                    stride,
                    height,
                    pixelFormat);

            if (data.Length < requiredDataLength)
            {
                throw new ArgumentException(
                    $"Image data is too small for the declared " +
                    $"{width}x{height} {pixelFormat} layout. " +
                    $"Required: {requiredDataLength} bytes. " +
                    $"Available: {data.Length} bytes.",
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


        // ============================================================
        // Pixel-format layout
        // ============================================================

        private static int GetMinimumStride(
            int width,
            NorthStarPixelFormat pixelFormat)
        {
            return pixelFormat switch
            {
                NorthStarPixelFormat.Gray8 =>
                    width,

                NorthStarPixelFormat.RGB24 =>
                    checked(width * 3),

                NorthStarPixelFormat.BGR24 =>
                    checked(width * 3),

                NorthStarPixelFormat.RGBA32 =>
                    checked(width * 4),

                NorthStarPixelFormat.BGRA32 =>
                    checked(width * 4),

                NorthStarPixelFormat.NV12 =>
                    ValidateNv12Dimensions(width),

                NorthStarPixelFormat.YUY2 =>
                    ValidateYuy2Dimensions(width),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(pixelFormat),
                        pixelFormat,
                        "Unsupported NorthStar pixel format.")
            };
        }


        // ============================================================
        // Required buffer size
        // ============================================================

        private static int GetRequiredDataLength(
            int stride,
            int height,
            NorthStarPixelFormat pixelFormat)
        {
            return pixelFormat switch
            {
                // One byte per pixel.
                NorthStarPixelFormat.Gray8 =>
                    checked(stride * height),

                // Three bytes per pixel.
                NorthStarPixelFormat.RGB24 =>
                    checked(stride * height),

                NorthStarPixelFormat.BGR24 =>
                    checked(stride * height),

                // Four bytes per pixel.
                NorthStarPixelFormat.RGBA32 =>
                    checked(stride * height),

                NorthStarPixelFormat.BGRA32 =>
                    checked(stride * height),

                // Luma plane plus half-height chroma plane.
                NorthStarPixelFormat.NV12 =>
                    checked(
                        stride * height +
                        stride * (height / 2)),

                // Packed 4:2:2 format.
                NorthStarPixelFormat.YUY2 =>
                    checked(stride * height),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(pixelFormat))
            };
        }


        // ============================================================
        // NV12 validation
        // ============================================================

        private static int ValidateNv12Dimensions(int width)
        {
            if ((width & 1) != 0)
            {
                throw new ArgumentException(
                    "NV12 images must have even width.",
                    nameof(width));
            }

            return width;
        }


        // ============================================================
        // YUY2 validation
        // ============================================================

        private static int ValidateYuy2Dimensions(int width)
        {
            if ((width & 1) != 0)
            {
                throw new ArgumentException(
                    "YUY2 images must have even width.",
                    nameof(width));
            }

            return checked(width * 2);
        }
    }
}
