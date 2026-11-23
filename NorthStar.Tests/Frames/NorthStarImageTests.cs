
using NorthStar.Formats;
using NorthStar.Frames;

namespace NorthStar.Tests.Frames;

public sealed class NorthStarImageTests
{
    [Fact]
    public void Constructor_SetsProperties_WhenArgumentsAreValid()
    {
        Guid sourceFormat = Guid.NewGuid();

        byte[] data =
        {
            10, 20, 30, 40
        };

        NorthStarImage image = new(
            2,
            2,
            2,
            sourceFormat,
            NorthStarPixelFormat.Gray8,
            null,
            data);

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(2, image.Stride);
        Assert.Equal(sourceFormat, image.SourceFormat);
        Assert.Equal(NorthStarPixelFormat.Gray8, image.PixelFormat);
        Assert.Null(image.ColorSpace);
        Assert.Equal(data, image.Data.ToArray());
    }

    [Fact]
    public void Constructor_Throws_WhenWidthIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NorthStarImage(
                0, 1, 1, Guid.NewGuid(),
                NorthStarPixelFormat.Gray8,
                null,
                new byte[] { 1 }));
    }

    [Fact]
    public void Constructor_Throws_WhenHeightIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NorthStarImage(
                1, 0, 1, Guid.NewGuid(),
                NorthStarPixelFormat.Gray8,
                null,
                new byte[] { 1 }));
    }

    [Fact]
    public void Constructor_Throws_WhenStrideIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NorthStarImage(
                1, 1, 0, Guid.NewGuid(),
                NorthStarPixelFormat.Gray8,
                null,
                new byte[] { 1 }));
    }

    [Fact]
    public void Constructor_Throws_WhenStrideIsSmallerThanWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NorthStarImage(
                2, 1, 1, Guid.NewGuid(),
                NorthStarPixelFormat.Gray8,
                null,
                new byte[] { 1 }));
    }

    [Fact]
    public void Constructor_Throws_WhenSourceFormatIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new NorthStarImage(
                1, 1, 1, Guid.Empty,
                NorthStarPixelFormat.Gray8,
                null,
                new byte[] { 1 }));
    }

    [Fact]
    public void Constructor_Throws_WhenImageDataIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new NorthStarImage(
                1, 1, 1, Guid.NewGuid(),
                NorthStarPixelFormat.Gray8,
                null,
                Array.Empty<byte>()));
    }
}
