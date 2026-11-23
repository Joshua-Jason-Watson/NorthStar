
using NorthStar.Frames;

using System;

namespace NorthStar.Processing
{
    // Transforms model-output coordinates back into the original
    // camera frame's coordinate space.
    //
    // Accounts for model input dimensions, SimCC output resolution,
    // preprocessing scale and padding.
    public sealed class CoordinateTransformer
    {
        // ============================================================
        // Coordinate transformation
        // ============================================================

        public NorthStarPoint Transform(
            NorthStarPoint simccPoint,
            int simccXResolution,
            int simccYResolution,
            int modelWidth,
            int modelHeight,
            PreprocessResult preprocessResult)
        {
            ArgumentNullException.ThrowIfNull(preprocessResult);


            // ========================================================
            // Validate model dimensions
            // ========================================================

            if (simccXResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simccXResolution));
            }

            if (simccYResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simccYResolution));
            }

            if (modelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modelWidth));
            }

            if (modelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modelHeight));
            }


            // ========================================================
            // Validate preprocessing parameters
            // ========================================================

            double scale =
                preprocessResult.Scale;

            double paddingX =
                preprocessResult.PaddingX;

            double paddingY =
                preprocessResult.PaddingY;

            if (!double.IsFinite(scale) || scale <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preprocessResult),
                    "Preprocessing scale must be finite and greater than zero.");
            }

            if (!double.IsFinite(paddingX) ||
                !double.IsFinite(paddingY))
            {
                throw new ArgumentException(
                    "Preprocessing padding must contain finite values.",
                    nameof(preprocessResult));
            }


            // ========================================================
            // Validate SimCC coordinates
            // ========================================================

            if (simccPoint.X < 0 ||
                simccPoint.X >= simccXResolution ||
                simccPoint.Y < 0 ||
                simccPoint.Y >= simccYResolution)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simccPoint),
                    "SimCC point lies outside the SimCC coordinate space.");
            }


            // ========================================================
            // SimCC to model coordinates
            // ========================================================

            // SimCC represents the model's spatial coordinates
            // at a higher resolution than the model input.
            //
            // Convert its coordinates back into model-input pixels.

            double modelX =
                (double)simccPoint.X *
                modelWidth /
                simccXResolution;

            double modelY =
                (double)simccPoint.Y *
                modelHeight /
                simccYResolution;


            // ========================================================
            // Remove preprocessing padding
            // ========================================================

            double unpaddedX =
                modelX - paddingX;

            double unpaddedY =
                modelY - paddingY;


            // ========================================================
            // Restore original image coordinates
            // ========================================================

            double originalX =
                unpaddedX / scale;

            double originalY =
                unpaddedY / scale;


            // ========================================================
            // Convert to integer pixel coordinates
            // ========================================================

            if (!double.IsFinite(originalX) ||
                !double.IsFinite(originalY))
            {
                throw new InvalidOperationException(
                    "Coordinate transformation produced a non-finite result.");
            }

            double roundedX =
                Math.Round(originalX);

            double roundedY =
                Math.Round(originalY);

            if (roundedX < int.MinValue ||
                roundedX > int.MaxValue ||
                roundedY < int.MinValue ||
                roundedY > int.MaxValue)
            {
                throw new OverflowException(
                    "Transformed coordinates exceed the supported integer range.");
            }

            return new NorthStarPoint(
                (int)roundedX,
                (int)roundedY);
        }
    }
}
