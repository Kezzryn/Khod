using BKH.Geometry;
using System.Diagnostics.CodeAnalysis;

namespace Khod;

internal class KhodWord
{
    const int SPACING = 150;
    const int MARGIN_X = 127;
    const int MARGIN_Y = 127;

    private static readonly Dictionary<int, List<char>> pos_to_char = new()
    {
        { 1, ['a','b','c'] },
        { 2, ['d','e','f'] },
        { 3, ['g','h','i'] },
        { 4, ['j','k','l'] },
        { 5, ['m','n','o'] },
        { 6, ['p','q','r'] },
        { 7, ['s','t','u'] },
        { 8, ['v','w','x'] },
        { 9, ['y','z'] }
    };

    private static readonly Dictionary<char, int> char_to_pos = new()
    {
        { 'a', 1 },     { 'd', 2 },     { 'g', 3 },
        { 'b', 1 },     { 'e', 2 },     { 'h', 3 },
        { 'c', 1 },     { 'f', 2 },     { 'i', 3 },

        { 'j', 4 },     { 'm', 5 },     { 'p', 6 },
        { 'k', 4 },     { 'n', 5 },     { 'q', 6 },
        { 'l', 4 },     { 'o', 5 },     { 'r', 6 },

        { 's', 7 },     { 'v', 8 },     { 'y', 9 },
        { 't', 7 },     { 'w', 8 },     { 'z', 9 },
        { 'u', 7 },     { 'x', 8 },
    };

    private static readonly Dictionary<int, Dictionary<int, int>> distance = [];

    private static readonly Dictionary<Point2D, int> point_to_pos = new()
    {
        { new(-1, -1), 1},
        { new(-1,  0), 2},
        { new(-1,  1), 3},
        { new( 0, -1), 4},
        { new( 0,  0), 5},
        { new( 0,  1), 6},
        { new( 1, -1), 7},
        { new( 1,  0), 8},
        { new( 1,  1), 9}
    };

    private readonly List<Node> nodes = [];

    private readonly KhodMap khodMap;

    private string chargeNode = "";

    public bool Verbose { get; set; }

    public KhodWord(string text, bool verbose = false)
    {
        Verbose = verbose;
        if (Verbose) Console.WriteLine($"Beginning parse of {text}");
        khodMap = new(35, 35);


        foreach ((Point2D source, int sourcePos) in point_to_pos)
        {
            distance.TryAdd(sourcePos, []);
            foreach((Point2D dest, int destPos) in point_to_pos.Where(x => x.Key != source))
            {
                int dist = Point2D.TaxiDistance2D(source, dest);
                distance[sourcePos].TryAdd(destPos, dist);
            }
        }

        if (Verbose) Console.WriteLine($"- Setting up nodes.");
        SetupNodes(text);
        if (Verbose) Console.WriteLine($"- Starting Link Tracing.");
        CalcLinkTrace();
    }

    public void CalcLinkTrace()
    {
        if (Verbose) Console.WriteLine("-- Build pairs of start and target nodes");
        List<(Node start, Node end)> links = [];

        for (int toNode = 1; toNode < nodes.Count; toNode++)
        {
            int fromNode = toNode - 1;
            if (Verbose) Console.WriteLine($"-- Adding link from node# {nodes[fromNode].POS} to {nodes[toNode].POS}");

            links.Add((nodes[fromNode], nodes[toNode]));
            nodes[fromNode].SortStartPoints(nodes[toNode].GridXY);
        }

        //build charge
        (Node firstLink, _) = links.First();
        firstLink.AddChargeLinkTrace(khodMap);

        //build a ground.
        (_, Node lastLink) = links.Last();
        lastLink.AddGroundLinkTrace(khodMap);  


        if (Verbose) Console.WriteLine("-- Sorting start/end pairs.");
         
        List<(Node fromNode, Node toNode)> shortToLong = [.. links.OrderBy(x => distance[x.start.POS][x.end.POS])];

        for (int currNode = 0; currNode < shortToLong.Count; currNode++)
        {
            (Node fromNode, Node toNode) = shortToLong[currNode];
            if (Verbose) Console.WriteLine($"-- Testing {fromNode.POS} to {toNode.POS}.");
            if (fromNode.GridPath.Count > 0)
            {
                if (Verbose) Console.WriteLine("-- Unblocking path.");
                khodMap.UnblockPath(fromNode.GridPath);
                fromNode.GridPath.Clear();
            }
            khodMap.EndPosition = toNode.GridXY;
            khodMap.EndRing = [.. toNode.EdgePoints];
            khodMap.MinSteps = fromNode.MinTraceDistance();
            
            bool isDone = false;
            bool isPath = false;
            do
            {
                if(fromNode.AtEndOfStartPoints())
                {
                    isDone = true;
                }
                else
                {
                    khodMap.StartPosition = fromNode.GetNextStartPoint();
                    if (khodMap.MapValueAt(khodMap.StartPosition) == KhodMap.BLOCKED) continue;
                    //Console.WriteLine($"Trying from {khodMap.StartPosition} ");

                    if (khodMap.A_Star() != -1)
                    {
                        fromNode.GridPath = [.. khodMap.FinalPath]; //make backup copy for possible unmarking later.
                        if (Verbose) Console.WriteLine($"-- FinalPath found. {fromNode.GridPath.First()} to {fromNode.GridPath.Last()}");
                        fromNode.TargetRadius = toNode.Radius;
                        fromNode.TargetX = toNode.WorldX;
                        fromNode.TargetY = toNode.WorldY;

                        khodMap.BlockPath(khodMap.FinalPath);

                        isDone = true;
                        isPath = true;
                    }
                }
            } while (!isDone);

            if (!isPath)
            {
                Console.WriteLine($"-- No path found.");
                //unblock the current node.
                fromNode.ResetStartPointIndex();
                if(fromNode.GridPath.Count != 0)
                {
                    khodMap.UnblockPath(fromNode.GridPath);
                    fromNode.GridPath.Clear();
                }

                //back up and try again.
                //reverse by two since the loop will push forward by one, giving us a net -1 step.
                currNode -= 2;
                //if we cannot backup, then fail out.
                if (currNode < 0)
                {
                    Console.WriteLine("Unable to reticulate splines, or whatever the hell I'm supposed to be doing.");
                    break;
                }
            }
        }
    }
    private static (int x, int y) NodePosition(int nodeNum)
    {
        int x = (((nodeNum - 1) % 3) * SPACING) + MARGIN_X;
        int y = (((nodeNum - 1) / 3) * SPACING) + MARGIN_Y;

        return (x, y);
    }

