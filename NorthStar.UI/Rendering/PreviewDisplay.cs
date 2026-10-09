using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

using NorthStar;
using NorthStar.Frames;
using NorthStar.Tracking;

using System;
using System.Diagnostics;

namespace NorthStar.UI.Rendering
{
    // Displays the latest runtime image on a timer and reports
    // how many distinct frames were shown each second.
    internal sealed class PreviewDisplay : IDisposable
    {
        private readonly NorthStarRuntime runtime;

        private readonly DispatcherQueueTimer previewDisplayTimer;

        private readonly PreviewRenderer previewRenderer;

        private readonly TextBlock fpsText;

        private readonly Stopwatch fpsStopwatch;

        private NorthStarImage? lastDisplayedImage;

        private int displayedFrameCount;

        private bool closed;

        private bool disposed;


        public PreviewDisplay(
            NorthStarRuntime runtime,
            DispatcherQueue dispatcherQueue,
            Image previewImage,
            Canvas trackingOverlay,
            TextBlock fpsText)
        {
            this.runtime =
                runtime;

            this.fpsText =
                fpsText;

            previewRenderer =
                new PreviewRenderer(
                    previewImage,
                    trackingOverlay);

            previewDisplayTimer =
                dispatcherQueue.CreateTimer();

            previewDisplayTimer.Interval =
                TimeSpan.FromMilliseconds(16);

            previewDisplayTimer.IsRepeating =
                true;

            previewDisplayTimer.Tick +=
                PreviewDisplayTimer_Tick;

            fpsStopwatch =
                new Stopwatch();

            ResetFpsCounter();
        }


        public void Start(
            int width,
            int height)
        {
            previewRenderer.Initialize(
                width,
                height);

            ResetFpsCounter();

            previewDisplayTimer.Start();
        }

        public void Stop()
        {
            previewDisplayTimer.Stop();

            previewRenderer.Reset();

            ResetFpsCounter();
        }


        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            closed =
                true;

            Stop();

            previewDisplayTimer.Tick -=
                PreviewDisplayTimer_Tick;

            disposed =
                true;
        }


        private void PreviewDisplayTimer_Tick(
            DispatcherQueueTimer sender,
            object args)
        {
            if (closed)
            {
                return;
            }

            if (!runtime.IsRunning)
            {
                return;
            }

            NorthStarTrackingFrame? trackingFrame =
                runtime.LatestFrame;

            if (trackingFrame != null)
            {
                previewRenderer.Render(
                    trackingFrame);

                UpdateFps(
                    trackingFrame.Image);

                return;
            }

            NorthStarImage? image =
                runtime.LatestImage;

            if (image == null)
            {
                return;
            }

            previewRenderer.Render(
                image);

            UpdateFps(
                image);
        }

        private void ResetFpsCounter()
        {
            fpsStopwatch.Reset();

            lastDisplayedImage =
                null;

            displayedFrameCount =
                0;

            fpsText.Text =
                "FPS: 0";
        }

        private void UpdateFps(
            NorthStarImage image)
        {
            if (ReferenceEquals(
                    image,
                    lastDisplayedImage))
            {
                return;
            }

            lastDisplayedImage =
                image;

            displayedFrameCount++;

            if (!fpsStopwatch.IsRunning)
            {
                fpsStopwatch.Start();

                return;
            }

            TimeSpan elapsed =
                fpsStopwatch.Elapsed;

            if (elapsed <
                TimeSpan.FromSeconds(1))
            {
                return;
            }

            double framesPerSecond =
                displayedFrameCount /
                elapsed.TotalSeconds;

            fpsText.Text =
                $"FPS: {framesPerSecond:F0}";

            displayedFrameCount =
                0;

            fpsStopwatch.Restart();
        }
    }
}
