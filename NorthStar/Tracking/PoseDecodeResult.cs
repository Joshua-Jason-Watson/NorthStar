using System;
using System.Collections.Generic;

namespace NorthStar.Tracking
{
    // Contains the landmarks decoded from the model together with
    // the coordinate-space dimensions used by the model output.
    //
    // This is an intermediate processing result. It is not the
    // final public tracking result.
    public sealed class PoseDecodeResult
    {
        public IReadOnlyList<Landmark> Landmarks { get; }

        public int XResolution { get; }

        public int YResolution { get; }

        public PoseDecodeResult(
            IReadOnlyList<Landmark> landmarks,
            int xResolution,
            int yResolution)
        {
            if (landmarks == null)
            {
                throw new ArgumentNullException(
                    nameof(landmarks));
            }

            if (xResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(xResolution));
            }

            if (yResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(yResolution));
            }

            Landmarks =
                landmarks;

            XResolution =
                xResolution;

            YResolution =
                yResolution;
        }
    }
}