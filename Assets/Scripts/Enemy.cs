using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int health = 20;
    [SerializeField] private int attackDamage = 1;
    [SerializeField, Tooltip("Unused for obstacle smashing — smash attacks require a 4-direction adjacent cell.")]
    private float attackRange = 1.6f;
    [SerializeField] private float attackInterval = 0.6f;
    [SerializeField] private float repathInterval = 0.4f;
    [SerializeField] private float exitWeight = 1.2f;
    [SerializeField] private float agentWeight = 0.8f;
    [SerializeField] private float probeWeight = 1.5f;

    [SerializeField] private float agentRadius = 0.5f;
    private GridAgent agent;
    private Transform exit;
    [SerializeField] private float ttl = 0f;
    [SerializeField] private bool ttlTicks = false;
    [SerializeField] private bool tickMovement = true;
    [SerializeField, Min(1)] private int cellsPerMove = 1;

    [Header("Starting TTL")]
    [Tooltip("If on, Initialize() overwrites TTL from current map size.")]
    [SerializeField] private bool autoStartingTTL = true;
    [Tooltip("Measured open-lane speed. 50 tiles / 16 s = 3.125 tiles/s.")]
    [SerializeField] private float tilesPerSecond = 3.125f;
    [Tooltip("Path-length pad. 1.0 open lane, 1.25 light maze, 1.5 safer default.")]
    [SerializeField] private float pathScale = 1.5f;
    [Tooltip("Use 0.32*(C+R) instead of 0.32*C when the path may snake the full pad.")]
    [SerializeField] private bool includeRowsInPath = false;
    [SerializeField] private float ttlPadding = 0f;
    [SerializeField] private float minTTL = 1f;
    [SerializeField] private ParticleSystem destroyedParticles;
    [SerializeField] BreakableKind canBreak = BreakableKind.Loose;
    [SerializeField] float bashDistance = 0.28f;
    [SerializeField] float bashOutTime = 0.08f;
    [SerializeField] float bashBackTime = 0.12f;
    [SerializeField] Transform visual;
    [Tooltip("Added on top of the grid surface after snapping spawn to a cell center. Use this if the prefab pivot is not at the feet.")]
    [SerializeField] float spawnGroundOffset = 0f;

    float bashTimer;
    Vector3 bashHome;
    bool bashing;

    private float ttlTickTimer;
    private Label ttlLabel;
    private PanelRenderer panelRenderer;
    private Obstacle targetObstacle;
    private float repathTimer;
    private float attackTimer;
    private bool isDead;

    public event Action<Enemy> Died;

    private void Awake()
    {
        agent = GetComponent<GridAgent>();
        if (agent != null)
        {
            agent.tickMode = tickMovement;
            agent.cellsPerMove = cellsPerMove;
        }

        ttlTickTimer = TickManager.Instance != null ? TickManager.Instance.tickInterval : 0.5f;

        // try locate UI Toolkit label (PanelRenderer or UIDocument)
        panelRenderer = GetComponentInChildren<PanelRenderer>();
    }

    private void OnEnable()
    {
        if (panelRenderer != null)
            panelRenderer.RegisterUIReloadCallback(OnUIReload);

        Obstacle.Changed += OnWorldChanged;
    }

    private void OnDisable()
    {
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        Obstacle.Changed -= OnWorldChanged;
    }

    public void Initialize(Transform targetTransform)
    {
        exit = targetTransform;
        SnapSpawnToGrid();
        if (autoStartingTTL)
            ApplyStartingTTL();
        Think();
    }

    // Spawn transforms are often at ground height with a centered mesh pivot,
    // and rarely sit on an exact cell center. Snap XZ to the cell and lift the
    // renderer onto the surface so the first move is a full tile at tick speed.
    void SnapSpawnToGrid()
    {
        var gm = GridManager.Instance;
        if (gm == null) return;

        Vector2Int cell = gm.WorldToCell(transform.position);
        Vector3 center = gm.CellToWorld(cell.x, cell.y);

        transform.position = new Vector3(center.x, transform.position.y, center.z);

        var rends = GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null)
                    b.Encapsulate(rends[i].bounds);
            }

            float delta = (center.y + spawnGroundOffset) - b.min.y;
            transform.position += Vector3.up * delta;
        }
        else
        {
            transform.position = new Vector3(center.x, center.y + spawnGroundOffset, center.z);
        }
    }

    /// <summary>
    /// Open-lane time is C / tilesPerSecond (= 0.32*C at 3.125 tiles/s).
    /// Buildings force detours, so multiply by pathScale (default 1.5 → 0.48*C).
    /// </summary>
    public static float ComputeStartingTTL(
        int columns,
        int rows = 0,
        float tilesPerSecond = 3.125f,
        float pathScale = 1.5f,
        bool includeRows = false,
        float padding = 0f,
        float minTTL = 1f)
    {
        float speed = Mathf.Max(0.01f, tilesPerSecond);
        float walk = Mathf.Max(1, columns);
        if (includeRows)
            walk += Mathf.Max(0, rows);

        float seconds = (walk / speed) * Mathf.Max(0.01f, pathScale) + padding;
        return Mathf.Max(minTTL, seconds);
    }

    public static bool TryGetMapSize(out int columns, out int rows)
    {
        if (ExpandingIsland.Instance != null)
        {
            columns = ExpandingIsland.Instance.Columns;
            rows = ExpandingIsland.Instance.Rows;
            return true;
        }

        var gm = GridManager.Instance;
        if (gm != null)
        {
            columns = gm.width;
            rows = gm.height;
            return true;
        }

        columns = 0;
        rows = 0;
        return false;
    }

    public float ApplyStartingTTL()
    {
        if (!TryGetMapSize(out int columns, out int rows))
            return ttl;

        float computed = ComputeStartingTTL(
            columns,
            rows,
            tilesPerSecond,
            pathScale,
            includeRowsInPath,
            ttlPadding,
            minTTL);

        SetTTL(computed);
        return computed;
    }
    void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (root == null) return;
        ttlLabel = root.Q<Label>("ttl-label");
        if (ttlLabel != null)
            ttlLabel.text = Mathf.Max(0f, ttl).ToString("F1");
    }

    private void Update()
    {
        if (isDead || exit == null || GridManager.Instance == null || agent == null) return;

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            // Only replan if we don't currently have a path, or if the path we have
            // is no longer valid (e.g. a cell became blocked). Replanning unconditionally
            // resets the agent back to its current cell center every interval, which
            // causes a visible rubberband/snap-back effect.
            if (!agent.HasPath || !IsPathStillValid())
                Think();
        }

        // TTL handling
        if (ttl > 0f)
        {
            if (ttlTicks)
            {
                float interval = TickManager.Instance != null ? TickManager.Instance.tickInterval : 0.5f;
                ttlTickTimer -= Time.deltaTime;
                if (ttlTickTimer <= 0f)
                {
                    ttlTickTimer = interval;
                    ttl -= interval;
                }
            }
            else
            {
                ttl -= Time.deltaTime;
            }

            if (ttlLabel != null)
                ttlLabel.text = Mathf.Max(0f, ttl).ToString("F1");
            if (ttl <= 0f) Die();
        }

        if (targetObstacle == null) return;

        // Smash only from a 4-directional neighboring cell, never diagonal or from range.
        if (!IsOrthogonallyAdjacentTo(targetObstacle))
            return;

        if (!agent.isStopped)
            agent.isStopped = true;

        if (bashing) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;
        attackTimer = attackInterval;

        StartCoroutine(BashThenHit(targetObstacle));
    }

    private void OnWorldChanged()
    {
        targetObstacle = null;
        if (agent != null)
        {
            agent.isStopped = false;
            Think();
        }
    }

    private bool IsPathStillValid()
    {
        var gm = GridManager.Instance;
        if (gm == null) return false;
        var path = agent.GetPathWorld();
        if (path == null || path.Count == 0) return false;

        foreach (var worldPos in path)
        {
            var cell = gm.WorldToCell(worldPos);
            if (gm.IsPathable(cell, canBreak))
                continue;
            return false;
        }
        return true;
    }

    void Think()
    {
        if (GridManager.Instance == null || agent == null) return;
        var gm = GridManager.Instance;
        Vector2Int start = gm.WorldToCell(transform.position);
        Vector2Int goal = gm.WorldToCell(exit.position);

        var path = gm.FindPath(start, goal, canBreak);
        if (path != null && path.Count > 0)
        {
            Obstacle firstBreak = FirstBreakableOnPath(gm, path);
            if (firstBreak != null)
            {
                targetObstacle = firstBreak;
                agent.isStopped = false;
                var worldPath = WalkablePrefixWorld(gm, path, firstBreak);
                if (TryGetApproachPoint(firstBreak, out Vector3 approach))
                {
                    if (worldPath.Count == 0 || DistanceOnXZ(worldPath[worldPath.Count - 1], approach) > 0.15f)
                        worldPath.Add(approach);
                }
                else if (worldPath.Count == 0)
                {
                    worldPath.Add(firstBreak.Position);
                }
                agent.SetPath(worldPath);
                return;
            }

            targetObstacle = null;
            agent.isStopped = false;
            var worldPathOpen = new List<Vector3>(path.Count);
            foreach (var c in path)
                worldPathOpen.Add(gm.CellToWorld(c.x, c.y));
            agent.SetPath(worldPathOpen);
            return;
        }

        if (TryFindSmashTarget(out Obstacle smash, out Vector3 rim))
        {
            targetObstacle = smash;
            agent.isStopped = false;
            agent.SetPath(new List<Vector3> { rim });
        }
    }

    Obstacle FirstBreakableOnPath(GridManager gm, List<Vector2Int> path)
    {
        if (path == null) return null;
        for (int i = 0; i < path.Count; i++)
        {
            var cell = path[i];
            if (gm.IsWalkable(cell.x, cell.y)) continue;
            if (gm.TryGetObstacle(cell, out var obstacle) && obstacle != null && obstacle.CanBeBrokenBy(canBreak))
                return obstacle;
        }
        return null;
    }

    List<Vector3> WalkablePrefixWorld(GridManager gm, List<Vector2Int> path, Obstacle smash)
    {
        var world = new List<Vector3>();
        if (path == null) return world;

        for (int i = 0; i < path.Count; i++)
        {
            var cell = path[i];
            if (gm.TryGetObstacle(cell, out var obstacle) && obstacle == smash)
                break;
            if (gm.IsWalkable(cell.x, cell.y))
                world.Add(gm.CellToWorld(cell.x, cell.y));
        }
        return world;
    }

    bool TryFindSmashTarget(out Obstacle best, out Vector3 approach)
    {
        best = null;
        approach = default;
        if (canBreak == BreakableKind.None || exit == null) return false;

        Vector3 toExit = exit.position - transform.position;
        toExit.y = 0f;
        float bestScore = float.MaxValue;

        for (int i = 0; i < Obstacle.All.Count; i++)
        {
            Obstacle o = Obstacle.All[i];
            if (o == null || !o.CanBeBrokenBy(canBreak)) continue;
            if (!TryGetApproachPoint(o, out Vector3 rim)) continue;

            Vector3 toObs = o.Position - transform.position;
            toObs.y = 0f;
            if (toExit.sqrMagnitude > 0.01f && Vector3.Dot(toExit.normalized, toObs) < 0.15f)
                continue; // behind them / already passed

            float score =
                DistanceOnXZ(transform.position, o.Position) * agentWeight +
                DistanceOnXZ(o.Position, exit.position) * exitWeight;
            if (score < bestScore)
            {
                bestScore = score;
                best = o;
                approach = rim;
            }
        }

        return best != null;
    }

    private Obstacle FindBlockingObstacle()
    {
        Vector3 probe = exit != null ? exit.position : transform.position;
        Vector3 toExit = exit.position - transform.position;
        toExit.y = 0f;

        Obstacle best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < Obstacle.All.Count; i++)
        {
            Obstacle o = Obstacle.All[i];
            if (o == null) continue;

            if (!TryGetApproachPoint(o, out Vector3 approach))
                continue; // no orthogonal walkable cell to stand on

            Vector3 toObs = o.Position - transform.position;
            toObs.y = 0f;
            if (toExit.sqrMagnitude > 0.01f && Vector3.Dot(toExit.normalized, toObs) < 0f)
                continue;

            float score =
                DistanceOnXZ(o.Position, exit.position) * exitWeight +
                DistanceOnXZ(o.Position, transform.position) * agentWeight +
                DistanceOnXZ(probe, o.Position) * probeWeight;

            if (score < bestScore)
            {
                bestScore = score;
                best = o;
            }
        }

        return best;
    }

    static readonly Vector2Int[] OrthoDeltas =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    bool IsOrthogonallyAdjacentTo(Obstacle obstacle)
    {
        var gm = GridManager.Instance;
        if (gm == null || obstacle == null) return false;
        return IsOrthogonallyAdjacent(gm.WorldToCell(transform.position), obstacle);
    }

    bool IsOrthogonallyAdjacent(Vector2Int from, Obstacle obstacle)
    {
        foreach (var cell in GetOccupiedCells(obstacle))
        {
            if (Mathf.Abs(from.x - cell.x) + Mathf.Abs(from.y - cell.y) == 1)
                return true;
        }
        return false;
    }

    static IEnumerable<Vector2Int> GetOccupiedCells(Obstacle obstacle)
    {
        if (obstacle == null) yield break;

        var placed = obstacle.GetComponent<PlacedBuilding>();
        if (placed != null && placed.cells != null && placed.cells.Count > 0)
        {
            for (int i = 0; i < placed.cells.Count; i++)
                yield return placed.cells[i];
            yield break;
        }

        var gm = GridManager.Instance;
        if (gm != null)
            yield return gm.WorldToCell(obstacle.Position);
    }

    private bool TryGetApproachPoint(Obstacle obstacle, out Vector3 approach)
    {
        approach = default;
        var gm = GridManager.Instance;
        if (gm == null || obstacle == null) return false;

        Vector2Int self = gm.WorldToCell(transform.position);
        Vector2Int bestCell = default;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var occupied in GetOccupiedCells(obstacle))
        {
            for (int i = 0; i < OrthoDeltas.Length; i++)
            {
                Vector2Int n = occupied + OrthoDeltas[i];
                if (!gm.IsWalkable(n.x, n.y)) continue;

                float dist = Mathf.Abs(self.x - n.x) + Mathf.Abs(self.y - n.y);
                if (dist >= bestDist) continue;

                bestDist = dist;
                bestCell = n;
                found = true;
            }
        }

        if (!found) return false;
        approach = gm.CellToWorld(bestCell.x, bestCell.y);
        return true;
    }

    private static float DistanceOnXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        health -= damage;
        if (health <= 0) Die();
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        Died?.Invoke(this);
        ParticleSystem particles = Instantiate(destroyedParticles, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    public float SetTTL(float newTTL)
    {
        float oldTTL = ttl;
        ttl = newTTL;
        if (ttlLabel != null)
            ttlLabel.text = Mathf.Max(0f, ttl).ToString("F1");
        return oldTTL;
    }

    IEnumerator BashThenHit(Obstacle obstacle)
    {
        if (obstacle == null) yield break;
        bashing = true;

        Transform xf = visual != null ? visual : transform;
        bashHome = xf.localPosition;

        Vector3 worldToward = obstacle.Position - transform.position;
        worldToward.y = 0f;
        if (worldToward.sqrMagnitude < 0.0001f)
            worldToward = transform.forward;
        Vector3 localToward = xf.parent != null
            ? xf.parent.InverseTransformDirection(worldToward.normalized)
            : worldToward.normalized;

        float t = 0f;
        Vector3 outPos = bashHome + localToward * bashDistance;
        while (t < bashOutTime)
        {
            t += Time.deltaTime;
            xf.localPosition = Vector3.Lerp(bashHome, outPos, t / bashOutTime);
            yield return null;
        }

        if (obstacle != null)
            obstacle.TakeDamage(attackDamage);

        t = 0f;
        Vector3 from = xf.localPosition;
        while (t < bashBackTime)
        {
            t += Time.deltaTime;
            xf.localPosition = Vector3.Lerp(from, bashHome, t / bashBackTime);
            yield return null;
        }

        xf.localPosition = bashHome;
        bashing = false;
    }
}