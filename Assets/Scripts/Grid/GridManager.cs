using System;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public Vector3 origin = Vector3.zero;
    public int width = 50;
    public int height = 50;
    public float cellSize = 1f;

    private GridCell[] cells;
    private readonly Dictionary<Vector2Int, GridAgent> occupants = new Dictionary<Vector2Int, GridAgent>();
    private readonly List<EnemySpawnPoint> spawnPointExclusions = new List<EnemySpawnPoint>();

    public event Action GridRebuilt;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        Instance = this;
        BuildGrid();
    }

    public void BuildGrid()
    {
        cells = new GridCell[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 world = CellToWorld(x, y);
                cells[x + y * width] = new GridCell(x, y, world);
            }
        }

        GridRebuilt?.Invoke();
    }

    public void RebuildGrid()
    {
        BuildGrid();
    }

    // Grows the grid to a new size while preserving the walkable state of
    // existing cells. Use this instead of setting width/height + RebuildGrid()
    // when the map is expanded at runtime (e.g. ExpandingIsland), otherwise
    // BuildGrid() wipes out obstacle blocking data placed by the player.
    public void GrowGrid(int newWidth, int newHeight)
    {
        newWidth = Mathf.Max(newWidth, width);
        newHeight = Mathf.Max(newHeight, height);
        if (newWidth == width && newHeight == height)
        {
            if (cells == null) BuildGrid();
            return;
        }

        var oldCells = cells;
        int oldWidth = width;
        int oldHeight = height;

        width = newWidth;
        height = newHeight;
        cells = new GridCell[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 world = CellToWorld(x, y);
                var cell = new GridCell(x, y, world);
                if (oldCells != null && x < oldWidth && y < oldHeight)
                    cell.walkable = oldCells[x + y * oldWidth].walkable;
                cells[x + y * width] = cell;
            }
        }

        GridRebuilt?.Invoke();
    }

    public GridCell GetCell(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return cells[x + y * width];
    }

    public bool IsWalkable(int x, int y)
    {
        var c = GetCell(x, y);
        return c != null && c.walkable;
    }

    public void SetWalkable(int x, int y, bool walkable)
    {
        var c = GetCell(x, y);
        if (c == null) return;
        c.walkable = walkable;
    }

    public Vector3 CellToWorld(int x, int y)
    {
        float wx = origin.x + (x + 0.5f) * cellSize;
        float wz = origin.z + (y + 0.5f) * cellSize;
        return new Vector3(wx, origin.y, wz);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector2Int c = WorldToCellUnclamped(world);
        c.x = Mathf.Clamp(c.x, 0, width - 1);
        c.y = Mathf.Clamp(c.y, 0, height - 1);
        return c;
    }

    // Same conversion as WorldToCell but without clamping to the grid
    // bounds. Use this whenever "is this actually inside the board" matters
    // (e.g. build placement footprint checks) - WorldToCell's clamping
    // silently snaps out-of-bounds points onto the nearest edge cell, which
    // would let part of a footprint hang off the board and still validate.
    public Vector2Int WorldToCellUnclamped(Vector3 world)
    {
        Vector3 local = world - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    public List<Vector2Int> GetNeighbors(Vector2Int cell)
    {
        var list = new List<Vector2Int>(4);
        // 4-directional
        Vector2Int[] deltas = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in deltas)
        {
            var n = cell + d;
            if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < height && IsWalkable(n.x, n.y))
                list.Add(n);
        }

        return list;
    }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        return AStarPathfinder.FindPath(start, goal, this);
    }

    public bool IsReachable(Vector2Int start, Vector2Int goal)
    {
        if (!InBounds(start) || !InBounds(goal)) return false;
        if (!IsWalkable(start.x, start.y) || !IsWalkable(goal.x, goal.y)) return false;
        if (start == goal) return true;

        var visited = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var n in GetNeighbors(current))
            {
                if (n == goal) return true;
                if (visited.Add(n))
                    queue.Enqueue(n);
            }
        }

        return false;
    }

    // Optional helper to set walkability by world bounds (for level setup)
    public void SetWalkableAtWorldPosition(Vector3 world, bool walkable)
    {
        var c = WorldToCell(world);
        SetWalkable(c.x, c.y, walkable);
    }

    // --- Cell reservation / occupancy (simple agent-to-agent avoidance) ---
    public bool IsCellReserved(Vector2Int cell, GridAgent requester)
    {
        return occupants.TryGetValue(cell, out var owner) && owner != null && owner != requester;
    }

    public bool TryReserveCell(Vector2Int cell, GridAgent agent)
    {
        if (IsCellReserved(cell, agent)) return false;
        occupants[cell] = agent;
        return true;
    }

    public void ReleaseCell(Vector2Int cell, GridAgent agent)
    {
        if (occupants.TryGetValue(cell, out var owner) && owner == agent)
            occupants.Remove(cell);
    }

    // --- Building placement helpers ---

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public List<Vector2Int> GetFootprintCells(Vector2Int origin, int sizeX, int sizeY)
    {
        var list = new List<Vector2Int>(Mathf.Max(1, sizeX * sizeY));
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                list.Add(new Vector2Int(origin.x + x, origin.y + y));
            }
        }
        return list;
    }

    // World-space center of a footprint starting at origin with the given size,
    // useful for centering a preview/instantiated prefab that spans multiple cells.
    public Vector3 FootprintCenterWorld(Vector2Int origin, int sizeX, int sizeY)
    {
        float wx = origin.x + sizeX * 0.5f;
        float wz = origin.y + sizeY * 0.5f;
        return new Vector3(this.origin.x + wx * cellSize, this.origin.y, this.origin.z + wz * cellSize);
    }

    public bool IsCellBuildable(Vector2Int cell)
    {
        if (!InBounds(cell)) return false;
        if (!IsWalkable(cell.x, cell.y)) return false;
        if (IsCellReserved(cell, null)) return false;

        if (Core.Instance != null)
        {
            var coreCell = WorldToCell(Core.Instance.transform.position);
            int dist = Mathf.Max(Mathf.Abs(cell.x - coreCell.x), Mathf.Abs(cell.y - coreCell.y));
            if (dist <= Core.Instance.ExclusionRadiusCells)
                return false;
        }

        foreach (var spawnPoint in spawnPointExclusions)
        {
            if (spawnPoint == null || !spawnPoint.IsActive) continue;
            var spawnCell = WorldToCell(spawnPoint.transform.position);
            int dist = Mathf.Max(Mathf.Abs(cell.x - spawnCell.x), Mathf.Abs(cell.y - spawnCell.y));
            if (dist <= spawnPoint.exclusionRadiusCells)
                return false;
        }

        return true;
    }

    // --- Enemy spawn point exclusion registration ---

    public void RegisterSpawnPointExclusion(EnemySpawnPoint spawnPoint)
    {
        if (spawnPoint == null || spawnPointExclusions.Contains(spawnPoint)) return;
        spawnPointExclusions.Add(spawnPoint);
    }

    public void UnregisterSpawnPointExclusion(EnemySpawnPoint spawnPoint)
    {
        spawnPointExclusions.Remove(spawnPoint);
    }

    public bool IsFootprintBuildable(Vector2Int origin, int sizeX, int sizeY)
    {
        foreach (var cell in GetFootprintCells(origin, sizeX, sizeY))
        {
            if (!IsCellBuildable(cell))
                return false;
        }
        return true;
    }

    // Overload for shape-based footprints (e.g. one BuildingShapeUnit marker
    // per occupied cell) rather than a plain rectangle.
    public bool IsFootprintBuildable(IEnumerable<Vector2Int> cells)
    {
        foreach (var cell in cells)
        {
            if (!IsCellBuildable(cell))
                return false;
        }
        return true;
    }
}