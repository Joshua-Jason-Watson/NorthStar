using NorthStar.Frames;
using NorthStar.Formats;

namespace NorthStar.Conversion
{
    // Defines a conversion between two NorthStar image formats.
    //
    // Converters are independent of cameras, decoders, and tracking
    // models. Their responsibility is only to transform image data
    // from one supported pixel format into another.
    public interface IImageConverter
    {
        // Determines whether this converter supports the requested
        // source and destination pixel-format combination.
        bool CanConvert(
            NorthStarPixelFormat sourceFormat,
            NorthStarPixelFormat destinationFormat);

        // Converts the supplied image into the requested pixel format.
        NorthStarImage Convert(
            NorthStarImage image,
            NorthStarPixelFormat destinationFormat);
    }
}