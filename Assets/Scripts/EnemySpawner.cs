using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    private Enemy[] currentEnemies = new Enemy[0];
    private Enemy[] currentBoss = new Enemy[0];
    private Enemy currentEnemyPrefab;
    // Regular enemy
    [SerializeField] private Enemy drifterPrefab;
    // Pressure Enemies
    [SerializeField] private Enemy jumperPrefab; // Hops over low cover
    [SerializeField] private Enemy breakerPrefab; // Smashes through walls if path is shorter
    [SerializeField] private Enemy squeezerPrefab; // Squeezes through narrow gaps, takes the health toll
    [SerializeField] private Enemy flyerPrefab; // Flies over all cover, slower than ground enemies, but can only be damaged by ranged attacks
    // Timer Enemeis
    [SerializeField] private Enemy sprinterPrefab; // Moves fast, but has a short lifespan
    [SerializeField] private Enemy sfusePrefab; // Short lifespan, high HP, explodes on lifespan end, damaging nearby buildings
    [SerializeField] private Enemy spongePrefab; // Normal lifespan, high HP
    // Rule Breakers
    [SerializeField] private Enemy stubbornPrefab; // Ignores path bait
    [SerializeField] private Enemy curiousPrefab; // Prefers bait / low cost routes even when longer
    [SerializeField] private Enemy herdPrefab; // Moves in groups, following the first spawned enemy of its type
    // Bosses
    [SerializeField] private Enemy siegePrefab; // Breaker with aura that makes drifters also damage walls
    [SerializeField] private Enemy clockThiefPrefab; // On death, refunds lifespan to nearby enemies, and steals lifespan from nearby buildings
    [SerializeField] private Enemy phaseMitePrefab; // Every few tiles, skips one shape check, allowing it to pass through walls and other obstacles
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform target;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private GameController gameController;
    private System.Action waveDeathCallback;

    public Transform[] SpawnPoints => spawnPoints;
    public Transform Target => target;
    private System.Random rng;

    private float nextSpawnTime;

    private void Awake()
    {
        currentEnemyPrefab = drifterPrefab;
        if (gameController == null)
            gameController = FindAnyObjectByType<GameController>();
        rng = RunSeed.Instance != null
    ? RunSeed.Instance.CreateRng("enemies")
    : new System.Random();
    }
    private void Start()
    {
        nextSpawnTime = Time.time + spawnInterval;
        currentEnemies = new Enemy[] { drifterPrefab };
        currentBoss = new Enemy[] { siegePrefab, clockThiefPrefab, phaseMitePrefab };
    }

    private void FixedUpdate()
    {

    }

    // Returns only the spawn points currently active based on their
    // EnemySpawnPoint.activationScore (score-gated) compared to the
    // GameController's current score. Spawn points without an
    // EnemySpawnPoint component are treated as always active.
    private Transform[] GetActiveSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return spawnPoints;

        int currentScore = gameController != null ? gameController.Score : int.MaxValue;

        var active = new System.Collections.Generic.List<Transform>(spawnPoints.Length);
        foreach (var point in spawnPoints)
        {
            if (point == null) continue;
            var spawnPointData = point.GetComponent<EnemySpawnPoint>();
            if (spawnPointData == null || currentScore >= spawnPointData.activationScore)
                active.Add(point);
        }

        return active.ToArray();
    }

    public void SpawnOne(System.Action onEnemyDied)
    {
        if (drifterPrefab == null || target == null || spawnPoints == null || spawnPoints.Length == 0)
            return;

        Transform[] activeSpawnPoints = GetActiveSpawnPoints();
        if (activeSpawnPoints == null || activeSpawnPoints.Length == 0)
            return;
        currentEnemyPrefab = currentEnemies[Random.Range(0, currentEnemies.Length)];

        waveDeathCallback = onEnemyDied;
        int randomIndex = Random.Range(0, activeSpawnPoints.Length);
        Transform spawnPoint = activeSpawnPoints[randomIndex];

        Enemy enemy = Instantiate(currentEnemyPrefab, spawnPoint.position, spawnPoint.rotation);
        enemy.Initialize(target);
        enemy.Died += HandleEnemyDied;
    }

    public void SpawnBoss(System.Action onBossDied)
    {
        if (currentBoss == null || target == null || spawnPoints == null || spawnPoints.Length == 0)
            return;
        Transform[] activeSpawnPoints = GetActiveSpawnPoints();
        if (activeSpawnPoints == null || activeSpawnPoints.Length == 0)
            return;
        waveDeathCallback = onBossDied;
        int randomIndex = Random.Range(0, activeSpawnPoints.Length);
        Transform spawnPoint = activeSpawnPoints[randomIndex];
        Enemy bossEnemy = Instantiate(currentBoss[Random.Range(0, currentBoss.Length)], spawnPoint.position, spawnPoint.rotation);
        bossEnemy.Initialize(target);
        bossEnemy.Died += HandleEnemyDied;
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        enemy.Died -= HandleEnemyDied;
        waveDeathCallback?.Invoke();
        print("Enemy died");
    }

    public void IncreaseDifficulty(int level)
    {
        spawnInterval = Mathf.Max(1f, 5f - (level * 0.5f));

        for (int i = 0; i < currentEnemies.Length; i++)
        {
            currentEnemies[i].GetComponent<Enemy>().SetTTL(30f + (level * 2f));
        }
        if (level >= 5)
        {
            currentEnemies = new Enemy[] { drifterPrefab, jumperPrefab, breakerPrefab, curiousPrefab };
        }
        if (level >= 10)
        {
            currentEnemies = new Enemy[] { drifterPrefab, jumperPrefab, breakerPrefab, squeezerPrefab, curiousPrefab, stubbornPrefab };
        }
        if (level >= 20)
        {
            currentEnemies = new Enemy[] { drifterPrefab, jumperPrefab, breakerPrefab, squeezerPrefab, curiousPrefab, stubbornPrefab, sprinterPrefab, spongePrefab };
        }
        if (level >= 30)
        {
            currentEnemies = new Enemy[] { drifterPrefab, jumperPrefab, breakerPrefab, squeezerPrefab, curiousPrefab, stubbornPrefab, sprinterPrefab, spongePrefab, flyerPrefab, herdPrefab, sfusePrefab };
        }
        if (level >= 40)
        {
            currentEnemies = new Enemy[] { drifterPrefab, jumperPrefab, breakerPrefab, squeezerPrefab, curiousPrefab, stubbornPrefab, sprinterPrefab, spongePrefab, flyerPrefab, herdPrefab, sfusePrefab};
        }
    }
}