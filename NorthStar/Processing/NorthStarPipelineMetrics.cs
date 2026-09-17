namespace NorthStar.Processing
{
    // Contains timing information for one pipeline frame.
    //
    // All values are measured in milliseconds.
    public readonly struct NorthStarPipelineMetrics
    {
        public double CaptureMilliseconds { get; }

        public double DecodeMilliseconds { get; }

        public double ConversionMilliseconds { get; }

        public double TotalMilliseconds { get; }

        public NorthStarPipelineMetrics(
            double captureMilliseconds,
            double decodeMilliseconds,
            double conversionMilliseconds,
            double totalMilliseconds)
        {
            CaptureMilliseconds =
                captureMilliseconds;

            DecodeMilliseconds =
                decodeMilliseconds;

            ConversionMilliseconds =
                conversionMilliseconds;

            TotalMilliseconds =
                totalMilliseconds;
        }
    }
}