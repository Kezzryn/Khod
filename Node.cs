using BKH.Geometry;
using System.IO.Pipes;

namespace Khod;

internal class Node
{
    public int Radius { get; set; }
    public int SubNodeRadius { get; set; }
    public int POS { get; set; }
    public int WorldX { get; set; } = 0;
    public int WorldY { get; set; } = 0;
    public int TargetX { get; set; } = -1;
    public int TargetY { get; set; } = -1;
    public int TargetRadius { get; set; } = -1;
    public Point2D GridXY { get; set; }
    public List<Point2D> EndPoints { get { return StartPoints; } }
    private List<Point2D> StartPoints = [];
    private int startPointsIndex = 0;
    public readonly List<(int X, int Y)> TraceLine = [];
    public readonly List<int> SubNodes = [];
    public List<Point2D> FinalPath = [];

    public Node((int X, int Y) worldXY, int position, int radius, int subnodeRadius = 5)
    {
        WorldX = worldXY.X; 
        WorldY = worldXY.Y;
        POS = position;
        Radius = radius;
        SubNodeRadius = subnodeRadius;

        GridXY = KhodMap.WorldToGrid(WorldX, WorldY);
    }

    public void GenerateStartPoints(int maxRadius)
    {
        //not perfect, we lose a few at the corners. Something for Future Me.
        //int n_r = ((((maxRadius  * 2) + KhodMap.GRID_SIZE - 1) / KhodMap.GRID_SIZE) - 1) / 2;
        StartPoints = [.. GridXY.GetNeighborsAtRadius(maxRadius)];
    }

    public string GetSVG()
    {
        string node = $"<circle cx=\"{WorldX}\" cy=\"{WorldY}\" r=\"{Radius}\" style=\"fill:red;stroke:black;stroke-width:3;fill-opacity:0.0\"/>";
        //string node = $"<circle cx=\"{X}\" cy=\"{Y}\" r=\"{Radius}\" style=\"fill:none;stroke:black;stroke-width:3\"/>";
        //string node = $"<circle cx=\"{X}\" cy=\"{Y}\" r=\"{Radius}\"/>\n";
        
        return DrawTraceLine() + node + DrawSubNodes(); 
    }

    public int MinTraceDistance()
    {
        if (SubNodes.Count == 0) return 0;
        return SubNodes.Sum() + SubNodes.Count; // Count -1 for spaces, but +1 for leading space. 
    }

    public void SortStartPoints(Point2D dest)
    {
        StartPoints = [.. StartPoints.OrderBy(x => Point2D.TaxiDistance2D(dest, x))];
    }

    public Point2D GetNextStartPoint()
    {
        if (AtEndOfStartPoints()) return new(-1, -1);

        return StartPoints[startPointsIndex++];
    }

    public bool AtEndOfStartPoints() => startPointsIndex >= StartPoints.Count;

    public void ResetStartPointIndex() => startPointsIndex = 0;

    private string DrawSubNodes()
    {
        string returnvalue = "";

        if((SubNodes.Sum() + SubNodes.Count - 1) > (TraceLine.Count - 1))
        {
            Console.WriteLine("Traceline is too short!");
            return returnvalue;
        }

        int traceLinePos = 2;
        for(int i = 0; i < SubNodes.Count; i++)
        {
            for(int j = 0; j < SubNodes[i]; j++)
            {
                returnvalue += $"<circle cx=\"{TraceLine[traceLinePos].X}\" cy=\"{TraceLine[traceLinePos].Y}\" r=\"{SubNodeRadius}\" style=\"fill:none;stroke:black;stroke-width:3;fill-opacity:0.0\"/>\n";

                traceLinePos++;
            }
            if (i < SubNodes.Count - 1) traceLinePos++;
        }

        return returnvalue;
    }

    private static (int x, int y) CalculateIntersection(int sourceX, int sourceY, int sourceRadius, (int X, int Y) target)
    {
        return CalculateIntersection(sourceX, sourceY, sourceRadius, target.X, target.Y);
    }
    private static (int x, int y) CalculateIntersection(int sourceX, int sourceY, int sourceRadius, int targetX, int targetY)
    {
        // Circle center and radius
        double cx = sourceX;
        double cy = sourceY;
        double radius = sourceRadius;

        // Target point on the line
        double tx = targetX;
        double ty = targetY;

        // Direction vector from center to target
        double dx = tx - cx;  // -2
        double dy = ty - cy;  // 2

        // Normalize direction vector
        double length = Math.Sqrt(dx * dx + dy * dy);
        double dxNorm = dx / length;
        double dyNorm = dy / length;

        // Move from center in that direction by radius
        double intersectionX = cx + dxNorm * radius;
        double intersectionY = cy + dyNorm * radius;
        return ((int)intersectionX, (int)intersectionY);
    }

    private string DrawTraceLine()
    {
        int offset = KhodMap.GRID_SIZE / 2;

        //from my source intersect at my radius, targeting first trace line. 

        if (FinalPath.Count > 0)
        {
            TraceLine.Add(CalculateIntersection(WorldX, WorldY, Radius, KhodMap.GridToWorld(FinalPath.First(), offset)));

            foreach (Point2D p in FinalPath)
            {
                TraceLine.Add(KhodMap.GridToWorld(p, offset));
            }

            TraceLine.Add(CalculateIntersection(TargetX, TargetY, TargetRadius, KhodMap.GridToWorld(FinalPath.Last(), offset)));
        }
       // if(TargetRadius != -1) TraceLine.Add((TargetX, TargetY));

        if (TraceLine.Count == 0) return "";

        string pointList = String.Join(" ", TraceLine.Select(x => $"{x.X},{x.Y}"));

        return $"<polyline points=\"" + pointList + "\"   style=\"fill:none;stroke:green;stroke-width:3\" />\n";
    }
}