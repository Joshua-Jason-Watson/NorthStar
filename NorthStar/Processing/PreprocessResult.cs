using Microsoft.ML.OnnxRuntime.Tensors;

namespace NorthStar.Processing
{
    // Stores the completed preprocessing result along with the
    // information needed to convert model coordinates back to
    // the original frame.
    public sealed class PreprocessResult
    {
        // The image data formatted for the ONNX model.
        public DenseTensor<float> Tensor { get; }

        // Scale applied when resizing the original frame.
        public float Scale { get; }

        // Horizontal padding added during preprocessing.
        public int PaddingX { get; }

        // Vertical padding added during preprocessing.
        public int PaddingY { get; }

        // Original frame dimensions before preprocessing.
        public int OriginalWidth { get; }

        public int OriginalHeight { get; }

        public PreprocessResult(
            DenseTensor<float> tensor,
            float scale,
            int paddingX,
            int paddingY,
            int originalWidth,
            int originalHeight)
        {
            Tensor =
                tensor ??
                throw new System.ArgumentNullException(
                    nameof(tensor));

            if (scale <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(scale));
            }

            if (paddingX < 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(paddingX));
            }

            if (paddingY < 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(paddingY));
            }

            if (originalWidth <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(originalWidth));
            }

            if (originalHeight <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(originalHeight));
            }

            Scale =
                scale;

            PaddingX =
                paddingX;

            PaddingY =
                paddingY;

            OriginalWidth =
                originalWidth;

            OriginalHeight =
                originalHeight;
        }
    }
}