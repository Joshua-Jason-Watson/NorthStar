
using System;
using System.Collections.Generic;

namespace NorthStar.Camera
{
    // Describes a camera discovered by NorthStar.
    //
    // A descriptor contains information about a camera but does not
    // open or own any Media Foundation resources.
    //
    // Its camera capabilities are captured as an immutable snapshot.
    public sealed class CameraDescriptor
    {
        // Human-readable camera name.
        public string Name { get; }

        // Stable identifier used to locate the camera again.
        public string Identifier { get; }

        // Read-only snapshot of the camera's supported media types.
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

            ArgumentNullException.ThrowIfNull(capabilities);

            // Copy the supplied capabilities so changes to the
            // original collection cannot affect this descriptor.
            CameraCapability[] snapshot =
                new CameraCapability[capabilities.Count];

            for (int i = 0; i < capabilities.Count; i++)
            {
                CameraCapability capability =
                    capabilities[i];

                if (capability == null)
                {
                    throw new ArgumentException(
                        "Camera capabilities cannot contain null entries.",
                        nameof(capabilities));
                }

                snapshot[i] = capability;
            }

            Name = name;

            Identifier = identifier;

            Capabilities =
                Array.AsReadOnly(snapshot);
        }
    }
}
