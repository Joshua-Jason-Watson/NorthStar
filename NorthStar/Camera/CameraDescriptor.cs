using System;
using System.Collections.Generic;

namespace NorthStar.Camera
{
    // Describes a camera discovered by NorthStar.
    //
    // A descriptor contains information about a camera but does not
    // open or own any Media Foundation resources.
    //
    // Once constructed, the descriptor is immutable.
    public sealed class CameraDescriptor
    {
        // Human-readable camera name.
        public string Name { get; }

        // Stable identifier used to locate the camera again.
        public string Identifier { get; }

        // Native media types supported by this camera.
        public IReadOnlyList<CameraCapability> Capabilities { get; }

        public CameraDescriptor(
            string name,
            string identifier,
            IReadOnlyList<CameraCapability> capabilities)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Camera name cannot be empty.",
                    nameof(name));
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException(
                    "Camera identifier cannot be empty.",
                    nameof(identifier));
            }

            if (capabilities == null)
            {
                throw new ArgumentNullException(
                    nameof(capabilities));
            }

            Name =
                name;

            Identifier =
                identifier;

            Capabilities =
                new List<CameraCapability>(
                    capabilities);
        }
    }
}