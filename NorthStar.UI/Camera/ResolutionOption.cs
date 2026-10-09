using NorthStar.Camera;

namespace NorthStar.UI.Camera
{
    internal sealed class ResolutionOption
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
}
