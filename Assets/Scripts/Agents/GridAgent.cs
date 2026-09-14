using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class GridAgent : MonoBehaviour
{
    public float moveSpeed = 3f;
    public bool isStopped = false;
    public bool tickMode = false; // if true, speed is derived from tick interval + cell size instead of moveSpeed
    public float tickInterval = 0.5f; // used only if TickManager.Instance is unavailable
    [Min(1)] public int cellsPerMove = 1; // how many grid cells the agent covers per tick when tickMode is enabled
    public float arriveThreshold = 0.05f; // distance to a waypoint considered "arrived", allows early lookahead to next cell

    private List<Vector3> pathWorld = new List<Vector3>();
    private int pathIndex = 0;
    private Vector2Int? reservedCell;

    public bool HasPath => pathWorld != null && pathWorld.Count > 0 && pathIndex < pathWorld.Count;

    public void SetPath(List<Vector3> worldPositions)
    {
        pathWorld = worldPositions ?? new List<Vector3>();
        pathIndex = 0;
    }

    // Expose path for visualization
    public List<Vector3> GetPathWorld()
    {
        return pathWorld;
    }

    public void ClearPath()
    {
        pathWorld = new List<Vector3>();
        pathIndex = 0;
    }

    private void OnDisable()
    {
        ReleaseReservation();
    }

    private float CurrentSpeed()
    {
        if (!tickMode) return moveSpeed;
        var gm = GridManager.Instance;
        float interval = TickManager.Instance != null ? TickManager.Instance.tickInterval : tickInterval;
        float cellSize = gm != null ? gm.cellSize : 1f;
        int steps = Mathf.Max(1, cellsPerMove);
        return interval > 0f ? (cellSize * steps) / interval : moveSpeed;
    }

    private void Update()
    {
        if (isStopped) return;
        if (!HasPath) return;

        float speed = CurrentSpeed();
        float remaining = speed * Time.deltaTime;

        // Move through as many waypoints as we can this frame (lookahead), so the
        // agent keeps flowing forward instead of stalling exactly at each cell center.
        while (remaining > 0f && HasPath)
        {
            Vector3 target = pathWorld[pathIndex];
            var targetCell = GridManager.Instance != null ? GridManager.Instance.WorldToCell(target) : (Vector2Int?)null;

            // try to reserve the cell we're heading into; if another agent holds it, wait here
            if (targetCell.HasValue && !TryReserve(targetCell.Value))
            {
                return; // blocked by another agent occupying the next cell; try again next frame
            }

            Vector3 pos = transform.position;
            Vector3 toTarget = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            float dist = toTarget.magnitude;

            if (dist <= arriveThreshold || dist <= remaining)
            {
                transform.position = new Vector3(target.x, transform.position.y, target.z);
                remaining -= dist;
                Advance(targetCell);
                continue;
            }

            Vector3 move = toTarget.normalized * remaining;
            transform.position = transform.position + new Vector3(move.x, 0f, move.z);
            if (move.sqrMagnitude > 0.0001f)
                transform.forward = new Vector3(move.x, 0f, move.z).normalized;
            remaining = 0f;
        }
    }

    private bool TryReserve(Vector2Int cell)
    {
        var gm = GridManager.Instance;
        if (gm == null) return true;
        if (reservedCell.HasValue && reservedCell.Value == cell) return true;
        return gm.TryReserveCell(cell, this);
    }

    private void ReleaseReservation()
    {
        var gm = GridManager.Instance;
        if (gm != null && reservedCell.HasValue)
            gm.ReleaseCell(reservedCell.Value, this);
        reservedCell = null;
    }

    private void Advance(Vector2Int? arrivedCell)
    {
        // release the previous reservation now that we've physically left it
        var gm = GridManager.Instance;
        if (gm != null && reservedCell.HasValue && arrivedCell.HasValue && reservedCell.Value != arrivedCell.Value)
            gm.ReleaseCell(reservedCell.Value, this);
        reservedCell = arrivedCell;

        pathIndex++;
        if (pathIndex >= pathWorld.Count)
        {
            ClearPath();
        }
    }
}
