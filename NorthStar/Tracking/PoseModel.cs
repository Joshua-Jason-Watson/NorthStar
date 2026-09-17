using Microsoft.ML.OnnxRuntime;

using NorthStar.Frames;
using NorthStar.Processing;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NorthStar.Tracking
{
    // Manages the ONNX pose model and coordinates preprocessing,
    // inference, decoding, and coordinate transformation.
    public sealed class PoseModel : IDisposable
    {
        // ONNX Runtime session used to execute the pose model.
        private InferenceSession? session;

        // Converts NorthStar images into the tensor format expected
        // by the pose model.
        private readonly FramePreprocessor preprocessor;

        // Converts the model's raw output into pose landmarks.
        private readonly PoseDecoder decoder;

        // Converts landmark coordinates from model space back into
        // the coordinates of the original camera image.
        private readonly CoordinateTransformer transformer;

        // Actual model input dimensions.
        private readonly int modelWidth;
        private readonly int modelHeight;

        // Name of the model's input tensor.
        private readonly string inputName;

        private bool disposed;


        public PoseModel(
            string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException(
                    "Model path cannot be empty.",
                    nameof(modelPath));
            }

            try
            {
                session =
                    new InferenceSession(
                        modelPath);

                if (session.InputMetadata.Count != 1)
                {
                    throw new InvalidOperationException(
                        "NorthStar pose models must have exactly one input tensor.");
                }

                KeyValuePair<string, NodeMetadata> input =
                    session.InputMetadata
                        .First();

                inputName =
                    input.Key;

                IReadOnlyList<int> dimensions =
                    input.Value.Dimensions;

                if (dimensions.Count != 4)
                {
                    throw new InvalidOperationException(
                        "The pose model input must have four dimensions: " +
                        "[batch, channels, height, width].");
                }

                if (dimensions[0] != -1 &&
                    dimensions[0] != 1)
                {
                    throw new InvalidOperationException(
                        $"The pose model must support a batch size of 1. " +
                        $"Model reported: {dimensions[0]}.");
                }

                if (dimensions[1] != 3)
                {
                    throw new InvalidOperationException(
                        $"The pose model must use three input channels. " +
                        $"Model reported: {dimensions[1]}.");
                }

                modelHeight =
                    dimensions[2];

                modelWidth =
                    dimensions[3];

                if (modelWidth <= 0)
                {
                    throw new InvalidOperationException(
                        $"Model reported invalid width: {modelWidth}.");
                }

                if (modelHeight <= 0)
                {
                    throw new InvalidOperationException(
                        $"Model reported invalid height: {modelHeight}.");
                }

                preprocessor =
                    new FramePreprocessor(
                        modelWidth,
                        modelHeight);

                decoder =
                    new PoseDecoder();

                transformer =
                    new CoordinateTransformer();
            }
            catch
            {
                session?.Dispose();
                session = null;

                throw;
            }
        }


        public void PrintModelInfo()
        {
            ThrowIfDisposed();

            foreach (
                KeyValuePair<string, NodeMetadata> input
                in session!.InputMetadata)
            {
                Console.WriteLine(
                    input.Key);

                foreach (
                    int dimension
                    in input.Value.Dimensions)
                {
                    Console.WriteLine(
                        dimension);
                }
            }

            foreach (
                KeyValuePair<string, NodeMetadata> output
                in session.OutputMetadata)
            {
                Console.WriteLine(
                    output.Key);

                foreach (
                    int dimension
                    in output.Value.Dimensions)
                {
                    Console.WriteLine(
                        dimension);
                }
            }
        }


        public PoseResult ProcessFrame(
            NorthStarImage image)
        {
            return ProcessFrame(
                image,
                out _);
        }


        public PoseResult ProcessFrame(
            NorthStarImage image,
            out PoseModelMetrics metrics)
        {
            ThrowIfDisposed();

            if (image == null)
            {
                throw new ArgumentNullException(
                    nameof(image));
            }

            long totalStartTimestamp =
                Stopwatch.GetTimestamp();


            // ========================================================
            // Preprocessing
            // ========================================================

            long preprocessingStartTimestamp =
                Stopwatch.GetTimestamp();

            PreprocessResult preprocessResult =
                preprocessor.CreateTensor(
                    image);

            double preprocessingMilliseconds =
                GetElapsedMilliseconds(
                    preprocessingStartTimestamp);


            // ========================================================
            // Inference
            // ========================================================

            NamedOnnxValue input =
                NamedOnnxValue.CreateFromTensor(
                    inputName,
                    preprocessResult.Tensor);

            var inputs =
                new List<NamedOnnxValue>
                {
                    input
                };

            long inferenceStartTimestamp =
                Stopwatch.GetTimestamp();

            using IDisposableReadOnlyCollection<
                DisposableNamedOnnxValue> results =
                session!.Run(
                    inputs);

            double inferenceMilliseconds =
                GetElapsedMilliseconds(
                    inferenceStartTimestamp);


            // ========================================================
            // Pose decoding
            // ========================================================

            long poseDecodeStartTimestamp =
                Stopwatch.GetTimestamp();

            PoseDecodeResult decodedPose =
                decoder.Decode(
                    results);

            double poseDecodeMilliseconds =
                GetElapsedMilliseconds(
                    poseDecodeStartTimestamp);


            // ========================================================
            // Coordinate transformation
            // ========================================================

            long coordinateTransformStartTimestamp =
                Stopwatch.GetTimestamp();

            List<Landmark> transformedLandmarks =
                new List<Landmark>(
                    decodedPose.Landmarks.Count);

            foreach (
                Landmark landmark
                in decodedPose.Landmarks)
            {
                NorthStarPoint simccPoint =
                    new NorthStarPoint(
                        (int)landmark.X,
                        (int)landmark.Y);

                NorthStarPoint originalPoint =
                    transformer.Transform(
                        simccPoint,
                        decodedPose.XResolution,
                        decodedPose.YResolution,
                        modelWidth,
                        modelHeight,
                        preprocessResult);

                transformedLandmarks.Add(
                    new Landmark(
                        landmark.KeypointID,
                        originalPoint.X,
                        originalPoint.Y,
                        landmark.Confidence));
            }

            double coordinateTransformMilliseconds =
                GetElapsedMilliseconds(
                    coordinateTransformStartTimestamp);


            // ========================================================
            // Result
            // ========================================================

            double totalMilliseconds =
                GetElapsedMilliseconds(
                    totalStartTimestamp);

            metrics =
                new PoseModelMetrics(
                    preprocessingMilliseconds,
                    inferenceMilliseconds,
                    poseDecodeMilliseconds,
                    coordinateTransformMilliseconds,
                    totalMilliseconds);

            return new PoseResult(
                transformedLandmarks);
        }


        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            session?.Dispose();
            session = null;

            GC.SuppressFinalize(
                this);
        }


        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
        }


        private static double GetElapsedMilliseconds(
            long startTimestamp)
        {
            long elapsedTimestamp =
                Stopwatch.GetTimestamp() -
                startTimestamp;

            return
                elapsedTimestamp *
                1000.0 /
                Stopwatch.Frequency;
        }
    }
}