using NorthStar.Camera;

using System;
using System.Collections.Generic;

namespace NorthStar.UI.Camera
{
    internal sealed class CameraCapabilitySelector
    {
        public IReadOnlyList<(int Width, int Height)> GetResolutions(
            CameraDescriptor camera)
        {
            HashSet<(int Width, int Height)> resolutions =
                new();

            foreach (
                CameraCapability capability
                in camera.Capabilities)
            {
                resolutions.Add(
                    (
                        capability.Width,
                        capability.Height));
            }

            return new List<(int Width, int Height)>(
                resolutions);
        }


        public IReadOnlyList<double> GetFrameRates(
            CameraDescriptor camera,
            int width,
            int height)
        {
            HashSet<double> frameRates =
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

                frameRates.Add(
                    capability.FPS);
            }

            return new List<double>(
                frameRates);
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