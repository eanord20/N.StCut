using System.Linq;

using NStCut.Core.Constants;

namespace NStCut.Core.Geometry;

/// <summary>Result of analyzing the union of free rectangles on a sheet.</summary>
public sealed class FreeSpaceAnalysis
{
    public static readonly FreeSpaceAnalysis Empty =
        new(0.0, Array.Empty<Pocket>(), 0.0, 0.0, 0.0, 0, 0);

    public FreeSpaceAnalysis(
        double unionArea,
        IReadOnlyList<Pocket> pockets,
        double valuableArea,
        double usefulArea,
        double scrapArea,
        int longThinWasteCount,
        int deepPocketCount)
    {
        UnionArea = unionArea;
        Pockets = pockets;
        ValuableArea = valuableArea;
        UsefulArea = usefulArea;
        ScrapArea = scrapArea;
        LongThinWasteCount = longThinWasteCount;
        DeepPocketCount = deepPocketCount;
    }

    public double UnionArea { get; }
    public IReadOnlyList<Pocket> Pockets { get; }
    public double ValuableArea { get; }
    public double UsefulArea { get; }
    public double ScrapArea { get; }
    public int FragmentationCount => Pockets.Count;
    public int LongThinWasteCount { get; }
    public int DeepPocketCount { get; }

    public double WeightedRemnantValue =>
        ValuableArea * AppConstants.ResidualValuableWeight +
        UsefulArea * AppConstants.ResidualUsefulWeight;
}

/// <summary>Decomposes a list of free rectangles into connected residual pockets and
/// finds the largest inscribed rectangle for each pocket.</summary>
public static class FreeSpaceAnalyzer
{
    public static FreeSpaceAnalysis Analyze(IReadOnlyList<Rect> freeRectangles)
    {
        if (freeRectangles.Count == 0)
            return FreeSpaceAnalysis.Empty;

        var xs = freeRectangles
            .SelectMany(r => new[] { r.Left, r.Right })
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var ys = freeRectangles
            .SelectMany(r => new[] { r.Bottom, r.Top })
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        if (xs.Count < 2 || ys.Count < 2)
            return FreeSpaceAnalysis.Empty;

        var nx = xs.Count - 1;
        var ny = ys.Count - 1;

        var occupied = new bool[ny, nx];
        for (var j = 0; j < nx; j++)
        {
            var cellX = xs[j];
            var cellWidth = xs[j + 1] - cellX;
            for (var i = 0; i < ny; i++)
            {
                var cellY = ys[i];
                var cellHeight = ys[i + 1] - cellY;
                var cell = new Rect(cellX, cellY, cellWidth, cellHeight);

                foreach (var r in freeRectangles)
                {
                    if (r.Contains(cell))
                    {
                        occupied[i, j] = true;
                        break;
                    }
                }
            }
        }

        var visited = new bool[ny, nx];
        var pockets = new List<Pocket>();
        double valuableArea = 0.0;
        double usefulArea = 0.0;
        double scrapArea = 0.0;
        var longThinCount = 0;
        var deepPocketCount = 0;

        for (var i = 0; i < ny; i++)
        {
            for (var j = 0; j < nx; j++)
            {
                if (!occupied[i, j] || visited[i, j])
                    continue;

                var component = CollectComponent(occupied, visited, i, j, nx, ny, xs, ys);
                var (pocketArea, largestRect, isValuable, isUseful, isLongThin, isDeepPocket) =
                    AnalyzeComponent(component, occupied, xs, ys);

                pockets.Add(new Pocket(
                    component,
                    pocketArea,
                    largestRect,
                    isValuable,
                    isUseful,
                    isLongThin,
                    isDeepPocket));

                if (isValuable) valuableArea += pocketArea;
                else if (isUseful) usefulArea += pocketArea;
                else scrapArea += pocketArea;

                if (isLongThin) longThinCount++;
                if (isDeepPocket) deepPocketCount++;
            }
        }

        var unionArea = pockets.Sum(p => p.Area);
        return new FreeSpaceAnalysis(
            unionArea,
            pockets,
            valuableArea,
            usefulArea,
            scrapArea,
            longThinCount,
            deepPocketCount);
    }

