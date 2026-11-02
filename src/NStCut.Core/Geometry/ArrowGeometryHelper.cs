namespace NStCut.Core.Geometry;

public static class ArrowGeometryHelper
{
    public static (Point Head1, Point Head2) GetArrowHead(
        Point start,
        Point end,
        double headLength,
        double headAngle)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0)
            return (end, end);

        var ux = dx / length;
        var uy = dy / length;
        var px = -uy;
        var py = ux;

        var halfSpan = headLength * Math.Tan(headAngle);
        var backX = end.X - headLength * ux;
        var backY = end.Y - headLength * uy;

        var head1 = new Point(backX + halfSpan * px, backY + halfSpan * py);
        var head2 = new Point(backX - halfSpan * px, backY - halfSpan * py);
        return (head1, head2);
    }
}
