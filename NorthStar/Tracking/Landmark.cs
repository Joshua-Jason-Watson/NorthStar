namespace NorthStar.Tracking
{
    // Represents a single pose keypoint detected by the model.
    public sealed class Landmark
    {
        // Identifies which keypoint this landmark represents.
        public int KeypointID { get; }

        // Horizontal position in the current coordinate space.
        public float X { get; }

        // Vertical position in the current coordinate space.
        public float Y { get; }

        // Confidence score assigned by the pose model.
        public float Confidence { get; }

        public Landmark(
            int keypointID,
            float x,
            float y,
            float confidence)
        {
            KeypointID = keypointID;
            X = x;
            Y = y;
            Confidence = confidence;
        }
    }
}