using NorthStar.Camera;

namespace NorthStar.UI.Camera
{
    internal sealed class FormatOption
    {
        public CameraCapability Capability { get; }

        public string DisplayName =>
            Capability.FormatName;

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
}
