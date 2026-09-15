using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual drop-in for an existing GridManager.
/// Does not own cells, pathing, placement, or gizmos.
/// GridDebugger draws the grid. This script only lands tiles, core, and the enemy spawner
/// on GridManager.CellToWorld.
/// </summary>
public class ExpandingIsland : MonoBehaviour
{
    public static ExpandingIsland Instance { get; private set; }

    public enum BoardEnd { Left, Right }

    [Header("Grid")]
    [Tooltip("Leave empty to use GridManager.Instance.")]
    public GridManager grid;
    [Tooltip("Fallback only if no GridManager is present.")]
    public int columns = 8;
    public int rows = 4;
    [Tooltip("Expand() increases GridManager.width, RebuildGrid(), then lands the new strip.")]
    public bool growGridOnExpand = true;

    [Header("Tiles")]
    public GameObject[] tilePrefabs;
    public float[] tilePrefabYOffsets;
    public float tileScale = 1f;
    public float tileHeightScale = 1f;
    public float tileYOffset = 0f;
    public bool randomTileYaw;
    public int seed;

    [Header("Dirt")]
    [Tooltip("Placed under every tile so nothing shows through if a tile ever shifts or falls out of place.")]
    public GameObject dirtPrefab;
    [Tooltip("How far below the tile's resting position the dirt block sits.")]
    public float dirtThickness = 0.5f;
    [Tooltip("Delay after the dirt starts falling before the tile on top starts falling.")]
    public float dirtDropLead = 0.12f;
    public AudioClip dirtClip;
    public AudioSource dirtSource;

    [Header("Ends")]
    public Transform corePiece;
    public Transform enemySpawner;
    public BoardEnd coreEnd = BoardEnd.Left;
    public BoardEnd spawnEnd = BoardEnd.Right;
    public float pieceY = 0.35f;

    [Header("Drop-in")]
    public bool dropFromSky = true;
    public float dropHeight = 18f;
    public float dropDuration = 1.15f;
    public float dropStagger = 0.05f;
    public AudioClip tileClip;
    public AudioClip stampClip;
    public AudioSource tileSource;
    public AudioSource stampSource;

    [Header("Expand")]
    public int expandColumns = 2;

    [Header("World setup")]
    [Tooltip("If true, Start() waits until RunOverlayUI calls BeginGeneration() after size pick.")]
    public bool waitForWorldSetup = true;

    public bool Ready { get; private set; }
    bool generationStarted;
    public int Columns => grid != null ? Mathf.Max(1, grid.width) : Mathf.Max(1, columns);
    public int Rows => grid != null ? Mathf.Max(1, grid.height) : Mathf.Max(1, rows);

    public event Action OnReady;
    public event Action<int> OnExpanded;

    class Tile
    {
        public int col, row;
        public Transform transform;
        public Renderer renderer;
        public Vector3 rest;
        public Transform dirtTransform;
        public Renderer dirtRenderer;
        public Vector3 dirtRest;
    }

    readonly List<Tile> tiles = new();
    bool busy;
    Vector3 coreRest;
    Vector3 spawnRest;

    void Awake()
    {
        Instance = this;
        SyncGridReference();
        if (RunSeed.Instance != null)
            seed = RunSeed.Instance.Seed;
        else if (seed == 0)
            seed = UnityEngine.Random.Range(1, int.MaxValue);
        SyncSizeFromGrid();
        SyncPrefabOffsets();
    }



    void Start()
    {
        if (waitForWorldSetup)
        {
            // Keep core / spawner invisible during World Setup (scene poses sit in camera view).
            SetEndVisible(corePiece, false);
            SetEndVisible(enemySpawner, false);
            return;
        }
        BeginGeneration();
    }

    /// <summary>Apply grid size before generation (world setup).</summary>
    public void ConfigureSize(int cols, int rows)
    {
        SyncGridReference();
        cols = Mathf.Max(1, cols);
        rows = Mathf.Max(1, rows);
        if (grid != null)
        {
            grid.SetSize(cols, rows);
        }
        else
        {
            columns = cols;
            this.rows = rows;
        }
        SyncSizeFromGrid();
    }

