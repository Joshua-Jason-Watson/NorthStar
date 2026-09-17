using NorthStar.Camera;

using System.Collections.Generic;

namespace NorthStar.UI.Camera
{
    internal sealed class CameraCapabilitySelector
    {
        public IReadOnlyList<(int Width, int Height)> GetResolutions(
            CameraDescriptor camera)
        {
            List<(int Width, int Height)> resolutions =
                new();

            HashSet<(int Width, int Height)> addedResolutions =
                new();

            foreach (
                CameraCapability capability
                in camera.Capabilities)
            {
                (int Width, int Height) resolution =
                    (
                        capability.Width,
                        capability.Height);

                if (!addedResolutions.Add(
                        resolution))
                {
                    continue;
                }

                resolutions.Add(
                    resolution);
            }

            return resolutions;
        }


        public IReadOnlyList<double> GetFrameRates(
            CameraDescriptor camera,
            int width,
            int height)
        {
            List<double> frameRates =
                new();

            HashSet<double> addedFrameRates =
                new();

            foreach (
                CameraCapability capability
                in camera.Capabilities)
            {
                if (capability.Width != width ||
                    capability.Height != height)
                {
                    continue;
                }

                if (!addedFrameRates.Add(
                        capability.FPS))
                {
                    continue;
                }

                frameRates.Add(
                    capability.FPS);
            }

            return frameRates;
        }


        public IReadOnlyList<CameraCapability> GetFormats(
            CameraDescriptor camera,
            int width,
            int height,
            double frameRate)
        {
            List<CameraCapability> formats =
                new();

            foreach (
                CameraCapability capability
                in camera.Capabilities)
            {
                if (capability.Width != width ||
                    capability.Height != height)
                {
                    continue;
                }

                if (capability.FPS != frameRate)
                {
                    continue;
                }

                formats.Add(
                    capability);
            }

            return formats;
        }
    }
}