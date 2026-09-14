
using System;
using UnityEngine;
using UnityEngine.UIElements;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int health = 20;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackRange = 1.6f;
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
        if (autoStartingTTL)
            ApplyStartingTTL();
        Think();
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

        if (DistanceOnXZ(transform.position, targetObstacle.Position) <= attackRange)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                targetObstacle.TakeDamage(attackDamage);
                TakeDamage(targetObstacle.ContactDamage);
                attackTimer = attackInterval;
            }
        }
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
            if (!gm.IsWalkable(cell.x, cell.y))
                return false;
        }
        return true;
    }

    private void Think()
    {
        if (GridManager.Instance == null || agent == null) return;

        var gm = GridManager.Instance;
        Vector2Int start = gm.WorldToCell(transform.position);
        Vector2Int goal = gm.WorldToCell(exit.position);
        var path = gm.FindPath(start, goal);
        if (path != null)
        {
            targetObstacle = null;
            agent.isStopped = false;
            var worldPath = new System.Collections.Generic.List<Vector3>(path.Count);
            foreach (var c in path)
                worldPath.Add(gm.CellToWorld(c.x, c.y));
            agent.SetPath(worldPath);
            return;
        }

        targetObstacle = FindBlockingObstacle();
        if (targetObstacle == null)
            return;

        if (TryGetApproachPoint(targetObstacle, out Vector3 approach))
        {
            agent.isStopped = false;
            agent.SetPath(new System.Collections.Generic.List<Vector3> { approach });
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (root == null) return;
        ttlLabel = root.Q<Label>("ttl-label");
        if (ttlLabel != null)
            ttlLabel.text = ttl.ToString("F1");
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
                continue; // buried in the pile — skip

            if (DistanceOnXZ(approach, o.Position) > attackRange)
                continue; // rim is too far from this cube to hit it

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

    private bool TryGetApproachPoint(Obstacle obstacle, out Vector3 approach)
    {
        approach = default;
        var gm = GridManager.Instance;
        if (gm == null) return false;

        // sample multiple directions around the obstacle to find a nearby walkable cell
        int samples = 12;
        float sampleRadius = Mathf.Max(0.5f, agentRadius + attackRange * 0.5f);
        for (int i = 0; i < samples; i++)
        {
            float angle = (360f / samples) * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 candidate = obstacle.Position + dir * sampleRadius;
            Vector2Int cell = gm.WorldToCell(candidate);
            if (gm.IsWalkable(cell.x, cell.y))
            {
                approach = gm.CellToWorld(cell.x, cell.y);
                return true;
            }
        }

        return false;
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
}