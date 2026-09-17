using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NorthStar.Camera;
using NorthStar.Frames;
using NorthStar.Tracking;
using NorthStar.UI.Rendering;

using System;
using System.Collections.Generic;
using IOPath = System.IO.Path;

namespace NorthStar.UI
{
    public sealed partial class MainWindow : Window
    {
        // ============================================================
        // NorthStar runtime
        // ============================================================

        private NorthStarRuntime? runtime;


        // ============================================================
        // Camera discovery and capability selection
        // ============================================================

        private readonly WindowsCameraDiscovery cameraDiscovery;

        private IReadOnlyList<CameraDescriptor> cameras =
            Array.Empty<CameraDescriptor>();

        private CameraCapability? selectedCapability;


        // ============================================================
        // Preview display
        // ============================================================

        private readonly DispatcherQueueTimer previewDisplayTimer;

        private readonly PreviewRenderer previewRenderer;


        // ============================================================
        // Window state
        // ============================================================

        private bool closed;


        // ============================================================
        // Constructor
        // ============================================================

        public MainWindow()
        {
            InitializeComponent();

            previewRenderer =
                new PreviewRenderer(
                    PreviewImage,
                    TrackingOverlay);

            cameraDiscovery =
                new WindowsCameraDiscovery();

            previewDisplayTimer =
                DispatcherQueue.CreateTimer();

            previewDisplayTimer.Interval =
                TimeSpan.FromMilliseconds(16);

            previewDisplayTimer.IsRepeating =
                true;

            previewDisplayTimer.Tick +=
                PreviewDisplayTimer_Tick;

            LoadCameras();

            Closed +=
                MainWindow_Closed;
        }


        // ============================================================
        // Camera selection
        // ============================================================

        private void LoadCameras()
        {
            cameras =
                cameraDiscovery.Discover();

            CameraSelector.Items.Clear();

            foreach (
                CameraDescriptor camera
                in cameras)
            {
                CameraSelector.Items.Add(
                    camera);
            }
        }

        private void CameraSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            StopRuntime();

            ResolutionSelector.Items.Clear();
            FrameRateSelector.Items.Clear();
            FormatSelector.Items.Clear();

            selectedCapability =
                null;

            if (CameraSelector.SelectedItem
                is not CameraDescriptor camera)
            {
                return;
            }

            HashSet<string> addedResolutions =
                new();

            foreach (
                CameraCapability capability
                in camera.Capabilities)
            {
                string key =
                    $"{capability.Width}x{capability.Height}";

                if (!addedResolutions.Add(key))
                {
                    continue;
                }

                ResolutionSelector.Items.Add(
                    new ResolutionOption(
                        capability.Width,
                        capability.Height));
            }
        }

        private void ResolutionSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            FrameRateSelector.Items.Clear();
            FormatSelector.Items.Clear();

            selectedCapability =
                null;

            if (CameraSelector.SelectedItem
                is not CameraDescriptor camera)
            {
                return;
            }

            if (ResolutionSelector.SelectedItem
                is not ResolutionOption resolution)
            {
                return;
            }

            HashSet<double> addedFrameRates =
                new();

