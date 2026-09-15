using System;
using UnityEngine;

/// <summary>
/// Single run seed for hand, world, and enemy RNG.
/// Put on the same object as GameController (or any early-awake bootstrap).
/// Set seed in the Inspector for reproducible runs; leave 0 to roll one.
/// World Setup can override via SetSeed before generation.
/// </summary>
public class RunSeed : MonoBehaviour
{
    public static RunSeed Instance { get; private set; }

    [SerializeField] private int seed;
    [SerializeField] private bool randomizeIfZero = true;

    public int Seed => seed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (seed == 0 && randomizeIfZero)
            seed = UnityEngine.Random.Range(1, int.MaxValue);

        Debug.Log($"[RunSeed] run seed = {seed}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Override the run seed (e.g. from World Setup) before generation / dealing.</summary>
    public void SetSeed(int newSeed)
    {
        if (newSeed == 0 && randomizeIfZero)
            newSeed = UnityEngine.Random.Range(1, int.MaxValue);
        seed = newSeed;
        Debug.Log($"[RunSeed] run seed set = {seed}");
    }

    /// <summary>
    /// Independent stream per system so draw order in one doesn't affect another.
    /// </summary>
    public System.Random CreateRng(string channel)
    {
        unchecked
        {
            int mixed = seed;
            if (!string.IsNullOrEmpty(channel))
            {
                for (int i = 0; i < channel.Length; i++)
                    mixed = mixed * 31 + channel[i];
            }
            if (mixed == int.MinValue) mixed = 1;
            return new System.Random(Math.Abs(mixed));
        }
    }

    /// <summary>Deterministic int in [minInclusive, maxExclusive).</summary>
    public static int Range(System.Random rng, int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }
}