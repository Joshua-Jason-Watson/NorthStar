using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NorthStar.Camera;
using NorthStar.Conversion;
using NorthStar.Decoding;
using NorthStar.Formats;
using NorthStar.Frames;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

namespace NorthStar.UI
{
    public sealed partial class MainWindow : Window
    {
        private readonly WindowsCameraDiscovery cameraDiscovery;

        private readonly FrameDecoderRegistry previewDecoderRegistry =
            new FrameDecoderRegistry();

        private readonly ImageConverterRegistry previewConverterRegistry =
            new ImageConverterRegistry();

        private readonly DispatcherQueueTimer previewDisplayTimer;

        private IReadOnlyList<CameraDescriptor> cameras =
            Array.Empty<CameraDescriptor>();

        private CameraCapability? selectedCapability;

        private CameraDevice? previewCamera;

        private CancellationTokenSource? previewCts;

        private Task? previewTask;

        private WriteableBitmap? previewBitmap;

        private byte[]? previewPixels;

        // The preview worker publishes the newest completed image here.
        //
        // There is intentionally only one pending image. If the UI has
        // not displayed an older image yet, a newer image replaces it.
        private NorthStarImage? latestPreviewImage;

        private bool closed;

        public MainWindow()
        {
            InitializeComponent();

            cameraDiscovery =
                new WindowsCameraDiscovery();

            previewDecoderRegistry.Register(
                new NV12Decoder());

            previewConverterRegistry.Register(
                new NV12Converter());

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
            StopPreview();

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

            StopPreview();

            CameraCapability capability =
                selectedCapability;

            CameraDevice newCamera =
                new WindowsCameraDeviceFactory()
                    .Open(camera);

            CancellationTokenSource? newCts =
                null;

            try
            {
                newCamera.Configure(
                    capability);

                WriteableBitmap newBitmap =
                    new WriteableBitmap(
                        capability.Width,
                        capability.Height);

                byte[] newPixels =
                    new byte[
                        checked(
                            capability.Width *
                            capability.Height *
                            4)];

                newCts =
                    new CancellationTokenSource();

                // These are the actual objects the worker will use.
                //
                // Keeping these local variables separate means we can safely
                // transfer ownership to the MainWindow fields without changing
                // what the worker's closure references.
                CameraDevice workerCamera =
                    newCamera;

                CancellationTokenSource workerCts =
                    newCts;

                previewCamera =
                    newCamera;

                previewBitmap =
                    newBitmap;

                previewPixels =
                    newPixels;

                previewCts =
                    newCts;

                PreviewImage.Source =
                    newBitmap;

                previewDisplayTimer.Start();

                previewTask =
                    Task.Run(
                        () => PreviewLoop(
                            workerCamera,
                            workerCts.Token));

                // Ownership has now been transferred to the MainWindow fields
                // and the worker task.
                newCamera =
                    null!;

                newCts =
                    null;
            }
            catch
            {
                newCts?.Cancel();
                newCts?.Dispose();

                newCamera.Dispose();

                throw;
            }
        }


        private void PreviewLoop(
            CameraDevice camera,
            CancellationToken cancellationToken)
        {
            try
            {
                while (
                    !cancellationToken.IsCancellationRequested)
                {
                    NorthStarFrame frame =
                        camera.GetFrame();

                    IFrameDecoder decoder =
                        previewDecoderRegistry.GetDecoder(
                            frame.Subtype);

                    NorthStarImage image =
                        decoder.Decode(
                            frame);

                    if (image.PixelFormat !=
                        NorthStarPixelFormat.BGR24)
                    {
                        IImageConverter converter =
                            previewConverterRegistry.GetConverter(
                                image.PixelFormat,
                                NorthStarPixelFormat.BGR24);

                        image =
                            converter.Convert(
                                image,
                                NorthStarPixelFormat.BGR24);
                    }

                    // Publish this frame as the newest available frame.
                    //
                    // An older frame may be replaced here. That is
                    // intentional: stale preview frames have no value.
                    Interlocked.Exchange(
                        ref latestPreviewImage,
                        image);
                }
            }
            catch
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                DispatcherQueue.TryEnqueue(
                    () =>
                    {
                        if (closed)
                        {
                            return;
                        }

                        StopPreview();
                    });
            }
        }

        private void PreviewDisplayTimer_Tick(
            DispatcherQueueTimer sender,
            object args)
        {
            if (closed)
            {
                return;
            }

            NorthStarImage? image =
                Interlocked.Exchange(
                    ref latestPreviewImage,
                    null);

            if (image == null)
            {
                return;
            }

            UpdatePreviewImage(
                image);
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

            for (int y = 0;
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

                for (int x = 0;
                     x < width;
                     x++)
                {
                    int sourcePixel =
                        sourceOffset +
                        (x * 3);

                    int destinationPixel =
                        destinationOffset +
                        (x * 4);

                    pixels[
                        destinationPixel] =
                        source[
                            sourcePixel];

                    pixels[
                        destinationPixel + 1] =
                        source[
                            sourcePixel + 1];

                    pixels[
                        destinationPixel + 2] =
                        source[
                            sourcePixel + 2];

                    pixels[
                        destinationPixel + 3] =
                        255;
                }
            }

            using Stream stream =
                bitmap.PixelBuffer
                    .AsStream();

            stream.Position =
                0;

            stream.Write(
                pixels,
                0,
                requiredSize);

            bitmap.Invalidate();
        }

        private void StopPreview()
        {
            previewDisplayTimer.Stop();

            CancellationTokenSource? cts =
                previewCts;

            Task? task =
                previewTask;

            previewCts =
                null;

            previewTask =
                null;

            if (cts != null)
            {
                cts.Cancel();
            }

            // The capture worker owns the camera while it is running.
            //
            // Wait for it to finish before disposing the camera so that
            // the worker can never be using CameraDevice after disposal.
            if (task != null &&
                !task.IsCompleted)
            {
                try
                {
                    task.Wait(
                        TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                    // The worker's exception has already been handled
                    // by PreviewLoop. Shutdown should continue.
                }
            }

            cts?.Dispose();

            CameraDevice? camera =
                previewCamera;

            previewCamera =
                null;

            camera?.Dispose();

            // Remove any frame that was waiting for the UI.
            Interlocked.Exchange(
                ref latestPreviewImage,
                null);

            previewBitmap =
                null;

            previewPixels =
                null;

            PreviewImage.Source =
                null;
        }

        private void MainWindow_Closed(
            object sender,
            WindowEventArgs args)
        {
            closed =
                true;

            StopPreview();

            previewDisplayTimer.Tick -=
                PreviewDisplayTimer_Tick;
        }
    }
}
