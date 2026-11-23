
using NorthStar.Conversion;
using NorthStar.Formats;
using NorthStar.Frames;

namespace NorthStar.Tests.Conversion;

public sealed class YUY2ConverterTests
{
    private readonly YUY2Converter _converter = new();

    [Fact]
    public void CanConvert_ReturnsTrue_ForYUY2ToBGR24()
    {
        bool result = _converter.CanConvert(
            NorthStarPixelFormat.YUY2,
            NorthStarPixelFormat.BGR24);

        Assert.True(result);
    }

    [Fact]
    public void CanConvert_ReturnsFalse_ForUnsupportedFormats()
    {
        Assert.False(_converter.CanConvert(
            NorthStarPixelFormat.Gray8,
            NorthStarPixelFormat.BGR24));

        Assert.False(_converter.CanConvert(
            NorthStarPixelFormat.YUY2,
            NorthStarPixelFormat.RGB24));
    }

    [Fact]
    public void Convert_ConvertsTwoPixelsIntoBGR24()
    {
        byte[] inputData =
        {
            81, 90, 145, 240
        };

        NorthStarImage image = CreateImage(
            2,
            1,
            4,
            NorthStarPixelFormat.YUY2,
            inputData);

        NorthStarImage result = _converter.Convert(
            image,
            NorthStarPixelFormat.BGR24);

        byte[] expected =
        {
            0, 24, 255,
            70, 99, 255
        };

        Assert.Equal(2, result.Width);
        Assert.Equal(1, result.Height);
        Assert.Equal(6, result.Stride);
        Assert.Equal(NorthStarPixelFormat.BGR24, result.PixelFormat);
        Assert.Equal(image.SourceFormat, result.SourceFormat);
        Assert.Equal(image.ColorSpace, result.ColorSpace);
        Assert.Equal(expected, result.Data.ToArray());
    }

    [Fact]
    public void Convert_HandlesMultipleRowsAndInputPadding()
    {
        byte[] inputData =
        {
            // First row: black, white
            16, 128, 235, 128,
            0, 0, 0, 0,

            // Second row: white, black
            235, 128, 16, 128,
            0, 0, 0, 0
        };

        NorthStarImage image = CreateImage(
            2,
            2,
            8,
            NorthStarPixelFormat.YUY2,
            inputData);

        NorthStarImage result = _converter.Convert(
            image,
            NorthStarPixelFormat.BGR24);

        byte[] expected =
        {
            0, 0, 0,
            255, 255, 255,

            255, 255, 255,
            0, 0, 0
        };

        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(6, result.Stride);
        Assert.Equal(expected, result.Data.ToArray());
    }

    [Fact]
    public void Convert_Throws_WhenImageIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _converter.Convert(
                null!,
                NorthStarPixelFormat.BGR24));
    }

    [Fact]
    public void Convert_Throws_WhenSourceFormatIsUnsupported()
    {
        NorthStarImage image = CreateImage(
            2,
            1,
            4,
            NorthStarPixelFormat.Gray8,
            new byte[] { 1, 2, 3, 4 });

        Assert.Throws<InvalidOperationException>(() =>
            _converter.Convert(
                image,
                NorthStarPixelFormat.BGR24));
    }

    [Fact]
    public void Convert_Throws_WhenDestinationFormatIsUnsupported()
    {
        NorthStarImage image = CreateImage(
            2,
            1,
            4,
            NorthStarPixelFormat.YUY2,
            new byte[] { 16, 128, 235, 128 });

        Assert.Throws<InvalidOperationException>(() =>
            _converter.Convert(
                image,
                NorthStarPixelFormat.RGB24));
    }

    [Fact]
    public void Convert_Throws_WhenYUY2StrideIsTooSmall()
    {
        NorthStarImage image = CreateImage(
            2,
            1,
            3,
            NorthStarPixelFormat.YUY2,
            new byte[] { 16, 128, 235 });

        Assert.Throws<InvalidOperationException>(() =>
            _converter.Convert(
                image,
                NorthStarPixelFormat.BGR24));
    }

    [Fact]
    public void Convert_Throws_WhenImageDataIsTooShort()
    {
        NorthStarImage image = CreateImage(
            2,
            2,
            4,
            NorthStarPixelFormat.YUY2,
            new byte[] { 16, 128, 235, 128 });

        Assert.Throws<InvalidOperationException>(() =>
            _converter.Convert(
                image,
                NorthStarPixelFormat.BGR24));
    }

    [Fact]
    public void Convert_Throws_WhenImageWidthIsOdd()
    {
        NorthStarImage image = CreateImage(
            3,
            1,
            6,
            NorthStarPixelFormat.YUY2,
            new byte[] { 16, 128, 235, 128, 16, 128 });

        Assert.Throws<InvalidOperationException>(() =>
            _converter.Convert(
                image,
                NorthStarPixelFormat.BGR24));
    }

    private static NorthStarImage CreateImage(
        int width,
        int height,
        int stride,
        NorthStarPixelFormat pixelFormat,
        byte[] data)
    {
        return new NorthStarImage(
            width,
            height,
            stride,
            Guid.NewGuid(),
            pixelFormat,
            null,
            data);
    }
}
