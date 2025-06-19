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
    public List<Point2D> EdgePoints = [];
    private int edgePointIndex = 0;
    public readonly List<(int X, int Y)> TraceLine = [];
    public readonly List<int> SubNodes = [];
    public List<Point2D> GridPath = [];
    private string ChargeLinkTrace = "";
    private string GroundLinkTrace = "";


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
        EdgePoints = [.. GridXY.GetNeighborsAtRadius(maxRadius)];
    }

    public string GetSVG()
    {
        string node = $"<circle cx=\"{WorldX}\" cy=\"{WorldY}\" r=\"{Radius}\" style=\"fill:red;stroke:black;stroke-width:3;fill-opacity:0.0\"/>";

        return DrawTraceLine() + node + DrawSubNodes(); 
    }

    public int MinTraceDistance()
    {
        if (SubNodes.Count == 0) return 0;
        return SubNodes.Sum() + SubNodes.Count; // Count -1 for spaces, but +1 for leading space. 
    }

    public void SortStartPoints(Point2D dest)
    {
        EdgePoints = [.. EdgePoints.OrderBy(x => Point2D.TaxiDistance2D(x, dest))];
    }

    public Point2D GetNextStartPoint()
    {
        if (AtEndOfStartPoints()) return new(-1, -1);

        return EdgePoints[edgePointIndex++];
    }

    public bool AtEndOfStartPoints() => edgePointIndex >= EdgePoints.Count;

    public void ResetStartPointIndex() => edgePointIndex = 0;

    private string DrawSubNodes()
    {
        string returnvalue = "";

        if((SubNodes.Sum() + SubNodes.Count - 1) > (TraceLine.Count - 1))
        {
            Console.WriteLine($"ERROR: Traceline is too short. ({SubNodes.Sum()} + {SubNodes.Count - 1}) >  {TraceLine.Count} ");
            return returnvalue;
        }

        int traceLinePos = 2;
        for(int i = 0; i < SubNodes.Count; i++)
        {
            for(int j = 0; j < SubNodes[i]; j++)
            {
                returnvalue += $"<circle cx=\"{TraceLine[traceLinePos].X}\" cy=\"{TraceLine[traceLinePos].Y}\" r=\"{SubNodeRadius}\" style=\"stroke:black;stroke-width:3;fill-opacity:1.0\"/>\n";

                traceLinePos++;
            }
            if (i < SubNodes.Count - 1) traceLinePos++;
        }

        return returnvalue;
    }

    public static(int x, int y) CalculateIntersection(int sourceX, int sourceY, int sourceRadius, (int X, int Y) target)
    {
        return CalculateIntersection(sourceX, sourceY, sourceRadius, target.X, target.Y);
    }
    public static (int x, int y) CalculateIntersection(int sourceX, int sourceY, int sourceRadius, int targetX, int targetY)
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

        if (GroundLinkTrace != "") return GroundLinkTrace;
        if (GridPath.Count == 0)
        {
            Console.WriteLine($"ERROR: No FinalPath for POS: {POS} R:{Radius}");
            return "";
        }

        //from node source intersect at node radius, targeting first trace line. 

        TraceLine.Add(CalculateIntersection(WorldX, WorldY, Radius, KhodMap.GridToWorld(GridPath.First(), offset)));

        foreach (Point2D p in GridPath)
        {
            TraceLine.Add(KhodMap.GridToWorld(p, offset));
        }

        TraceLine.Add(CalculateIntersection(TargetX, TargetY, TargetRadius, KhodMap.GridToWorld(GridPath.Last(), offset)));

        string pointList = String.Join(" ", TraceLine.Select(x => $"{x.X},{x.Y}"));

        return ChargeLinkTrace + $"<polyline points=\"" + pointList + "\" style=\"fill:none;stroke:green;stroke-width:3\"/>\n";
    }

    public void AddChargeLinkTrace(KhodMap khodMap)
    {
        int offset = KhodMap.GRID_SIZE / 2;
        int quarterOffset = KhodMap.GRID_SIZE / 4;

        //figure out what direction we're going.
        int step = EdgePoints.Min(x => Point2D.TaxiDistance2D(GridXY, x));

        Point2D.Direction dir = Point2D.Direction.Left;
        //Point2D.Direction dir = node.POS switch
        //{
        //    1 or 4 or 7 => Point2D.Direction.Left,
        //    3 or 6 or 9 => Point2D.Direction.Right,
        //    2 => Point2D.Direction.Up,
        //    5 or 8 => Point2D.Direction.Down,
        //    _ => throw new NotImplementedException($"Unknown POS {node.POS}")
        //};

        Point2D startChargePos = GridXY.OrthogonalNeighbor(dir, step + 3);
        Point2D endChargePos = GridXY.OrthogonalNeighbor(dir, step);

        List<(int x, int y)> pointList = [];

        if (dir == Point2D.Direction.Left) // one day we'll do this in any direction. This isn't that day. 
        {
            (int x, int y) cursorChargeNode = KhodMap.GridToWorld(startChargePos, offset);
            pointList.Add(cursorChargeNode);
            cursorChargeNode.x += offset;
            pointList.Add(cursorChargeNode);
            cursorChargeNode.y -= KhodMap.GRID_SIZE;
            cursorChargeNode.x += quarterOffset;
            pointList.Add(cursorChargeNode);

            for (int i = 0; i < 3; i++)
            {
                cursorChargeNode.y += KhodMap.GRID_SIZE * 2;
                cursorChargeNode.x += quarterOffset;
                pointList.Add(cursorChargeNode);
                cursorChargeNode.y -= KhodMap.GRID_SIZE * 2;
                cursorChargeNode.x += quarterOffset;
                pointList.Add(cursorChargeNode);
            }
            cursorChargeNode.y += KhodMap.GRID_SIZE;
            cursorChargeNode.x += quarterOffset;
            pointList.Add(cursorChargeNode);
            cursorChargeNode.x += offset;
            pointList.Add(cursorChargeNode);

            khodMap.MarkMap(startChargePos, KhodMap.BLOCKED);
            khodMap.MarkMap(endChargePos, KhodMap.BLOCKED);
            foreach (Point2D p in from y in Enumerable.Range(-1, 3)
                                  from x in Enumerable.Range(1, 2)
                                  select startChargePos + new Point2D(x, y))
            {
                khodMap.MarkMap(p, KhodMap.BLOCKED);
            }
        }

        if (pointList.Count > 0)
        {
            pointList.Add(Node.CalculateIntersection(WorldX, WorldY, Radius, pointList.Last()));

            string chargeNode = String.Join(" ", pointList.Select(s => $"{s.x},{s.y}"));

            ChargeLinkTrace = $"<polyline points=\"" + chargeNode + "\" style=\"fill:none;stroke:green;stroke-width:3\"/>\n";
        }
    }

    public void AddGroundLinkTrace(KhodMap khodMap)
    {
        int offset = KhodMap.GRID_SIZE / 2;
        int step = EdgePoints.Min(x => Point2D.TaxiDistance2D(GridXY, x));

        Point2D.Direction dir = Point2D.Direction.Right;
        Point2D cursor = GridXY.OrthogonalNeighbor(dir, step);
        Point2D endChargePos = GridXY.OrthogonalNeighbor(dir, step + MinTraceDistance() + 1);

        //Point2D cursor = startChargePos;
        
        Console.WriteLine()
        while (cursor != endChargePos)
        {
            khodMap.MarkMap(cursor, KhodMap.BLOCKED);
            GridPath.Add(cursor);
            cursor = cursor.OrthogonalNeighbor(dir);
        }
    }
}