using Microsoft.ML.OnnxRuntime.Tensors;
using NorthStar.Frames;
using NorthStar.Processing;

namespace NorthStar.Tests
{
    public class CoordinateTransformerTests
    {
        [Fact]
        public void Transform_MapsIdentityScaleWithoutPadding()
        {
            CoordinateTransformer transformer =
                new CoordinateTransformer();

            NorthStarPoint result =
                transformer.Transform(
                    new NorthStarPoint(
                        10,
                        20),
                    192,
                    256,
                    192,
                    256,
                    CreatePreprocessResult(
                        1.0f,
                        0,
                        0));

            Assert.Equal(
                10,
                result.X);

            Assert.Equal(
                20,
                result.Y);
        }

        [Fact]
        public void Transform_RemovesPaddingThenUnscales()
        {
            CoordinateTransformer transformer =
                new CoordinateTransformer();

            NorthStarPoint result =
                transformer.Transform(
                    new NorthStarPoint(
                        14,
                        12),
                    192,
                    256,
                    192,
                    256,
                    CreatePreprocessResult(
                        0.5f,
                        4,
                        2));

            Assert.Equal(
                20,
                result.X);

            Assert.Equal(
                20,
                result.Y);
        }

        [Fact]
        public void Transform_RejectsPointOutsideSimccSpace()
        {
            CoordinateTransformer transformer =
                new CoordinateTransformer();

            PreprocessResult preprocessResult =
                CreatePreprocessResult(
                    1.0f,
                    0,
                    0);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => transformer.Transform(
                    new NorthStarPoint(
                        192,
                        0),
                    192,
                    256,
                    192,
                    256,
                    preprocessResult));
        }

        private static PreprocessResult CreatePreprocessResult(
            float scale,
            int paddingX,
            int paddingY)
        {
            return new PreprocessResult(
                new DenseTensor<float>(
                    new[]
                    {
                        1,
                        3,
                        1,
                        1
                    }),
                scale,
                paddingX,
                paddingY,
                640,
                480);
        }
    }
}
