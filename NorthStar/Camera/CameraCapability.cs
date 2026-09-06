using System;

namespace NorthStar.Camera
{
    // Describes one native media type supported by a specific camera.
    //
    // A capability is an immutable description of a camera mode.
    // The exact Media Foundation frame-rate fraction is retained so
    // configuration does not have to rely on a rounded FPS value.
    public sealed class CameraCapability
    {
        // Index identifying this media type within the camera's
        // list of supported Media Foundation media types.
        public uint MediaTypeIndex { get; }

        // Horizontal resolution in pixels.
        public int Width { get; }

        // Vertical resolution in pixels.
        public int Height { get; }

        // Frame rate represented as a decimal value for convenient
        // display and comparison.
        public double FPS =>
            (double)FrameRateNumerator /
            FrameRateDenominator;

        // Exact Media Foundation frame-rate numerator.
        public uint FrameRateNumerator { get; }

        // Exact Media Foundation frame-rate denominator.
        public uint FrameRateDenominator { get; }

        // Media Foundation subtype identifying the native video format.
        public Guid Subtype { get; }

        public string DisplayName =>
            $"{Width} × {Height} @ {FPS:F2} FPS";

        public CameraCapability(
            uint mediaTypeIndex,
            int width,
            int height,
            uint frameRateNumerator,
            uint frameRateDenominator,
            Guid subtype)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height));
            }

            if (frameRateNumerator == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameRateNumerator));
            }

            if (frameRateDenominator == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameRateDenominator));
            }

            if (subtype == Guid.Empty)
            {
                throw new ArgumentException(
                    "Media Foundation subtype cannot be empty.",
                    nameof(subtype));
            }

            MediaTypeIndex =
                mediaTypeIndex;

            Width =
                width;

            Height =
                height;

            FrameRateNumerator =
                frameRateNumerator;

            FrameRateDenominator =
                frameRateDenominator;

            Subtype =
                subtype;
        }

        public override string ToString()
        {
            return
                $"{Width}x{Height} @ " +
                $"{FPS:F2} FPS - " +
                $"{Subtype}";
        }
    }
}