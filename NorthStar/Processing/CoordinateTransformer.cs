using NorthStar.Frames;
using System;

namespace NorthStar.Processing
{
    // Converts landmark coordinates from the model's SimCC coordinate
    // space back into the coordinate space of the original camera frame.
    public sealed class CoordinateTransformer
    {
        public NorthStarPoint Transform(
            NorthStarPoint simccPoint,
            int simccXResolution,
            int simccYResolution,
            int modelWidth,
            int modelHeight,
            PreprocessResult preprocessResult)
        {
            if (preprocessResult == null)
            {
                throw new ArgumentNullException(
                    nameof(preprocessResult));
            }

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

            if (preprocessResult.Scale <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preprocessResult),
                    "Preprocessing scale must be greater than zero.");
            }

            if (simccPoint.X < 0 ||
                simccPoint.X >= simccXResolution ||
                simccPoint.Y < 0 ||
                simccPoint.Y >= simccYResolution)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simccPoint),
                    "SimCC point lies outside the SimCC coordinate space.");
            }

            float modelX =
                simccPoint.X *
                ((float)modelWidth /
                 simccXResolution);

            float modelY =
                simccPoint.Y *
                ((float)modelHeight /
                 simccYResolution);

            float unpaddedX =
                modelX -
                preprocessResult.PaddingX;

            float unpaddedY =
                modelY -
                preprocessResult.PaddingY;

            float originalX =
                unpaddedX /
                preprocessResult.Scale;

            float originalY =
                unpaddedY /
                preprocessResult.Scale;

            return new NorthStarPoint(
                (int)Math.Round(originalX),
                (int)Math.Round(originalY));
        }
    }
}