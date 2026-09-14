using System;
using UnityEngine;

public class Core : MonoBehaviour
{
    public static Core Instance { get; private set; }

    [SerializeField] private int coreHitDamage = 10;

    [Tooltip("Cells within this Chebyshev distance of the core's cell cannot be built on. 0 = only the core's own cell is blocked.")]
    [SerializeField] private int exclusionRadiusCells = 1;

    public int ExclusionRadiusCells => exclusionRadiusCells;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            other.gameObject.GetComponent<Enemy>().Die();
            UnityEngine.Object.FindAnyObjectByType<GameController>().TakeDamage(coreHitDamage);
        }
    }
}
