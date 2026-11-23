
using System;
using System.Threading;
using System.Threading.Tasks;

using NorthStar.Frames;

namespace NorthStar.Camera
{
    // Defines the common interface for a NorthStar camera.
    //
    // Camera acquisition is independent of frame consumption.
    // Implementations may continuously acquire and buffer frames
    // while consumers independently request them.
    public interface ICamera : IDisposable
    {
        // Asynchronously retrieves a captured video frame and
        // its associated camera acquisition measurements.
        //
        // Cancelling this operation cancels the consumer's request.
        // It does not stop the underlying camera acquisition.
        Task<(
            NorthStarFrame Frame,
            CameraCaptureMetrics CaptureMetrics)> GetFrameAsync(
                CancellationToken cancellationToken = default);

        // Width of the current camera stream.
        int Width { get; }

        // Height of the current camera stream.
        int Height { get; }

        // Configured frame rate of the current camera stream.
        double FPS { get; }
    }
}
