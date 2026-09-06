using NorthStar.Frames;

namespace NorthStar.Tracking
{
    // Represents the complete result of processing one camera frame.
    // It keeps the image and the corresponding tracking result together
    // so visualization and future consumers can process the same frame.
    public sealed class NorthStarTrackingFrame
    {
        public NorthStarImage Image { get; }

        public PoseResult Pose { get; }

        public NorthStarTrackingFrame(
            NorthStarImage image,
            PoseResult pose)
        {
            Image = image;
            Pose = pose;
        }
    }
}