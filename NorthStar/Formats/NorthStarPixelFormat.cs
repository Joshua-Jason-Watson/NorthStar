namespace NorthStar.Formats
{
    // Describes the layout of pixel data stored inside a NorthStarImage.
    //
    // This represents decoded image data, not compressed camera/container
    // formats such as MJPG.
    public enum NorthStarPixelFormat
    {
        // Single-channel 8-bit grayscale.
        Gray8,

        // Three 8-bit color channels.
        RGB24,

        // Three 8-bit color channels in BGR order.
        BGR24,

        // Four 8-bit channels in RGBA order.
        RGBA32,

        // Four 8-bit channels in BGRA order.
        BGRA32,

        // YUV 4:2:0 semi-planar format.
        NV12,

        // Packed YUV 4:2:2 format.
        YUY2
    }
}