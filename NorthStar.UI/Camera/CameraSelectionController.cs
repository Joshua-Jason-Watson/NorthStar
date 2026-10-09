using Microsoft.UI.Xaml.Controls;

using NorthStar.Camera;

using System;
using System.Collections.Generic;

namespace NorthStar.UI.Camera
{
    // Owns the camera / resolution / frame-rate / format combo boxes
    // and keeps the currently selected capability in sync.
    internal sealed class CameraSelectionController
    {
        private readonly ComboBox cameraSelector;
        private readonly ComboBox resolutionSelector;
        private readonly ComboBox frameRateSelector;
        private readonly ComboBox formatSelector;

        private readonly WindowsCameraDiscovery cameraDiscovery;
        private readonly CameraCapabilitySelector capabilitySelector;


        public CameraSelectionController(
            ComboBox cameraSelector,
            ComboBox resolutionSelector,
            ComboBox frameRateSelector,
            ComboBox formatSelector)
        {
            this.cameraSelector =
                cameraSelector;

            this.resolutionSelector =
                resolutionSelector;

            this.frameRateSelector =
                frameRateSelector;

            this.formatSelector =
                formatSelector;

            cameraDiscovery =
                new WindowsCameraDiscovery();

            capabilitySelector =
                new CameraCapabilitySelector();

            cameraSelector.SelectionChanged +=
                CameraSelector_SelectionChanged;

            resolutionSelector.SelectionChanged +=
                ResolutionSelector_SelectionChanged;

            frameRateSelector.SelectionChanged +=
                FrameRateSelector_SelectionChanged;

            formatSelector.SelectionChanged +=
                FormatSelector_SelectionChanged;
        }


        public CameraDescriptor? SelectedCamera =>
            cameraSelector.SelectedItem as CameraDescriptor;

        public CameraCapability? SelectedCapability { get; private set; }


        public event EventHandler? CameraChanged;


        public void LoadCameras()
        {
            cameraSelector.Items.Clear();

            IReadOnlyList<CameraDescriptor> cameras =
                cameraDiscovery.Discover();

            foreach (
                CameraDescriptor camera
                in cameras)
            {
                cameraSelector.Items.Add(
                    camera);
            }
        }


        private void CameraSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            CameraChanged?.Invoke(
                this,
                EventArgs.Empty);

            resolutionSelector.Items.Clear();
            frameRateSelector.Items.Clear();
            formatSelector.Items.Clear();

            SelectedCapability =
                null;

            if (SelectedCamera
                is not CameraDescriptor camera)
            {
                return;
            }

            IReadOnlyList<(int Width, int Height)> resolutions =
                capabilitySelector.GetResolutions(
                    camera);

            foreach (
                (int Width, int Height) resolution
                in resolutions)
            {
                resolutionSelector.Items.Add(
                    new ResolutionOption(
                        resolution.Width,
                        resolution.Height));
            }
        }

        private void ResolutionSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            frameRateSelector.Items.Clear();
            formatSelector.Items.Clear();

            SelectedCapability =
                null;

            if (SelectedCamera
                is not CameraDescriptor camera)
            {
                return;
            }

            if (resolutionSelector.SelectedItem
                is not ResolutionOption resolution)
            {
                return;
            }

            IReadOnlyList<double> frameRates =
                capabilitySelector.GetFrameRates(
                    camera,
                    resolution.Width,
                    resolution.Height);

            foreach (
                double frameRate
                in frameRates)
            {
                frameRateSelector.Items.Add(
                    frameRate);
            }
        }

        private void FrameRateSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            formatSelector.Items.Clear();

            SelectedCapability =
                null;

            if (SelectedCamera
                is not CameraDescriptor camera)
            {
                return;
            }

            if (resolutionSelector.SelectedItem
                is not ResolutionOption resolution)
            {
                return;
            }

            if (frameRateSelector.SelectedItem
                is not double frameRate)
            {
                return;
            }

            IReadOnlyList<CameraCapability> formats =
                capabilitySelector.GetFormats(
                    camera,
                    resolution.Width,
                    resolution.Height,
                    frameRate);

            foreach (
                CameraCapability capability
                in formats)
            {
                formatSelector.Items.Add(
                    new FormatOption(
                        capability));
            }
        }

        private void FormatSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (formatSelector.SelectedItem
                is not FormatOption format)
            {
                SelectedCapability =
                    null;

                return;
            }

            SelectedCapability =
                format.Capability;
        }
    }
}