    private void SetupNodes(string text)
    {
        const int RADII_INCREASE = 7;
        const int DEFAULT_RADIUS = 20;

        for (int i = 0; i < text.Length; i++)
        {
            int currNodePos = char_to_pos[text[i]];

            int nodeRadius = DEFAULT_RADIUS;
            if (nodes.Any(x => x.POS == currNodePos))
            {
                nodeRadius = nodes.Where(x => x.POS == currNodePos).Max(x => x.Radius) + RADII_INCREASE;
            }

            Node newNode = new(NodePosition(currNodePos), currNodePos, nodeRadius);

            for (int j = i; j < text.Length; j++)
            {
                if (char_to_pos[text[j]] == currNodePos)
                {
                    newNode.SubNodes.Add(pos_to_char[currNodePos].IndexOf(text[j]) + 1);
                    if (j == text.Length - 1)
                    {
                        i = j;
                        break;
                    }
                }
                else
                {
                    i = j - 1;
                    break;
                }
            }

            nodes.Add(newNode);
        }

        //now that we have all the nodes setup, we need to update our map, and then push StartPoints back on each node at each POS.
        foreach(int pos in nodes.Select(x => x.POS).Distinct())
        {
            Point2D gridNode = KhodMap.WorldToGrid(NodePosition(pos));
            khodMap.MarkMap(gridNode, KhodMap.BLOCKED);

            int maxRadius = nodes.Where(x => x.POS == pos).Max(r => r.Radius);
            int n_r = ((((maxRadius * 2) + KhodMap.GRID_SIZE - 1) / KhodMap.GRID_SIZE) - 1) / 2;
            foreach (Point2D n in gridNode.GetAllNeighbors(n_r))
            {
                khodMap.MarkMap(n, KhodMap.BLOCKED);
            }

            List<Point2D> startPoints = [.. gridNode.GetNeighborsAtRadius(n_r + 1)];
            foreach (Point2D n in startPoints)
            {
                khodMap.MarkMap(n, KhodMap.SLOW_SQUARE);
            }

            foreach (Node n in nodes.Where(x => x.POS == pos))
            {
                n.GenerateStartPoints(n_r + 1);
            }
        }
    }

    public override string ToString()
    {
        
        const string SVG_HEADER = "<svg height=\"600\" width=\"600\" xmlns=\"http://www.w3.org/2000/svg\">\n";
        //const string SVG_STYLE = "<g stroke-width=\"2\" fill=\"none\">\n";

        string grid = khodMap.Grid();
        string body = string.Join("\n", nodes.Select(x => x.GetSVG()));


        const string SVG_FOOTER = "</svg>\n"; //</g>\n

        //return HTML_HEADER + SVG_HEADER + SVG_STYLE + grid + body + SVG_FOOTER + HTML_FOOTER;
        return SVG_HEADER + grid + body + SVG_FOOTER;
        //return SVG_HEADER + body + SVG_FOOTER;
    }
}