using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;

namespace NorthStar.Tracking
{
    // Converts the raw SimCC outputs from the pose model into
    // NorthStar landmark coordinates and confidence values.
    public sealed class PoseDecoder
    {
        private const string SimccXOutputName =
            "simcc_x";

        private const string SimccYOutputName =
            "simcc_y";

        public PoseDecodeResult Decode(
            IReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(
                    nameof(results));
            }

            Tensor<float>? simccX = null;
            Tensor<float>? simccY = null;

            foreach (
                DisposableNamedOnnxValue result
                in results)
            {
                Tensor<float> outputTensor =
                    result.AsTensor<float>();

                if (result.Name ==
                    SimccXOutputName)
                {
                    simccX =
                        outputTensor;
                }
                else if (result.Name ==
                         SimccYOutputName)
                {
                    simccY =
                        outputTensor;
                }
            }

            if (simccX == null ||
                simccY == null)
            {
                throw new InvalidOperationException(
                    "SimCC X or Y output was not found.");
            }

            ValidateSimccTensor(
                simccX,
                SimccXOutputName);

            ValidateSimccTensor(
                simccY,
                SimccYOutputName);

            int keypointCount =
                simccX.Dimensions[1];

            int xResolution =
                simccX.Dimensions[2];

            int yResolution =
                simccY.Dimensions[2];

            if (simccY.Dimensions[1] !=
                keypointCount)
            {
                throw new InvalidOperationException(
                    "SimCC X and Y outputs contain different " +
                    "numbers of keypoints.");
            }

            List<Landmark> landmarks =
                new List<Landmark>(
                    keypointCount);

            for (int k = 0;
                 k < keypointCount;
                 k++)
            {
                float maxXValue =
                    float.NegativeInfinity;

                int maxXIndex =
                    0;

                float maxYValue =
                    float.NegativeInfinity;

                int maxYIndex =
                    0;

                for (int i = 0;
                     i < xResolution;
                     i++)
                {
                    float value =
                        simccX[0, k, i];

                    if (value > maxXValue)
                    {
                        maxXValue =
                            value;

                        maxXIndex =
                            i;
                    }
                }

                for (int i = 0;
                     i < yResolution;
                     i++)
                {
                    float value =
                        simccY[0, k, i];

                    if (value > maxYValue)
                    {
                        maxYValue =
                            value;

                        maxYIndex =
                            i;
                    }
                }

                float confidence =
                    (maxXValue + maxYValue) / 2f;

                landmarks.Add(
                    new Landmark(
                        k,
                        maxXIndex,
                        maxYIndex,
                        confidence));
            }

            return new PoseDecodeResult(
                landmarks,
                xResolution,
                yResolution);
        }

        private static void ValidateSimccTensor(
    Tensor<float> tensor,
    string outputName)
        {
            if (tensor.Rank != 3)
            {
                throw new InvalidOperationException(
                    $"SimCC output '{outputName}' must have " +
                    "three dimensions: [batch, keypoints, resolution].");
            }

            if (tensor.Dimensions[0] != 1)
            {
                throw new InvalidOperationException(
                    $"SimCC output '{outputName}' must have " +
                    "a batch size of 1.");
            }

            if (tensor.Dimensions[1] <= 0)
            {
                throw new InvalidOperationException(
                    $"SimCC output '{outputName}' contains " +
                    "no keypoints.");
            }

            if (tensor.Dimensions[2] <= 0)
            {
                throw new InvalidOperationException(
                    $"SimCC output '{outputName}' has an invalid resolution.");
            }
        }
    }
}