    private static List<Rect> CollectComponent(
        bool[,] occupied,
        bool[,] visited,
        int startI,
        int startJ,
        int nx,
        int ny,
        List<double> xs,
        List<double> ys)
    {
        var cells = new List<Rect>();
        var queue = new Queue<(int i, int j)>();
        queue.Enqueue((startI, startJ));
        visited[startI, startJ] = true;

        while (queue.Count > 0)
        {
            var (i, j) = queue.Dequeue();
            var rect = new Rect(xs[j], ys[i], xs[j + 1] - xs[j], ys[i + 1] - ys[i]);
            cells.Add(rect);

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var ni = i + dy;
                    var nj = j + dx;
                    if (ni < 0 || ni >= ny || nj < 0 || nj >= nx)
                        continue;
                    if (visited[ni, nj] || !occupied[ni, nj])
                        continue;

                    // 4-connected only: skip diagonal moves.
                    if (Math.Abs(dx) + Math.Abs(dy) != 1)
                        continue;

                    visited[ni, nj] = true;
                    queue.Enqueue((ni, nj));
                }
            }
        }

        return cells;
    }

    private static (double area, Rect largestRect, bool isValuable, bool isUseful, bool isLongThin, bool isDeepPocket)
        AnalyzeComponent(List<Rect> cells, bool[,] occupied, List<double> xs, List<double> ys)
    {
        var area = cells.Sum(c => c.Width * c.Height);

        // Build a local binary grid for the component's bounding box.
        var minI = cells.Min(c => c.Bottom);
        var maxI = cells.Max(c => c.Top);
        var minJ = cells.Min(c => c.Left);
        var maxJ = cells.Max(c => c.Right);

        var j0 = xs.IndexOf(minJ);
        var j1 = xs.IndexOf(maxJ);
        var i0 = ys.IndexOf(minI);
        var i1 = ys.IndexOf(maxI);

        var localRows = i1 - i0;
        var localCols = j1 - j0;
        var matrix = new bool[localRows, localCols];
        foreach (var cell in cells)
        {
            var cj = xs.IndexOf(cell.Left) - j0;
            var ci = ys.IndexOf(cell.Bottom) - i0;
            if (ci >= 0 && ci < localRows && cj >= 0 && cj < localCols)
                matrix[ci, cj] = true;
        }

        var largestRect = FindLargestInscribedRectangle(matrix, xs, ys, j0, i0);

        var w = largestRect.Width;
        var h = largestRect.Height;
        var minDim = Math.Min(w, h);
        var maxDim = Math.Max(w, h);
        var aspect = minDim < 1e-6 ? 0.0 : maxDim / minDim;

        var valuable =
            minDim >= AppConstants.RemnantMinUsefulWidthMm &&
            maxDim >= AppConstants.RemnantMinUsefulLengthMm &&
            aspect <= AppConstants.RemnantMaxUsefulAspectRatio;

        var useful = !valuable &&
            (minDim >= AppConstants.RemnantMinUsefulWidthMm ||
             maxDim >= AppConstants.RemnantMinUsefulLengthMm);

        var longThin =
            minDim < AppConstants.LongThinWasteMinDimensionMm &&
            aspect > AppConstants.LongThinWasteAspectRatioThreshold;

        var deepPocket =
            largestRect.Area > AppConstants.DeepPocketAreaThresholdMm2 &&
            minDim < AppConstants.DeepPocketMinDimensionMm;

        return (area, largestRect, valuable, useful, longThin, deepPocket);
    }

    private static Rect FindLargestInscribedRectangle(
        bool[,] matrix,
        List<double> xs,
        List<double> ys,
        int j0,
        int i0)
    {
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        if (rows == 0 || cols == 0)
            return new Rect(0, 0, 0, 0);

        var heights = new int[cols];
        var bestArea = 0.0;
        var bestR1 = 0;
        var bestR2 = 0;
        var bestC1 = 0;
        var bestC2 = 0;

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
                heights[c] = matrix[r, c] ? heights[c] + 1 : 0;

            var largest = LargestRectangleInHistogram(heights, r);
            if (largest.area > bestArea)
            {
                bestArea = largest.area;
                bestR1 = largest.topRow;
                bestR2 = r;
                bestC1 = largest.leftCol;
                bestC2 = largest.rightCol;
            }
        }

        var left = xs[j0 + bestC1];
        var right = xs[j0 + bestC2 + 1];
        var bottom = ys[i0 + bestR1];
        var top = ys[i0 + bestR2 + 1];
        return new Rect(left, bottom, right - left, top - bottom);
    }

    private static (double area, int topRow, int leftCol, int rightCol)
        LargestRectangleInHistogram(int[] heights, int currentRow)
    {
        var n = heights.Length;
        var bestArea = 0.0;
        var bestTop = 0;
        var bestLeft = 0;
        var bestRight = 0;

        var stack = new Stack<int>();
        for (var i = 0; i <= n; i++)
        {
            var h = i < n ? heights[i] : 0;
            while (stack.Count > 0 && h < heights[stack.Peek()])
            {
                var height = heights[stack.Pop()];
                var right = i - 1;
                var left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                var width = right - left + 1;

                if (width * (double)height > bestArea)
                {
                    bestArea = width * height;
                    bestTop = currentRow - height + 1;
                    bestLeft = left;
                    bestRight = right;
                }
            }
            stack.Push(i);
        }

        return (bestArea, bestTop, bestLeft, bestRight);
    }
}
