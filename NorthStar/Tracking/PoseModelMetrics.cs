namespace NorthStar.Tracking
{
    // Contains timing information for one pose-model frame.
    //
    // All values are measured in milliseconds.
    public readonly struct PoseModelMetrics
    {
        public double PreprocessingMilliseconds { get; }

        public double InferenceMilliseconds { get; }

        public double PoseDecodeMilliseconds { get; }

        public double CoordinateTransformMilliseconds { get; }

        public double TotalMilliseconds { get; }

        public PoseModelMetrics(
            double preprocessingMilliseconds,
            double inferenceMilliseconds,
            double poseDecodeMilliseconds,
            double coordinateTransformMilliseconds,
            double totalMilliseconds)
        {
            PreprocessingMilliseconds =
                preprocessingMilliseconds;

            InferenceMilliseconds =
                inferenceMilliseconds;

            PoseDecodeMilliseconds =
                poseDecodeMilliseconds;

            CoordinateTransformMilliseconds =
                coordinateTransformMilliseconds;

            TotalMilliseconds =
                totalMilliseconds;
        }
    }
}