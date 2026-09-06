using NorthStar.Formats;
using System;

namespace NorthStar.Frames
{
    // Represents one video frame captured by NorthStar.
    //
    // The supplied byte array becomes owned by this frame.
    // The caller must not access or modify the array after
    // constructing the frame.
    //
    // Consumers receive read-only access through ReadOnlyMemory<byte>.
    public sealed class NorthStarFrame
    {
        public int Width { get; }

        public int Height { get; }

        // Number of bytes between the beginning of one image row
        // and the beginning of the next row in the source layout.
        public int Stride { get; }

        // Presentation timestamp supplied by Media Foundation.
        public long Timestamp { get; }

        // Media Foundation subtype describing the format of Data.
        public Guid Subtype { get; }

        public NorthStarColorSpace? ColorSpace { get; }

        public ReadOnlyMemory<byte> Data { get; }

        public NorthStarFrame(
            int width,
            int height,
            int stride,
            long timestamp,
            Guid subtype,
            NorthStarColorSpace? colorSpace,
            byte[] data)
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
                    "Stride cannot be smaller than the frame width.");
            }

            if (subtype == Guid.Empty)
            {
                throw new ArgumentException(
                    "Frame subtype cannot be empty.",
                    nameof(subtype));
            }

            if (data == null)
            {
                throw new ArgumentNullException(
                    nameof(data));
            }

            if (data.Length == 0)
            {
                throw new ArgumentException(
                    "Frame data cannot be empty.",
                    nameof(data));
            }

            Width = width;
            Height = height;
            Stride = stride;
            Timestamp = timestamp;
            Subtype = subtype;
            ColorSpace = colorSpace;
            Data = data;
        }
    }
}
