using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum BreakableKind
{
    None = 0,
    Loose = 1 << 0,
    Wall = 1 << 1
}

public class Obstacle : MonoBehaviour
{
    public static readonly List<Obstacle> All = new List<Obstacle>();
    public static event Action Changed;
    public static void RaiseChanged() => Changed?.Invoke();

    [SerializeField] int health = 2;
    [SerializeField] ParticleSystem destroyedParticles;
    [SerializeField] int contactDamage = 0;
    [SerializeField] BreakableKind kind = BreakableKind.Loose;
    [SerializeField] bool blockPath = true;

    public BreakableKind Kind => kind;
    public bool BlocksPath => blockPath;
    public Vector3 Position => transform.position;
    public int ContactDamage => contactDamage;

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        UnregisterFromGrid();
    }

    public bool CanBeBrokenBy(BreakableKind attacker) =>
        kind != BreakableKind.None && (kind & attacker) != 0;

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
            Break();
    }

    void Break()
    {
        if (destroyedParticles != null)
            Instantiate(destroyedParticles, transform.position, Quaternion.identity);

        var placed = GetComponent<PlacedBuilding>();
        var gm = GridManager.Instance;
        if (gm != null && placed != null && placed.cells != null)
        {
            foreach (var cell in placed.cells)
                gm.SetWalkable(cell.x, cell.y, true);
            gm.UnregisterObstacleCells(placed.cells, this);
        }
        else
        {
            UnregisterFromGrid();
        }

        Destroy(gameObject);
        RaiseChanged();
    }

    void UnregisterFromGrid()
    {
        var gm = GridManager.Instance;
        if (gm == null) return;

        var placed = GetComponent<PlacedBuilding>();
        if (placed != null && placed.cells != null)
            gm.UnregisterObstacleCells(placed.cells, this);
        else
            gm.UnregisterObstacleCells(new[] { gm.WorldToCell(transform.position) }, this);
    }
}