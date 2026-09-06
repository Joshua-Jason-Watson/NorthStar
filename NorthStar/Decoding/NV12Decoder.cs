using NorthStar.Formats;
using NorthStar.Frames;
using System;

namespace NorthStar.Decoding
{
    public sealed class NV12Decoder : IFrameDecoder
    {
        private static readonly Guid Nv12Subtype =
            new Guid(
                "3231564E-0000-0010-8000-00AA00389B71");

        public bool Supports(
            Guid sourceFormat)
        {
            return sourceFormat ==
                Nv12Subtype;
        }

        public NorthStarImage Decode(
            NorthStarFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(
                    nameof(frame));
            }

            if (!Supports(
                frame.Subtype))
            {
                throw new ArgumentException(
                    "The supplied frame is not an NV12 frame.",
                    nameof(frame));
            }

            return new NorthStarImage(
                frame.Width,
                frame.Height,
                frame.Stride,
                frame.Subtype,
                NorthStarPixelFormat.NV12,
                frame.ColorSpace,
                frame.Data);
        }
    }
}