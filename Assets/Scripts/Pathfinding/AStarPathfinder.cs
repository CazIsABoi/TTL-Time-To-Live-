using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinder
{
    private class Node
    {
        public Vector2Int pos;
        public int g;
        public int f;
        public Node parent;
        public Node(Vector2Int p) { pos = p; }
    }

    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, GridManager grid)
    {
        if (grid == null) return null;
        if (start == goal) return new List<Vector2Int> { start };

        var open = new Dictionary<Vector2Int, Node>();
        var closed = new Dictionary<Vector2Int, Node>();

        Node startNode = new Node(start) { g = 0, f = Heuristic(start, goal) };
        open[start] = startNode;

        while (open.Count > 0)
        {
            // get lowest f
            Node current = null;
            foreach (var kv in open)
            {
                if (current == null || kv.Value.f < current.f) current = kv.Value;
            }

            if (current.pos == goal)
            {
                return ReconstructPath(current);
            }

            open.Remove(current.pos);
            closed[current.pos] = current;

            foreach (var npos in grid.GetNeighbors(current.pos))
            {
                if (closed.ContainsKey(npos)) continue;

                int tentativeG = current.g + 1; // uniform cost

                if (!open.TryGetValue(npos, out Node neighbor))
                {
                    neighbor = new Node(npos);
                    neighbor.parent = current;
                    neighbor.g = tentativeG;
                    neighbor.f = neighbor.g + Heuristic(npos, goal);
                    open[npos] = neighbor;
                }
                else if (tentativeG < neighbor.g)
                {
                    neighbor.parent = current;
                    neighbor.g = tentativeG;
                    neighbor.f = neighbor.g + Heuristic(npos, goal);
                }
            }
        }

        return null; // no path
    }

    private static List<Vector2Int> ReconstructPath(Node node)
    {
        var list = new List<Vector2Int>();
        Node cur = node;
        while (cur != null)
        {
            list.Add(cur.pos);
            cur = cur.parent;
        }
        list.Reverse();
        return list;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan
    }
}
