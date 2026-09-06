using NorthStar.Formats;
using System;
using System.Collections.Generic;

namespace NorthStar.Conversion
{
    // Maintains the image converters available to NorthStar and
    // selects the appropriate converter for a requested format change.
    public sealed class ImageConverterRegistry
    {
        private readonly List<IImageConverter> converters =
            new List<IImageConverter>();

        // Registers an image converter with NorthStar.
        public void Register(
            IImageConverter converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(
                    nameof(converter));
            }

            converters.Add(
                converter);
        }

        // Finds a converter capable of performing the requested
        // source-to-destination pixel-format conversion.
        public IImageConverter GetConverter(
            NorthStarPixelFormat sourceFormat,
            NorthStarPixelFormat destinationFormat)
        {
            foreach (
                IImageConverter converter
                in converters)
            {
                if (converter.CanConvert(
                    sourceFormat,
                    destinationFormat))
                {
                    return converter;
                }
            }

            throw new InvalidOperationException(
                $"No converter supports " +
                $"{sourceFormat} -> {destinationFormat}.");
        }
    }
}