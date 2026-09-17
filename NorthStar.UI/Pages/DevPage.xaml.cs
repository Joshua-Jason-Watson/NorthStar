using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

using NorthStar.Tracking;
using NorthStar.Processing;

using System;

namespace NorthStar.UI.Pages
{
    public sealed partial class DevPage : Page, IDisposable
    {
        // ============================================================
        // Runtime
        // ============================================================

        private NorthStarRuntime? runtime;


        // ============================================================
        // Update timer
        // ============================================================

        private readonly DispatcherQueueTimer updateTimer;


        // ============================================================
        // State
        // ============================================================

        private bool disposed;


        // ============================================================
        // Constructor
        // ============================================================

        public DevPage()
        {
            InitializeComponent();

            DispatcherQueue dispatcherQueue =
                DispatcherQueue.GetForCurrentThread();

            updateTimer =
                dispatcherQueue.CreateTimer();

            updateTimer.Interval =
                TimeSpan.FromMilliseconds(100);

            updateTimer.IsRepeating =
                true;

            updateTimer.Tick +=
                UpdateTimer_Tick;
        }


        // ============================================================
        // Runtime
        // ============================================================

        public void SetRuntime(
            NorthStarRuntime? runtime)
        {
            if (disposed)
            {
                return;
            }

            this.runtime =
                runtime;

            if (runtime == null)
            {
                updateTimer.Stop();

                ClearMetrics();

                RuntimeStatusText.Text =
                    "Runtime: Not running";

                TrackingStatusText.Text =
                    "Tracking: Inactive";

                return;
            }

            updateTimer.Start();

            UpdateMetrics();
        }


        // ============================================================
        // Timer
        // ============================================================

        private void UpdateTimer_Tick(
            DispatcherQueueTimer sender,
            object args)
        {
            if (disposed)
            {
                return;
            }

            UpdateMetrics();
        }


        // ============================================================
        // Metrics
        // ============================================================

        private void UpdateMetrics()
        {
            NorthStarRuntime? currentRuntime =
                runtime;

            if (currentRuntime == null)
            {
                ClearMetrics();

                RuntimeStatusText.Text =
                    "Runtime: Not running";

                TrackingStatusText.Text =
                    "Tracking: Inactive";

                return;
            }

            RuntimeStatusText.Text =
                currentRuntime.IsRunning
                    ? "Runtime: Running"
                    : "Runtime: Stopped";

            TrackingStatusText.Text =
                currentRuntime.IsTracking
                    ? "Tracking: Active"
                    : "Tracking: Inactive";


            NorthStarPipelineMetrics? pipelineMetrics =
                currentRuntime.LatestPipelineMetrics;

            if (pipelineMetrics.HasValue)
            {
                NorthStarPipelineMetrics metrics =
                    pipelineMetrics.Value;

                CaptureTimeText.Text =
                    FormatMilliseconds(
                        metrics.CaptureMilliseconds);

                DecodeTimeText.Text =
                    FormatMilliseconds(
                        metrics.DecodeMilliseconds);

                ConversionTimeText.Text =
                    FormatMilliseconds(
                        metrics.ConversionMilliseconds);

                PipelineTotalTimeText.Text =
                    FormatMilliseconds(
                        metrics.TotalMilliseconds);
            }
            else
            {
                ClearPipelineMetrics();
            }


            PoseModelMetrics? poseModelMetrics =
                currentRuntime.LatestPoseModelMetrics;

            if (poseModelMetrics.HasValue)
            {
                PoseModelMetrics metrics =
                    poseModelMetrics.Value;

                PreprocessingTimeText.Text =
                    FormatMilliseconds(
                        metrics.PreprocessingMilliseconds);

                InferenceTimeText.Text =
                    FormatMilliseconds(
                        metrics.InferenceMilliseconds);

                PoseDecodeTimeText.Text =
                    FormatMilliseconds(
                        metrics.PoseDecodeMilliseconds);

                CoordinateTransformTimeText.Text =
                    FormatMilliseconds(
                        metrics.CoordinateTransformMilliseconds);

                PoseTotalTimeText.Text =
                    FormatMilliseconds(
                        metrics.TotalMilliseconds);
            }
            else
            {
                ClearPoseModelMetrics();
            }
        }


        // ============================================================
        // Display helpers
        // ============================================================

        private void ClearMetrics()
        {
            ClearPipelineMetrics();
            ClearPoseModelMetrics();
        }


        private void ClearPipelineMetrics()
        {
            CaptureTimeText.Text =
                "—";

            DecodeTimeText.Text =
                "—";

            ConversionTimeText.Text =
                "—";

            PipelineTotalTimeText.Text =
                "—";
        }


        private void ClearPoseModelMetrics()
        {
            PreprocessingTimeText.Text =
                "—";

            InferenceTimeText.Text =
                "—";

            PoseDecodeTimeText.Text =
                "—";

            CoordinateTransformTimeText.Text =
                "—";

            PoseTotalTimeText.Text =
                "—";
        }


        private static string FormatMilliseconds(
            double milliseconds)
        {
            return
                $"{milliseconds:F2} ms";
        }


        // ============================================================
        // Disposal
        // ============================================================

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed =
                true;

            updateTimer.Stop();

            updateTimer.Tick -=
                UpdateTimer_Tick;

            runtime =
                null;
        }
    }
}