            foreach (
                CameraCapability capability
                in camera.Capabilities)
            {
                if (capability.Width !=
                        resolution.Width ||
                    capability.Height !=
                        resolution.Height)
                {
                    continue;
                }

                if (!addedFrameRates.Add(
                        capability.FPS))
                {
                    continue;
                }

                FrameRateSelector.Items.Add(
                    capability.FPS);
            }
        }

        private void FrameRateSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            FormatSelector.Items.Clear();

            selectedCapability =
                null;

            if (CameraSelector.SelectedItem
                is not CameraDescriptor camera)
            {
                return;
            }

            if (ResolutionSelector.SelectedItem
                is not ResolutionOption resolution)
            {
                return;
            }

            if (FrameRateSelector.SelectedItem
                is not double frameRate)
            {
                return;
            }

            foreach (
                CameraCapability capability
                in camera.Capabilities)
            {
                if (capability.Width !=
                        resolution.Width ||
                    capability.Height !=
                        resolution.Height)
                {
                    continue;
                }

                if (capability.FPS !=
                    frameRate)
                {
                    continue;
                }

                FormatSelector.Items.Add(
                    new FormatOption(
                        capability));
            }
        }

        private void FormatSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (FormatSelector.SelectedItem
                is not FormatOption format)
            {
                selectedCapability =
                    null;

                return;
            }

            selectedCapability =
                format.Capability;
        }


        // ============================================================
        // Camera capability display helpers
        // ============================================================

        private sealed class ResolutionOption
        {
            public int Width { get; }

            public int Height { get; }

            public string DisplayName =>
                $"{Width} × {Height}";

            public ResolutionOption(
                int width,
                int height)
            {
                Width =
                    width;

                Height =
                    height;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class FormatOption
        {
            public CameraCapability Capability { get; }

            public string DisplayName =>
                GetFormatName(
                    Capability.Subtype);

            public FormatOption(
                CameraCapability capability)
            {
                Capability =
                    capability;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private static string GetFormatName(
            Guid subtype)
        {
            if (subtype ==
                new Guid(
                    "3231564E-0000-0010-8000-00AA00389B71"))
            {
                return "NV12";
            }

            if (subtype ==
                new Guid(
                    "47504A4D-0000-0010-8000-00AA00389B71"))
            {
                return "MJPG";
            }

            if (subtype ==
                new Guid(
                    "32595559-0000-0010-8000-00AA00389B71"))
            {
                return "YUY2";
            }

            return subtype.ToString();
        }


        // ============================================================
        // Runtime lifecycle
        // ============================================================

        private void StartPreviewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (CameraSelector.SelectedItem
                is not CameraDescriptor camera)
            {
                return;
            }

            if (selectedCapability == null)
            {
                return;
            }

            StopRuntime();

            CameraCapability capability =
                selectedCapability;

            NorthStarRuntime newRuntime =
                new NorthStarRuntime(
                    GetModelPath());

            try
            {
                newRuntime.Start(
                    camera,
                    capability);

                runtime =
                    newRuntime;

                previewRenderer.Initialize(
                    capability.Width,
                    capability.Height);

                previewDisplayTimer.Start();
            }
            catch
            {
                newRuntime.Dispose();

                throw;
            }
        }

        private void StopRuntime()
        {
            previewDisplayTimer.Stop();

            NorthStarRuntime? currentRuntime =
                runtime;

            runtime =
                null;

            currentRuntime?.Dispose();

            previewRenderer.Reset();
        }


        // ============================================================
        // Preview display
        // ============================================================

        private void PreviewDisplayTimer_Tick(
            DispatcherQueueTimer sender,
            object args)
        {
            if (closed)
            {
                return;
            }

            NorthStarRuntime? currentRuntime =
                runtime;

            if (currentRuntime == null)
            {
                return;
            }

            NorthStarTrackingFrame? trackingFrame =
                currentRuntime.LatestFrame;

            if (trackingFrame != null)
            {
                previewRenderer.Render(
                    trackingFrame);

                return;
            }

            NorthStarImage? image =
                currentRuntime.LatestImage;

            if (image == null)
            {
                return;
            }

            previewRenderer.Render(
                image);
        }
            
        // ============================================================
        // Tracking
        // ============================================================

        private void StartTrackingButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (runtime == null ||
                !runtime.IsRunning)
            {
                return;
            }

            if (runtime.IsTracking)
            {
                return;
            }

            runtime.StartTracking();
        }


        // ============================================================
        // Model configuration
        // ============================================================

        private string GetModelPath()
        {
            return IOPath.Combine(
                        AppContext.BaseDirectory,
                        "Models",
                        "rtmpose-m.onnx");
        }


        // ============================================================
        // Window shutdown
        // ============================================================

        private void MainWindow_Closed(
            object sender,
            WindowEventArgs args)
        {
            closed =
                true;

            StopRuntime();

            previewDisplayTimer.Tick -=
                PreviewDisplayTimer_Tick;
        }
    }
}