    /// <summary>Start dirt drop / intro. Safe to call once after ConfigureSize.</summary>
    public void BeginGeneration()
    {
        if (generationStarted) return;
        generationStarted = true;
        if (RunSeed.Instance != null)
            seed = RunSeed.Instance.Seed;
        SyncGridReference();
        SyncSizeFromGrid();
        ClearGround();
        CacheEnds();
        HideEndsInSky();
        SpawnRange(0, Columns - 1, dropFromSky);
        StartCoroutine(Intro());
    }

    void OnValidate()
    {
        SyncGridReference();
        SyncSizeFromGrid();
        SyncPrefabOffsets();
    }

    // The singleton GridManager.Instance is what pathing, obstacles, and
    // GridDebugger all use. If the Inspector-assigned `grid` field points at
    // a *different* GridManager (e.g. a leftover duplicate in the scene),
    // tiles get placed relative to one grid while pathfinding/obstacles run
    // on another - causing a fixed visual offset and enemies that appear to
    // ignore obstacles. Always defer to the singleton when one exists.
    void SyncGridReference()
    {
        if (GridManager.Instance == null)
        {
            if (grid == null) return;
        }
        else if (grid != null && grid != GridManager.Instance)
        {
            Debug.LogWarning(
                $"ExpandingIsland: 'grid' was assigned to '{grid.name}' but GridManager.Instance is " +
                $"'{GridManager.Instance.name}'. Using GridManager.Instance so tiles stay in sync with " +
                "pathfinding/obstacles. Clear or fix the 'grid' field in the Inspector to remove this warning.",
                this);
            grid = GridManager.Instance;
        }
        else if (grid == null)
        {
            grid = GridManager.Instance;
        }
    }

