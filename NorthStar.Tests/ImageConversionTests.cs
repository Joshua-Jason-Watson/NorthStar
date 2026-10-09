using NorthStar.Conversion;
using NorthStar.Formats;
using NorthStar.Frames;

namespace NorthStar.Tests
{
    public class ImageConversionTests
    {
        private static readonly Guid Nv12Subtype =
            new Guid(
                "3231564E-0000-0010-8000-00AA00389B71");

        [Fact]
        public void Registry_SelectsConverterForRequestedFormats()
        {
            ImageConverterRegistry registry =
                new ImageConverterRegistry();

            registry.Register(
                new NV12Converter());

            registry.Register(
                new YUY2Converter());

            Assert.IsType<NV12Converter>(
                registry.GetConverter(
                    NorthStarPixelFormat.NV12,
                    NorthStarPixelFormat.BGR24));

            Assert.IsType<YUY2Converter>(
                registry.GetConverter(
                    NorthStarPixelFormat.YUY2,
                    NorthStarPixelFormat.BGR24));
        }

        [Fact]
        public void Registry_ThrowsWhenConversionIsUnsupported()
        {
            ImageConverterRegistry registry =
                new ImageConverterRegistry();

            registry.Register(
                new NV12Converter());

            Assert.Throws<InvalidOperationException>(
                () => registry.GetConverter(
                    NorthStarPixelFormat.YUY2,
                    NorthStarPixelFormat.BGR24));
        }

        [Fact]
        public void Nv12Converter_ProducesBgr24Image()
        {
            byte[] nv12 =
            {
                128,
                128,
                128,
                128,
                128,
                128
            };

            NorthStarImage source =
                new NorthStarImage(
                    2,
                    2,
                    2,
                    Nv12Subtype,
                    NorthStarPixelFormat.NV12,
                    null,
                    nv12);

            NorthStarImage converted =
                new NV12Converter().Convert(
                    source,
                    NorthStarPixelFormat.BGR24);

            Assert.Equal(
                NorthStarPixelFormat.BGR24,
                converted.PixelFormat);

            Assert.Equal(
                2,
                converted.Width);

            Assert.Equal(
                2,
                converted.Height);

            Assert.Equal(
                6,
                converted.Stride);

            Assert.Equal(
                12,
                converted.Data.Length);
        }

        [Fact]
        public void Nv12Converter_RejectsTruncatedBuffer()
        {
            NorthStarImage source =
                new NorthStarImage(
                    2,
                    2,
                    2,
                    Nv12Subtype,
                    NorthStarPixelFormat.NV12,
                    null,
                    new byte[]
                    {
                        1,
                        2,
                        3
                    });

            Assert.Throws<InvalidOperationException>(
                () => new NV12Converter().Convert(
                    source,
                    NorthStarPixelFormat.BGR24));
        }
    }
}
