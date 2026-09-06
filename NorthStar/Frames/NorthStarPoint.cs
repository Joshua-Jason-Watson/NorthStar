namespace NorthStar.Frames
{
    // Represents a 2D coordinate within a NorthStar image or frame.
    public readonly struct NorthStarPoint
    {
        public int X { get; }
        public int Y { get; }

        public NorthStarPoint(
            int x,
            int y)
        {
            X = x;
            Y = y;
        }
    }
}