    void SyncSizeFromGrid()
    {
        if (grid == null)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            return;
        }
        columns = Mathf.Max(1, grid.width);
        rows = Mathf.Max(1, grid.height);
    }

    void SyncPrefabOffsets()
    {
        int n = tilePrefabs != null ? tilePrefabs.Length : 0;
        if (n == 0)
        {
            tilePrefabYOffsets = tilePrefabYOffsets ?? new float[0];
            return;
        }
        if (tilePrefabYOffsets != null && tilePrefabYOffsets.Length == n)
            return;
        var next = new float[n];
        if (tilePrefabYOffsets != null)
        {
            int copy = Mathf.Min(n, tilePrefabYOffsets.Length);
            for (int i = 0; i < copy; i++)
                next[i] = tilePrefabYOffsets[i];
        }
        tilePrefabYOffsets = next;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Expand() => Expand(expandColumns);

    public void Expand(int addColumns)
    {
        if (busy || !Ready) return;
        StartCoroutine(ExpandRoutine(Mathf.Max(1, addColumns)));
    }

    public float EnemyStartingTTL(float pathScale = 1.5f, bool includeRows = false, float tilesPerSecond = 3.125f)
    {
        return Enemy.ComputeStartingTTL(Columns, Rows, tilesPerSecond, pathScale, includeRows);
    }

    IEnumerator Intro()
    {
        busy = true;
        Ready = false;
        HideEndsInSky();
        yield return null;
        if (dropFromSky)
            yield return DropColumns(0, Columns - 1);
        yield return DropEnds(firstTime: true);
        busy = false;
        Ready = true;
        OnReady?.Invoke();
    }

    IEnumerator ExpandRoutine(int add)
    {
        busy = true;
        Ready = false;

        int firstNew = Columns;
        if (growGridOnExpand && grid != null)
        {
            grid.GrowGrid(firstNew + add, Mathf.Max(grid.height, Rows));
        }
        else
        {
            columns = firstNew + add;
        }
        SyncSizeFromGrid();
        SpawnRange(firstNew, Columns - 1, true);
        CacheEnds();
        HideSpawnerInSky();
        yield return DropColumns(firstNew, Columns - 1);
        yield return DropPiece(enemySpawner, spawnRest, EndColumn(spawnEnd), MidRow);

        busy = false;
        Ready = true;
        OnExpanded?.Invoke(add);
    }

    List<Tile> SpawnRange(int colMin, int colMax, bool hiddenHigh)
    {
        var made = new List<Tile>();
        int w = Columns;
        int h = Rows;
        colMin = Mathf.Clamp(colMin, 0, w - 1);
        colMax = Mathf.Clamp(colMax, 0, w - 1);
        for (int c = colMin; c <= colMax; c++)
            for (int r = 0; r < h; r++)
            {
                if (HasTile(c, r)) continue;
                var t = SpawnTile(c, r, hiddenHigh);
                tiles.Add(t);
                made.Add(t);
            }
        return made;
    }

    bool HasTile(int col, int row)
    {
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].col == col && tiles[i].row == row) return true;
        return false;
    }

    Tile SpawnTile(int col, int row, bool hiddenHigh)
    {
        Vector3 rest = CellWorld(col, row);
        GameObject go = MakeTileObject(col, row, rest, out float prefabY);
        rest.y += tileYOffset + prefabY;
        go.name = $"Ground_{col}_{row}";
        go.transform.SetParent(transform, true);
        go.transform.rotation = randomTileYaw ? QuarterYaw(col, row, seed + 21) : Quaternion.identity;
        go.transform.position = rest;

        Vector3 scale = go.transform.localScale;
        scale *= tileScale;
        scale.y *= tileHeightScale;
        go.transform.localScale = scale;

        // GrassFloor1 (and similar Blender exports) keep transform at 0,0,0 but the
        // mesh/collider is shifted in local space. Snap the *visual* XZ center to
        // GridManager.CellToWorld. Core and spawner are not snapped â€” they use CellToWorld
        // as their transform so they stay on the same cells as pathing / GridDebugger.
        rest = SnapVisualCenterToCell(go, rest);

        go.transform.position = hiddenHigh ? rest + Vector3.up * dropHeight : rest;

        foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (go.GetComponentInChildren<Collider>() == null)
        {
            var box = go.AddComponent<BoxCollider>();
            float cs = grid != null ? grid.cellSize : 1f;
            box.size = new Vector3(cs, Mathf.Max(0.12f, cs * 0.2f), cs);
        }

        var rend = go.GetComponentInChildren<Renderer>();
        if (hiddenHigh && rend != null)
            rend.enabled = false;

        // Dirt stays on the shared ground plane. Per-prefab Y offsets only lift the
        // grass mesh (Blender pivot fixes) — including them here made dirt stair-step.
        float sharedGroundY = CellWorld(col, row).y + tileYOffset;
        Vector3 dirtRest = new Vector3(rest.x, sharedGroundY - dirtThickness, rest.z);
        Transform dirtXf = null;
        Renderer dirtRend = null;
        if (dirtPrefab != null)
        {
            GameObject dirtGo = Instantiate(dirtPrefab, dirtRest, Quaternion.identity);
            dirtGo.name = $"Dirt_{col}_{row}";
            dirtGo.transform.SetParent(transform, true);
            dirtGo.transform.position = hiddenHigh ? dirtRest + Vector3.up * dropHeight : dirtRest;

            foreach (var rb in dirtGo.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            dirtRend = dirtGo.GetComponentInChildren<Renderer>();
            if (hiddenHigh && dirtRend != null)
                dirtRend.enabled = false;

            dirtXf = dirtGo.transform;
        }

        return new Tile
        {
            col = col,
            row = row,
            transform = go.transform,
            renderer = rend,
            rest = rest,
            dirtTransform = dirtXf,
            dirtRenderer = dirtRend,
            dirtRest = dirtRest
        };
    }

    static Vector3 SnapVisualCenterToCell(GameObject go, Vector3 cellWorld)
    {
        // Prefer an explicit BuildingShapeUnit marker (artist-defined center)
        // when the prefab has one. Otherwise fall back to the *visual mesh*
        // bounds, not the collider - a Blender-exported prefab (e.g.
        // GrassFloor1) commonly has its BoxCollider centered on the object's
        // origin/pivot while the mesh itself is offset from that pivot, so
        // snapping to the collider leaves the visible tile misaligned.
        Vector3 visualCenter;
        if (!BuildingShapeUnit.TryGetCenter(go, out visualCenter))
        {
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend == null) return cellWorld;
            visualCenter = rend.bounds.center;
        }

        Vector3 delta = new Vector3(cellWorld.x - visualCenter.x, 0f, cellWorld.z - visualCenter.z);
        go.transform.position += delta;
        Vector3 p = go.transform.position;
        return new Vector3(p.x, cellWorld.y, p.z);
    }

    GameObject MakeTileObject(int col, int row, Vector3 rest, out float prefabY)
    {
        GameObject prefab = PickPrefab(col, row, out prefabY);
        if (prefab != null)
            return Instantiate(prefab, rest, Quaternion.identity);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "GroundTile";
        cube.transform.position = rest;
        cube.transform.localScale = new Vector3(0.98f, 0.12f, 0.98f);
        return cube;
    }

    GameObject PickPrefab(int col, int row, out float prefabY)
    {
        prefabY = 0f;
        if (tilePrefabs == null || tilePrefabs.Length == 0) return null;
        int n = 0;
        for (int i = 0; i < tilePrefabs.Length; i++)
            if (tilePrefabs[i] != null) n++;
        if (n == 0) return null;
        int pick = Mathf.Abs(HashInt(col, row, seed)) % n;
        for (int i = 0; i < tilePrefabs.Length; i++)
        {
            if (tilePrefabs[i] == null) continue;
            if (pick == 0)
            {
                prefabY = PrefabYOffset(i);
                return tilePrefabs[i];
            }
            pick--;
        }
        prefabY = PrefabYOffset(0);
        return tilePrefabs[0];
    }

    float PrefabYOffset(int index)
    {
        if (tilePrefabYOffsets == null || index < 0 || index >= tilePrefabYOffsets.Length)
            return 0f;
        return tilePrefabYOffsets[index];
    }

    IEnumerator DropColumns(int colMin, int colMax)
    {
        PlayTile(0.9f);
        int count = Mathf.Max(1, (colMax - colMin + 1) * Rows);
        float stagger = dropStagger;
        if (count > 40)
            stagger = Mathf.Min(dropStagger, 2.5f / count);

        for (int c = colMin; c <= colMax; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                Tile t = FindTile(c, r);
                if (t == null) continue;
                StartCoroutine(DropTileWithDirt(t));
                yield return new WaitForSeconds(stagger);
            }
        }
        yield return new WaitForSeconds(dropDuration + 0.15f);
    }

    Tile FindTile(int col, int row)
    {
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].col == col && tiles[i].row == row) return tiles[i];
        return null;
    }

    IEnumerator DropTileWithDirt(Tile t)
    {
        if (t.dirtTransform != null)
        {
            if (t.dirtRenderer != null) t.dirtRenderer.enabled = true;
            float pitch = 0.85f + Hash(t.col, t.row, seed + 5) * 0.3f;
            StartCoroutine(FallOne(t.dirtTransform, t.dirtRest, t.col, t.row, false, () => PlayDirt(pitch)));
            yield return new WaitForSeconds(dirtDropLead);
        }

        if (t.renderer != null) t.renderer.enabled = true;
        yield return FallOne(t.transform, t.rest, t.col, t.row, true);
    }

    IEnumerator DropEnds(bool firstTime)
    {
        PlayStamp(0.6f);
        if (firstTime)
            yield return DropPiece(corePiece, coreRest, EndColumn(coreEnd), MidRow);
        yield return DropPiece(enemySpawner, spawnRest, EndColumn(spawnEnd), MidRow);
    }

    IEnumerator DropPiece(Transform xf, Vector3 rest, int col, int row)
    {
        if (xf == null) yield break;
        SetEndVisible(xf, true);
        if (dropFromSky)
            xf.position = rest + Vector3.up * (dropHeight + 6f);
        PlayStamp(0.7f);
        yield return FallOne(xf, rest, col, row);
    }

    IEnumerator FallOne(Transform xf, Vector3 to, int x, int y, bool tileThud = false, Action onThud = null)
    {
        if (xf == null) yield break;
        Vector3 from = xf.position;
        float d = dropDuration * (0.85f + Hash(x, y, seed + 9) * 0.3f);
        float elapsed = 0f;
        bool thud = false;
        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / d);
            float ease = 1f - Mathf.Pow(1f - u, 3f);
            float bounce = u > 0.78f
                ? 1f + Mathf.Sin((u - 0.78f) / 0.22f * Mathf.PI) * 0.06f * (1f - u)
                : ease;
            xf.position = Vector3.LerpUnclamped(from, to, bounce);
            if (!thud && u > 0.78f)
            {
                thud = true;
                if (tileThud && Hash(x, y, seed) > 0.78f)
                    PlayTile(0.7f + Hash(x + 2, y, seed) * 0.5f);
                onThud?.Invoke();
            }
            yield return null;
        }
        xf.position = to;
    }

    void CacheEnds()
    {
        int mid = MidRow;
        if (corePiece != null)
        {
            Vector3 p = CellWorld(EndColumn(coreEnd), mid);
            p.y = pieceY;
            coreRest = p;
        }
        if (enemySpawner != null)
        {
            Vector3 p = CellWorld(EndColumn(spawnEnd), mid);
            p.y = pieceY;
            spawnRest = p;
        }
    }

    void HideEndsInSky()
    {
        if (corePiece != null)
        {
            if (dropFromSky)
                corePiece.position = coreRest + Vector3.up * (dropHeight + 6f);
            SetEndVisible(corePiece, false);
        }
        HideSpawnerInSky();
    }

    void HideSpawnerInSky()
    {
        if (enemySpawner == null) return;
        if (dropFromSky)
            enemySpawner.position = spawnRest + Vector3.up * (dropHeight + 6f);
        SetEndVisible(enemySpawner, false);
    }

    static void SetEndVisible(Transform xf, bool visible)
    {
        if (xf == null) return;
        if (xf.gameObject.activeSelf != visible)
            xf.gameObject.SetActive(visible);
    }

    int EndColumn(BoardEnd end) => end == BoardEnd.Left ? 0 : Columns - 1;
    int MidRow => Rows / 2;

    public Vector3 CellCenter(int col, int row) => CellWorld(col, row);

    Vector3 CellWorld(int col, int row)
    {
        if (grid != null)
            return grid.CellToWorld(col, row);
        return transform.TransformPoint(new Vector3(col + 0.5f, 0f, row + 0.5f));
    }

    void ClearGround()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name.StartsWith("Ground_") || c.name.StartsWith("Dirt_"))
                Destroy(c.gameObject);
        }
        tiles.Clear();
    }

    void PlayTile(float pitch) => PlayBoard(tileClip, tileSource, pitch);
    void PlayStamp(float pitch) =>
        PlayBoard(stampClip, stampSource != null ? stampSource : tileSource, pitch);
    void PlayDirt(float pitch) =>
        PlayBoard(dirtClip, dirtSource != null ? dirtSource : tileSource, pitch);

    static void PlayBoard(AudioClip clip, AudioSource source, float pitch)
    {
        if (clip == null || source == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip);
    }

    static Quaternion QuarterYaw(int x, int y, int s) =>
        Quaternion.Euler(0f, (HashInt(x, y, s) & 3) * 90f, 0f);

    static float Hash(int x, int y, int s)
    {
        uint n = (uint)(x * 374761393 + y * 668265263 + s * 1274126177);
        n = (n ^ (n >> 13)) * 1274126177u;
        return (n & 0xFFFF) / 65535f;
    }

    static int HashInt(int x, int y, int s) => x * 73856093 ^ y * 19349663 ^ s * 83492791;
}
