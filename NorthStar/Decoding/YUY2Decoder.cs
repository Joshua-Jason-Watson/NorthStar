using NorthStar.Formats;
using NorthStar.Frames;
using System;

namespace NorthStar.Decoding
{
    public sealed class YUY2Decoder : IFrameDecoder
    {
        private static readonly Guid Yuy2Subtype =
            new Guid(
                "32595559-0000-0010-8000-00AA00389B71");

        public bool Supports(
            Guid sourceFormat)
        {
            return sourceFormat ==
                Yuy2Subtype;
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
                    "The supplied frame is not a YUY2 frame.",
                    nameof(frame));
            }

            return new NorthStarImage(
                frame.Width,
                frame.Height,
                frame.Stride,
                frame.Subtype,
                NorthStarPixelFormat.YUY2,
                frame.ColorSpace,
                frame.Data);
        }
    }
}