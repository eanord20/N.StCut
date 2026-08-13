namespace NStCut.Core.Geometry;

public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Right => X + Width;
    public double Bottom => Y;
    public double Top => Y + Height;
    public double Area => Width * Height;

    public bool Intersects(Rect other)
    {
        return Left < other.Right && Right > other.Left &&
               Bottom < other.Top && Top > other.Bottom;
    }

    public bool Contains(Rect other)
    {
        return Left <= other.Left && Right >= other.Right &&
               Bottom <= other.Bottom && Top >= other.Top;
    }
}
