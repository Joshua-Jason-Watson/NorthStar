using NorthStar.Camera;

namespace NorthStar.Tests
{
    public class CameraCapabilityTests
    {
        private static readonly Guid Nv12Subtype =
            new Guid(
                "3231564E-0000-0010-8000-00AA00389B71");

        private static readonly Guid MjpgSubtype =
            new Guid(
                "47504A4D-0000-0010-8000-00AA00389B71");

        private static readonly Guid Yuy2Subtype =
            new Guid(
                "32595559-0000-0010-8000-00AA00389B71");

        [Fact]
        public void Fps_UsesExactMediaFoundationFraction()
        {
            CameraCapability capability =
                new CameraCapability(
                    0,
                    1920,
                    1080,
                    30000,
                    1001,
                    Nv12Subtype);

            Assert.Equal(
                30000d / 1001d,
                capability.FPS);
        }

        [Theory]
        [InlineData("3231564E-0000-0010-8000-00AA00389B71", "NV12")]
        [InlineData("32595559-0000-0010-8000-00AA00389B71", "YUY2")]
        [InlineData("47504A4D-0000-0010-8000-00AA00389B71", "MJPG")]
        public void FormatName_MapsKnownSubtypes(
            string subtype,
            string expectedName)
        {
            CameraCapability capability =
                new CameraCapability(
                    1,
                    640,
                    480,
                    30,
                    1,
                    Guid.Parse(subtype));

            Assert.Equal(
                expectedName,
                capability.FormatName);

            Assert.Contains(
                expectedName,
                capability.ToString());
        }

        [Fact]
        public void Constructor_RejectsInvalidDimensionsAndRate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CameraCapability(
                    0,
                    0,
                    480,
                    30,
                    1,
                    Nv12Subtype));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CameraCapability(
                    0,
                    640,
                    0,
                    30,
                    1,
                    Nv12Subtype));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CameraCapability(
                    0,
                    640,
                    480,
                    0,
                    1,
                    Nv12Subtype));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CameraCapability(
                    0,
                    640,
                    480,
                    30,
                    0,
                    Nv12Subtype));

            Assert.Throws<ArgumentException>(
                () => new CameraCapability(
                    0,
                    640,
                    480,
                    30,
                    1,
                    Guid.Empty));
        }

        [Fact]
        public void KnownSubtypeFields_AreDistinct()
        {
            Assert.NotEqual(
                Nv12Subtype,
                Yuy2Subtype);

            Assert.NotEqual(
                Nv12Subtype,
                MjpgSubtype);
        }
    }
}
