
using System;

using NorthStar.Frames;

namespace NorthStar.Tracking
{
    // Represents the complete processing result associated
    // with one camera frame.
    //
    // The image and pose results must correspond to the same
    // source frame so consumers can visualize and interpret
    // the results without losing frame synchronization.
    public sealed class NorthStarTrackingFrame
    {
        public NorthStarImage Image { get; }

        public PoseResult Pose { get; }

        public NorthStarTrackingFrame(
            NorthStarImage image,
            PoseResult pose)
        {
            Image =
                image ?? throw new ArgumentNullException(nameof(image));

            Pose =
                pose ?? throw new ArgumentNullException(nameof(pose));
        }
    }
}
