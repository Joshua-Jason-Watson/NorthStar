namespace NorthStar.Formats
{
    // Describes the color-space information NorthStar needs when
    // interpreting or converting image data.
    public sealed class NorthStarColorSpace
    {
        // YUV-to-RGB transfer matrix, when applicable.
        public NorthStarYuvMatrix YuvMatrix { get; }

        // RGB/video color primaries.
        public NorthStarColorPrimaries Primaries { get; }

        // Transfer function used by the source.
        public NorthStarTransferFunction TransferFunction { get; }

        // Chroma sampling location.
        public NorthStarChromaSiting ChromaSiting { get; }

        // Nominal/studio or full color range.
        public NorthStarNominalRange NominalRange { get; }

        public NorthStarColorSpace(
            NorthStarYuvMatrix yuvMatrix,
            NorthStarColorPrimaries primaries,
            NorthStarTransferFunction transferFunction,
            NorthStarChromaSiting chromaSiting,
            NorthStarNominalRange nominalRange)
        {
            YuvMatrix = yuvMatrix;
            Primaries = primaries;
            TransferFunction = transferFunction;
            ChromaSiting = chromaSiting;
            NominalRange = nominalRange;
        }
    }
}