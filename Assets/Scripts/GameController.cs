using System.Collections;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [SerializeField] private int health = 100;
    [SerializeField] private int score = 0;
    [SerializeField] private int level = 1;
    [SerializeField] private int enemiesPerLevel = 10;
    [SerializeField] private int enemiesPerLevelIncrement = 5;
    [SerializeField] private float spawnDelay = 2f;
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private HandManager handManager;
    [Tooltip("Reachability treats cells occupied by obstacles this kind can smash as still connected. Loose matches default drifters.")]
    [SerializeField] private BreakableKind reachabilityBreakKind = BreakableKind.Loose;
    private int aliveEnemies = 0;
    private UIController uiController;

    public bool IsBetweenWaves { get; private set; }
    public bool IsExitReachable { get; private set; } = true;
    public int Score => score;

    private void Start()
    {
        StartCoroutine(GameLoop());
        uiController = GetComponent<UIController>();
        uiController.SetHealth(health, 100);
        uiController.SetScore(score);
        RecheckReachability();
    }

    private void Awake()
    {
        aliveEnemies = enemiesPerLevel;
        IsBetweenWaves = true;
    }

    private void OnEnable()
    {
        Obstacle.Changed += RecheckReachability;
    }

    private void OnDisable()
    {
        Obstacle.Changed -= RecheckReachability;
    }

    public void RecheckReachability()
    {
        var gm = GridManager.Instance;
        if (gm == null || spawner == null || spawner.Target == null || spawner.SpawnPoints == null)
        {
            IsExitReachable = true;
            return;
        }

        Vector2Int goal = gm.WorldToCell(spawner.Target.position);
        bool reachable = true;
        foreach (var spawnPoint in spawner.SpawnPoints)
        {
            if (spawnPoint == null) continue;
            Vector2Int start = gm.WorldToCell(spawnPoint.position);
            if (!gm.IsReachable(start, goal, reachabilityBreakKind))
            {
                reachable = false;
                break;
            }
        }

        IsExitReachable = reachable;
    }

    private IEnumerator GameLoop()
    {
        while (health > 0)
        {
            if (IsBetweenWaves)
                yield return new WaitUntil(() => !IsBetweenWaves);

            for (int i = 0; i < enemiesPerLevel; i++)
            {
                spawner.SpawnOne(OnEnemyDied);
                yield return new WaitForSeconds(spawnDelay);
            }

            while (aliveEnemies > 0)
                yield return null;

            IsBetweenWaves = true;
            handManager?.StartNewHand();
            level++;
            spawner.IncreaseDifficulty(level);
            uiController.SetWave(level);
            enemiesPerLevel += enemiesPerLevelIncrement;
            aliveEnemies = enemiesPerLevel;
        }
    }

    public void OnEnemyDied()
    {
        aliveEnemies--;
        score += 10;
        handManager?.RefreshUnlockedCards(score);
        uiController.SetScore(score);
        print(aliveEnemies + " enemies remaining");
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        uiController.SetHealth(health, 100);
        if (health <= 0)
        {
            GameOver();
        }
    }

    private void GameOver()
    {
        print("Game Over");
    }

    public bool SetIsBetweenWaves(bool value)
    {
        return IsBetweenWaves = value;
    }
}