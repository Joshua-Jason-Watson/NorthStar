using NorthStar.Frames;
using System;

namespace NorthStar.Decoding
{
    public interface IFrameDecoder
    {
        bool Supports(Guid sourceFormat);

        NorthStarImage Decode(NorthStarFrame frame);
    }
}