using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(Transform))]
public class GridAgent : MonoBehaviour
{
    public float moveSpeed = 3f;
    public bool isStopped = false;
    public bool tickMode = false;
    public float tickInterval = 0.5f;
    [Min(1)] public int cellsPerMove = 1;
    public float arriveThreshold = 0.05f;
    [Header("Agent crowding")]
    [Tooltip("How long to wait on a reserved cell before forcing through.")]
    public float blockPatience = 0.45f;
    [Tooltip("After forcing, briefly ignore yielding so the pack can spread.")]
    public float forceCooldown = 0.12f;
    [Tooltip("Don't enter the next cell if another agent is closer than this (world units) ahead.")]
    public float followDistance = 0.75f;
    private List<Vector3> pathWorld = new List<Vector3>();
    private int pathIndex = 0;
    private Vector2Int? reservedCell;
    private float blockWaitTimer;
    private float forceCooldownTimer;
    private bool waitingOnAgent;
    public bool HasPath => pathWorld != null && pathWorld.Count > 0 && pathIndex < pathWorld.Count;
    public bool IsWaitingOnAgent => waitingOnAgent;
    public void SetPath(List<Vector3> worldPositions)
    {
        ReleaseReservation();
        pathWorld = worldPositions ?? new List<Vector3>();
        pathIndex = 0;
        blockWaitTimer = 0f;
        waitingOnAgent = false;
        forceCooldownTimer = 0f;
    }
    public List<Vector3> GetPathWorld() => pathWorld;
    public void ClearPath()
    {
        ReleaseReservation();
        pathWorld = new List<Vector3>();
        pathIndex = 0;
        waitingOnAgent = false;
        blockWaitTimer = 0f;
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
        if (forceCooldownTimer > 0f)
            forceCooldownTimer -= Time.deltaTime;
        if (isStopped) return;
        if (!HasPath) return;
        float speed = CurrentSpeed();
        float remaining = speed * Time.deltaTime;
        while (remaining > 0f && HasPath)
        {
            Vector3 target = pathWorld[pathIndex];
            var targetCell = GridManager.Instance != null
                ? GridManager.Instance.WorldToCell(target)
                : (Vector2Int?)null;
            // Soft spacing: don't crowd whoever is already near this waypoint.
            if (IsTooCloseToOccupant(target, targetCell))
            {
                waitingOnAgent = true;
                return;
            }
            if (targetCell.HasValue && forceCooldownTimer <= 0f)
            {
                if (!TryReserve(targetCell.Value))
                {
                    waitingOnAgent = true;
                    blockWaitTimer += Time.deltaTime;
                    if (blockWaitTimer < blockPatience)
                        return; // polite wait
                    // Patience out: force through this cell.
                    ForceReserve(targetCell.Value);
                    blockWaitTimer = 0f;
                    forceCooldownTimer = forceCooldown;
                }
                else
                {
                    blockWaitTimer = 0f;
                }
            }
            waitingOnAgent = false;
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
    private bool IsTooCloseToOccupant(Vector3 target, Vector2Int? targetCell)
    {
        var gm = GridManager.Instance;
        if (gm == null || followDistance <= 0f || !targetCell.HasValue)
            return false;
        // If someone else already reserved the next cell and is still near it, keep spacing.
        if (!gm.IsCellReserved(targetCell.Value, this))
            return false;
        // Find that agent via reservation map isn't exposed; approximate by distance to cell center.
        // Prefer not entering while reserved AND we're still farther than followDistance from the waypoint.
        Vector3 pos = transform.position;
        Vector3 flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
        return flat.magnitude > arriveThreshold; // reserved ahead → wait (patience/force handles unjam)
    }
    private bool TryReserve(Vector2Int cell)
    {
        var gm = GridManager.Instance;
        if (gm == null) return true;
        if (reservedCell.HasValue && reservedCell.Value == cell) return true;
        return gm.TryReserveCell(cell, this);
    }
    private void ForceReserve(Vector2Int cell)
    {
        var gm = GridManager.Instance;
        if (gm == null) return;
        // Steal: release whoever was there is handled by overwriting in TryReserve-style API.
        gm.ForceReserveCell(cell, this);
        reservedCell = cell;
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
        var gm = GridManager.Instance;
        if (gm != null && reservedCell.HasValue && arrivedCell.HasValue && reservedCell.Value != arrivedCell.Value)
            gm.ReleaseCell(reservedCell.Value, this);
        reservedCell = arrivedCell;
        pathIndex++;
        if (pathIndex >= pathWorld.Count)
            ClearPath();
    }
}