using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

using NorthStar.Frames;
using NorthStar.Tracking;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

namespace NorthStar.UI.Rendering
{
    internal sealed class PreviewRenderer
    {
        private readonly Image previewImage;
        private readonly Canvas trackingOverlay;

        private WriteableBitmap? previewBitmap;

        private byte[]? previewPixels;

        private readonly Dictionary<int, Ellipse> landmarkDots =
            new();


        public PreviewRenderer(
            Image previewImage,
            Canvas trackingOverlay)
        {
            this.previewImage =
                previewImage;

            this.trackingOverlay =
                trackingOverlay;
        }


        public void Initialize(
            int width,
            int height)
        {
            previewBitmap =
                new WriteableBitmap(
                    width,
                    height);

            previewPixels =
                new byte[
                    checked(
                        width *
                        height *
                        4)];

            previewImage.Source =
                previewBitmap;
        }


        public void Render(
            NorthStarImage image)
        {
            UpdatePreviewImage(
                image);

            ClearTrackingOverlay();
        }


        public void Render(
            NorthStarTrackingFrame trackingFrame)
        {
            UpdatePreviewImage(
                trackingFrame.Image);

            UpdateTrackingOverlay(
                trackingFrame);
        }


        public void Reset()
        {
            previewBitmap =
                null;

            previewPixels =
                null;

            previewImage.Source =
                null;

            ClearTrackingOverlay();

            foreach (
                Ellipse dot
                in landmarkDots.Values)
            {
                trackingOverlay.Children.Remove(
                    dot);
            }

            landmarkDots.Clear();
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


        private Ellipse GetLandmarkDot(
            int keypointID)
        {
            if (landmarkDots.TryGetValue(
                    keypointID,
                    out Ellipse? existingDot))
            {
                return existingDot;
            }

            Ellipse dot =
                new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill =
                        new SolidColorBrush(
                            Microsoft.UI.Colors.Red)
                };

            trackingOverlay.Children.Add(
                dot);

            landmarkDots.Add(
                keypointID,
                dot);

            return dot;
        }


        private void ClearTrackingOverlay()
        {
            foreach (
                Ellipse dot
                in landmarkDots.Values)
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

            ClearTrackingOverlay();

            double imageWidth =
                previewImage.ActualWidth;

            double imageHeight =
                previewImage.ActualHeight;

            if (imageWidth <= 0 ||
                imageHeight <= 0)
            {
                return;
            }

            double scaleX =
                imageWidth /
                image.Width;

            double scaleY =
                imageHeight /
                image.Height;

            double scale =
                Math.Min(
                    scaleX,
                    scaleY);

            double displayedWidth =
                image.Width *
                scale;

            double displayedHeight =
                image.Height *
                scale;

            double offsetX =
                (imageWidth -
                    displayedWidth) /
                2.0;

            double offsetY =
                (imageHeight -
                    displayedHeight) /
                2.0;

            foreach (
                Landmark landmark
                in pose.Landmarks)
            {
                Ellipse dot =
                    GetLandmarkDot(
                        landmark.KeypointID);

                double x =
                    offsetX +
                    (landmark.X *
                        scale);

                double y =
                    offsetY +
                    (landmark.Y *
                        scale);

                Canvas.SetLeft(
                    dot,
                    x -
                    dot.Width / 2);

                Canvas.SetTop(
                    dot,
                    y -
                    dot.Height / 2);

                dot.Visibility =
                    Visibility.Visible;
            }
        }
    }
}