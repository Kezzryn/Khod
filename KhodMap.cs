namespace Khod;
using BKH.Geometry;

internal class KhodMap
{
    // this serves as our reference starting position for everything. 
    private readonly Dictionary<Point2D, int> _theMap = [];

    //the path after an A_Star call. 
    public List<Point2D> FinalPath = [];

    //as found on the map.
    public Point2D StartPosition { get; set; }
    public Point2D EndPosition { get; set; }

    public List<Point2D> EndRing = [];
    public int MinSteps { get; set; } = 0;
    public int NumSteps = -1;

    private readonly Dictionary<Point2D, (float gScore, float fScore, Point2D? parent)> stepCounter = [];
    //private readonly Dictionary<Point2D, (int gScore, int fScore, Point2D? parent)> stepCounter = [];

    public static int GRID_SIZE = 15;

    //0 is open 
    //100 is another trace, or charge/ground, or ... whatever.
    public const int OPEN = 0;
    public const int BLOCKED = 100;
    public const int SLOW_SQUARE = 200;

    public KhodMap(int maxX, int maxY)
    {
        foreach(Point2D p in  from x in Enumerable.Range(0, maxX)
                              from y in Enumerable.Range(0, maxY)
                              select new Point2D(x, y))
        {
            MarkMap(p, OPEN);
        }
    }

    private float TestStep(Point2D cursor, Point2D nextStep)
    {
        float returnValue = -1;

        //off the map
        if (!_theMap.TryGetValue(nextStep, out int nextValue)) return returnValue;

        //Standard don't path here
        if (nextValue == BLOCKED) return returnValue; 

        // corner case to stop long lines from moving around the start position.
        if (cursor == StartPosition && _theMap[nextStep] == SLOW_SQUARE) return returnValue;

        //Don't cross a diagonal. 
        if (!cursor.IsOnGridLine(nextStep))
        {
            Point2D diag1 = new(nextStep.X, cursor.Y);
            Point2D diag2 = new(cursor.X, nextStep.Y);

            _theMap.TryGetValue(diag1, out int value1);
            _theMap.TryGetValue(diag2, out int value2);
            if (value1 == BLOCKED && value2 == BLOCKED) return returnValue;
        }

        //Made it!
        stepCounter.TryAdd(nextStep, (int.MaxValue, int.MaxValue, null));
        
        //Figure out our steps.
        returnValue = cursor.IsOnGridLine(nextStep) ? 1 : 1.5f;
        if (nextValue == SLOW_SQUARE && EndRing.Contains(nextStep)) return 1; //free step to end square.
        
        return nextValue == SLOW_SQUARE ? 50 : returnValue;
    }

    private static IEnumerable<Point2D> NextSteps(Point2D cursor) => cursor.GetAllNeighbors();

    public static (int x, int y) GridToWorld(Point2D p, int offset = 0)
    {
        int x = p.X * GRID_SIZE;
        int y = p.Y * GRID_SIZE;

        return (x + offset, y + offset);
    }

    public static Point2D WorldToGrid((int x, int y) world)
    {
        return WorldToGrid(world.x, world.y);
    }

    public static Point2D WorldToGrid(int x, int y)
    {
        int newX = (x - (GRID_SIZE / 2)) / GRID_SIZE;
        int newY = (y - (GRID_SIZE / 2)) / GRID_SIZE;

        Point2D p = new(newX, newY);

        return p;
    }

    public void BlockPath(List<Point2D> path)
    {
        foreach (Point2D p in path)
        {
            MarkMap(p, BLOCKED);
        }
    }

    public void UnblockPath(List<Point2D> path)
    {
        foreach(Point2D p in path)
        {
            MarkMap(p, OPEN);
        }
        MarkMap(path.First(), SLOW_SQUARE);
        MarkMap(path.Last(), SLOW_SQUARE);
    }

    public void MarkMap(Point2D point, int value)
    {
        if(!_theMap.TryAdd(point, value))
        {
            _theMap[point] = value;     
        }
    }

    public int MapValueAt(Point2D point)
    {
        if (_theMap.TryGetValue(point, out int value))
        {
            return value;
        }
        return -1; 
    }

    public string Grid()
    {
        string returnValue = "";
        foreach ((Point2D p, int value) in _theMap.Where(x => x.Value != 0))
        {
            (int x, int y) = GridToWorld(p);
            string color = value switch
            {
                BLOCKED => "red",
                SLOW_SQUARE => "yellow",
                _ => "blue"
            };

            returnValue += $"<rect x={x} y={y} width={GRID_SIZE} height={GRID_SIZE} style=\"fill:{color};fill-opacity:0.3\"/>\n";
        }

        //debug diagonal
        //for (int i = 0; i < 35; i++)
        //{
        //    returnValue += $"<rect x={i * GRID_SIZE} y={i * GRID_SIZE} width={GRID_SIZE} height={GRID_SIZE} style=\"fill=:green;fill-opacity:0.5\"/>\n";
        //}

        return returnValue;
    }
    private static int Heuristic(Point2D a, Point2D b) => Point2D.TaxiDistance2D(a, b);

    public int A_Star() => A_Star(StartPosition, EndPosition);

    public int A_Star(Point2D start, Point2D end)
    {
        FinalPath.Clear();
        stepCounter.Clear();
        NumSteps = -1;

        PriorityQueue<Point2D, float> searchQueue = new(); //we enque based on fScore + h, the distance travelled, plus taxi distance guess to destination.
        HashSet<Point2D> inSearchQueue = []; //we add this because we don't have a way to query the queue to see if a specific item is in it.

        int gScore = 0; //gScore is value of the path from start to here
        stepCounter.Add(start, (gScore, Heuristic(start, end), null));

        searchQueue.Enqueue(start, stepCounter[start].fScore);
        inSearchQueue.Add(start);
        while (searchQueue.TryDequeue(out Point2D cursor, out _))
        {
            inSearchQueue.Remove(cursor);

            //We have arrived!
            //check to see if the cursor is at, or in proximity to a node.
            //this will be defined 
            if (EndRing.Any(x => x == cursor))
            {
                FinalPath.Add(cursor);
                //unroll our history.
                Point2D? p = stepCounter[cursor].parent;

                while (p != null)
                {
                    FinalPath.Add((Point2D)p);
                    p = stepCounter[(Point2D)p].parent;
                }

                //check the unroll since the step count could be 
                if (FinalPath.Count >= MinSteps)
                {
                    FinalPath.Reverse();
                    return FinalPath.Count;
                }

                //Failed to find a valid MinStep path. 
                FinalPath.Clear();
                continue;
            }

            foreach (Point2D nextStep in NextSteps(cursor))
            {
                //bounds and valid move check. 
                float dist = TestStep(cursor, nextStep);
                if (dist == -1) continue;

                //tentative_gScore := gScore[current] + d(current, neighbor)
                float t_gScore = stepCounter[cursor].gScore + dist;

                //if tentative_gScore < gScore[neighbor]
                if (t_gScore < stepCounter[nextStep].gScore)
                {
                    //cameFrom[neighbor] := current
                    //gScore[neighbor] := tentative_gScore
                    //fScore[neighbor] := tentative_gScore + h(neighbor)
                    stepCounter[nextStep] = (t_gScore, t_gScore + Heuristic(cursor, end), cursor);

                    //if neighbor not in openSet openSet.add(neighbor) 
                    if (!inSearchQueue.Contains(nextStep))
                    {
                        searchQueue.Enqueue(nextStep, stepCounter[nextStep].fScore);
                        inSearchQueue.Add(nextStep);
                    }
                }
            }
        }
        return -1;
    }
}