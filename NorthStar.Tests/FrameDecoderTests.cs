using NorthStar.Decoding;
using NorthStar.Formats;
using NorthStar.Frames;

namespace NorthStar.Tests
{
    public class FrameDecoderTests
    {
        private static readonly Guid Nv12Subtype =
            new Guid(
                "3231564E-0000-0010-8000-00AA00389B71");

        private static readonly Guid Yuy2Subtype =
            new Guid(
                "32595559-0000-0010-8000-00AA00389B71");

        [Fact]
        public void Registry_SelectsDecoderForRegisteredFormat()
        {
            FrameDecoderRegistry registry =
                new FrameDecoderRegistry();

            registry.Register(
                new NV12Decoder());

            registry.Register(
                new YUY2Decoder());

            Assert.IsType<NV12Decoder>(
                registry.GetDecoder(
                    Nv12Subtype));

            Assert.IsType<YUY2Decoder>(
                registry.GetDecoder(
                    Yuy2Subtype));
        }

        [Fact]
        public void Registry_ThrowsWhenFormatIsUnsupported()
        {
            FrameDecoderRegistry registry =
                new FrameDecoderRegistry();

            registry.Register(
                new NV12Decoder());

            Assert.Throws<InvalidOperationException>(
                () => registry.GetDecoder(
                    Yuy2Subtype));
        }

        [Fact]
        public void Registry_ThrowsWhenDecoderIsNull()
        {
            FrameDecoderRegistry registry =
                new FrameDecoderRegistry();

            Assert.Throws<ArgumentNullException>(
                () => registry.Register(
                    null!));
        }

        [Fact]
        public void Nv12Decoder_PreservesFrameDataAndMarksPixelFormat()
        {
            byte[] data =
            {
                1,
                2,
                3,
                4
            };

            NorthStarFrame frame =
                CreateFrame(
                    Nv12Subtype,
                    data);

            NorthStarImage image =
                new NV12Decoder().Decode(
                    frame);

            Assert.Equal(
                NorthStarPixelFormat.NV12,
                image.PixelFormat);

            Assert.Equal(
                frame.Width,
                image.Width);

            Assert.Equal(
                frame.Height,
                image.Height);

            Assert.Equal(
                frame.Stride,
                image.Stride);

            Assert.True(
                frame.Data.Span.SequenceEqual(
                    image.Data.Span));
        }

        [Fact]
        public void Yuy2Decoder_RejectsNv12Frame()
        {
            NorthStarFrame frame =
                CreateFrame(
                    Nv12Subtype,
                    new byte[]
                    {
                        1,
                        2,
                        3,
                        4
                    });

            Assert.Throws<ArgumentException>(
                () => new YUY2Decoder().Decode(
                    frame));
        }

        private static NorthStarFrame CreateFrame(
            Guid subtype,
            byte[] data)
        {
            return new NorthStarFrame(
                2,
                2,
                2,
                0,
                subtype,
                null,
                data);
        }
    }
}
