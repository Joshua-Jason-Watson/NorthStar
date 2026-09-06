using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

using NorthStar.Camera;
using NorthStar.Frames;
using NorthStar.Tracking;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;

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

        private WriteableBitmap? previewBitmap;

        private byte[]? previewPixels;

        private readonly List<Ellipse> landmarkDots =
            new();


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

                previewBitmap =
                    new WriteableBitmap(
                        capability.Width,
                        capability.Height);

                previewPixels =
                    new byte[
                        checked(
                            capability.Width *
                            capability.Height *
                            4)];

                PreviewImage.Source =
                    previewBitmap;

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

            previewBitmap =
                null;

            previewPixels =
                null;

            PreviewImage.Source =
                null;
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
                UpdatePreviewImage(
                    trackingFrame.Image);

                UpdateTrackingOverlay(
                    trackingFrame);

                return;
            }

            NorthStarImage? image =
                currentRuntime.LatestImage;

            if (image == null)
            {
                return;
            }

            UpdatePreviewImage(
                image);

            ClearTrackingOverlay();
        }

        private void UpdatePreviewImage(
            NorthStarImage image)
        {
            WriteableBitmap? bitmap =
                previewBitmap;

            byte[]? pixels =
                previewPixels;

            if (bitmap == null ||
                pixels == null)
            {
                return;
            }

            if (bitmap.PixelWidth !=
                    image.Width ||
                bitmap.PixelHeight !=
                    image.Height)
            {
                return;
            }

            int requiredSize =
                checked(
                    image.Width *
                    image.Height *
                    4);

            if (pixels.Length <
                requiredSize)
            {
                return;
            }

            ReadOnlySpan<byte> source =
                image.Data.Span;

            int sourceStride =
                image.Stride;

            int width =
                image.Width;

            int height =
                image.Height;

            for (
                int y = 0;
                y < height;
                y++)
            {
                int sourceOffset =
                    y *
                    sourceStride;

                int destinationOffset =
                    y *
                    width *
                    4;

                for (
                    int x = 0;
                    x < width;
                    x++)
                {
                    int sourcePixel =
                        sourceOffset +
                        (x * 3);

                    int destinationPixel =
                        destinationOffset +
                        (x * 4);

                    pixels[destinationPixel] =
                        source[sourcePixel];

                    pixels[destinationPixel + 1] =
                        source[sourcePixel + 1];

                    pixels[destinationPixel + 2] =
                        source[sourcePixel + 2];

                    pixels[destinationPixel + 3] =
                        255;
                }
            }

            using Stream stream =
                bitmap.PixelBuffer.AsStream();

            stream.Position =
                0;

            stream.Write(
                pixels,
                0,
                requiredSize);

            bitmap.Invalidate();
        }

        private void EnsureLandmarkDots(
            int count)
        {
            while (landmarkDots.Count < count)
            {
                Ellipse dot =
                    new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill =
                            new SolidColorBrush(
                                Microsoft.UI.Colors.Red)
                    };

                TrackingOverlay.Children.Add(
                    dot);

                landmarkDots.Add(
                    dot);
            }

            while (landmarkDots.Count > count)
            {
                Ellipse dot =
                    landmarkDots[^1];

                TrackingOverlay.Children.Remove(
                    dot);

                landmarkDots.RemoveAt(
                    landmarkDots.Count - 1);
            }
        }

        private void ClearTrackingOverlay()
        {
            foreach (
                Ellipse dot
                in landmarkDots)
            {
                dot.Visibility =
                    Visibility.Collapsed;
            }
        }

        private void UpdateTrackingOverlay(
            NorthStarTrackingFrame trackingFrame)
        {
            PoseResult pose =
                trackingFrame.Pose;

            NorthStarImage image =
                trackingFrame.Image;

            EnsureLandmarkDots(
                pose.Landmarks.Count);

            double imageWidth =
                PreviewImage.ActualWidth;

            double imageHeight =
                PreviewImage.ActualHeight;

            if (imageWidth <= 0 ||
                imageHeight <= 0)
            {
                return;
            }

            double scaleX =
                imageWidth / image.Width;

            double scaleY =
                imageHeight / image.Height;

            double scale =
                Math.Min(
                    scaleX,
                    scaleY);

            double displayedWidth =
                image.Width * scale;

            double displayedHeight =
                image.Height * scale;

            double offsetX =
                (imageWidth - displayedWidth) / 2.0;

            double offsetY =
                (imageHeight - displayedHeight) / 2.0;

            for (
                int i = 0;
                i < pose.Landmarks.Count;
                i++)
            {
                Landmark landmark =
                    pose.Landmarks[i];

                Ellipse dot =
                    landmarkDots[i];

                double x =
                    offsetX +
                    (landmark.X * scale);

                double y =
                    offsetY +
                    (landmark.Y * scale);

                Canvas.SetLeft(
                    dot,
                    x - dot.Width / 2);

                Canvas.SetTop(
                    dot,
                    y - dot.Height / 2);

                dot.Visibility =
                    Visibility.Visible;
            }
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