using System;
using System.Collections;
using UnityEngine;

public class TickManager : MonoBehaviour
{
    public static TickManager Instance { get; private set; }

    public float tickInterval = 0.5f;
    public event Action OnTick;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        Instance = this;
    }

    private void OnEnable()
    {
        StartCoroutine(TickLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator TickLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(tickInterval);
            OnTick?.Invoke();
        }
    }
}
