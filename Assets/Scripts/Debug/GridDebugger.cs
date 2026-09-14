using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class GridDebugger : MonoBehaviour
{
    [Header("Grid")]
    public bool showGrid = true;
    public Color gridColor = Color.cyan;
    [Tooltip("Draw a solid, semi-transparent quad on top of each cell (in addition to the wire cube) so blocked/walkable state is obvious from above.")]
    public bool fillCells = true;
    [Range(0f, 1f)] public float fillAlpha = 0.35f;
    public Color walkableFillColor = new Color(0f, 1f, 0f, 1f);
    public Color blockedFillColor = new Color(1f, 0f, 0f, 1f);
    [Tooltip("Draw the col,row coordinates on top of each cell. Can be slow on large grids.")]
    public bool showCoordLabels = false;

    [Header("Obstacles")]
    [Tooltip("Draw a marker at every live Obstacle's transform.position and highlight the grid cell GridManager thinks it occupies. Useful for spotting a world<->cell mismatch.")]
    public bool showObstacles = true;
    public Color obstacleMarkerColor = Color.yellow;

    [Header("Diagnostics")]
    [Tooltip("If ExpandingIsland references a different GridManager than GridManager.Instance, that mismatch is the classic cause of tiles/obstacles looking offset or enemies ignoring blocked cells.")]
    public bool warnOnGridMismatch = true;

    private void OnDrawGizmos()
    {
        if (!showGrid) return;
        var gm = GridManager.Instance;
        if (gm == null) return;

        for (int y = 0; y < gm.height; y++)
        {
            for (int x = 0; x < gm.width; x++)
            {
                var cell = gm.GetCell(x, y);
                if (cell == null) continue;
                Vector3 center = cell.worldPosition;
                Vector3 size = new Vector3(gm.cellSize * 0.9f, 0.01f, gm.cellSize * 0.9f);

                if (fillCells)
                {
                    Color fill = cell.walkable ? walkableFillColor : blockedFillColor;
                    fill.a = fillAlpha;
                    Gizmos.color = fill;
                    Gizmos.DrawCube(center, size);
                }

                Gizmos.color = cell.walkable ? gridColor : Color.red;
                Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
                if (showCoordLabels)
                    UnityEditor.Handles.Label(center + Vector3.up * 0.1f, $"{x},{y}");
#endif
            }
        }

        if (showObstacles)
            DrawObstacles(gm);

#if UNITY_EDITOR
        if (warnOnGridMismatch)
            DrawGridMismatchWarning(gm);
#endif
    }

    private static void DrawObstacles(GridManager gm)
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i < Obstacle.All.Count; i++)
        {
            var o = Obstacle.All[i];
            if (o == null) continue;

            Vector3 pos = o.Position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, 0.15f);

            // Where GridManager thinks this obstacle sits. If this marker is
            // not directly under the obstacle, world<->cell math (origin/cellSize)
            // is out of sync between whatever placed the obstacle and GridManager.
            Vector2Int cell = gm.WorldToCell(pos);
            Vector3 cellCenter = gm.CellToWorld(cell.x, cell.y);
            bool blocked = !gm.IsWalkable(cell.x, cell.y);
            Gizmos.color = blocked ? Color.green : Color.magenta; // magenta = obstacle NOT marked as blocking!
            Gizmos.DrawLine(pos, cellCenter);
            Gizmos.DrawWireCube(cellCenter, new Vector3(gm.cellSize, 0.02f, gm.cellSize));
        }
    }

#if UNITY_EDITOR
    private void DrawGridMismatchWarning(GridManager gm)
    {
        var island = ExpandingIsland.Instance;
        if (island == null || island.grid == null) return;
        if (island.grid == gm) return;

        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.Label(gm.transform.position + Vector3.up * 2f,
            "MISMATCH: ExpandingIsland.grid != GridManager.Instance\n" +
            "Tiles are placed using one GridManager while pathing/obstacles use another.");
    }
#endif
}
