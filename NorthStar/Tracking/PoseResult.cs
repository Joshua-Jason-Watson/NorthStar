using System;
using System.Collections.Generic;

namespace NorthStar.Tracking
{
    // Contains the pose landmarks detected for a single frame.
    //
    // Once constructed, the result cannot be modified.
    public sealed class PoseResult
    {
        private readonly IReadOnlyList<Landmark> landmarks;

        public IReadOnlyList<Landmark> Landmarks =>
            landmarks;

        public PoseResult(
            IReadOnlyList<Landmark> landmarks)
        {
            if (landmarks == null)
            {
                throw new ArgumentNullException(
                    nameof(landmarks));
            }

            this.landmarks =
                landmarks;
        }
    }
}