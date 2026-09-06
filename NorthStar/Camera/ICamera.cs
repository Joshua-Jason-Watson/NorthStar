using NorthStar.Frames;

namespace NorthStar.Camera
{
    // Defines the common interface for a NorthStar camera.
    //
    // The interface describes camera functionality without exposing
    // the underlying camera API or implementation.
    public interface ICamera : IDisposable
    {
        // Captures and returns the next video frame.
        NorthStarFrame GetFrame();

        // Width of the current camera stream.
        int Width { get; }

        // Height of the current camera stream.
        int Height { get; }

        // Frame rate associated with the current camera stream.
        double FPS { get; }
    }
}