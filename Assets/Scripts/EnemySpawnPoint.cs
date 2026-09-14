using UnityEngine;

// Attach to each enemy spawn point Transform (the same GameObjects referenced
// in EnemySpawner.spawnPoints). Controls when this specific spawn point
// starts producing enemies (gated by GameController.Score) and how large an
// area around it is off-limits for building, mirroring Core's exclusion.
//
// The exclusion only applies once the spawn point has "activated" (i.e. the
// player's score has reached activationScore). Before that, the area is free
// to build on like any other cell. The moment it activates, any buildings
// that were placed in its vicinity are destroyed and the cells cleared.
public class EnemySpawnPoint : MonoBehaviour
{
    [Tooltip("Score required before this spawn point starts producing enemies. 0 = active from the start.")]
    public int activationScore = 0;

    [Tooltip("Cells within this Chebyshev distance of this spawn point cannot be built on once active. 0 = only the spawn point's own cell is blocked.")]
    public int exclusionRadiusCells = 1;

    [SerializeField] private GameController gameController;

    private bool wasActive;

    // True once the player's score has reached activationScore. Buildings
    // may only be blocked/cleared from this spawn point's vicinity while
    // this is true.
    public bool IsActive => gameController == null || gameController.Score >= activationScore;

    private void Awake()
    {
        if (gameController == null)
            gameController = FindAnyObjectByType<GameController>();
    }

    private void Start()
    {
        // Registering in Start (rather than OnEnable/Awake) guarantees
        // GridManager.Instance is already set, since Unity runs all Awake()
        // calls in the scene before any Start() call.
        if (GridManager.Instance != null)
            GridManager.Instance.RegisterSpawnPointExclusion(this);

        wasActive = IsActive;
        if (wasActive)
            ClearVicinity();
    }

    private void Update()
    {
        bool isActiveNow = IsActive;
        if (isActiveNow && !wasActive)
            ClearVicinity();

        wasActive = isActiveNow;
    }

    // Destroys any PlacedBuilding within exclusionRadiusCells of this spawn
    // point and restores their grid cells to walkable, since they were only
    // legally placed there while this spawn point was still inactive.
    private void ClearVicinity()
    {
        var gm = GridManager.Instance;
        if (gm == null) return;

        Vector2Int spawnCell = gm.WorldToCell(transform.position);

        var buildings = FindObjectsByType<PlacedBuilding>();
        foreach (var building in buildings)
        {
            if (building == null) continue;

            bool withinRadius = false;
            foreach (var cell in building.cells)
            {
                int dist = Mathf.Max(Mathf.Abs(cell.x - spawnCell.x), Mathf.Abs(cell.y - spawnCell.y));
                if (dist <= exclusionRadiusCells)
                {
                    withinRadius = true;
                    break;
                }
            }

            if (!withinRadius) continue;

            foreach (var cell in building.cells)
                gm.SetWalkable(cell.x, cell.y, true);

            Destroy(building.gameObject);
        }

        Obstacle.RaiseChanged();
    }

    private void OnDisable()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.UnregisterSpawnPointExclusion(this);
    }
}
