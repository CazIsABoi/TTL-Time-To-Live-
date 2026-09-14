using System;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [SerializeField] private int health = 2;
    [SerializeField] private ParticleSystem destroyedParticles;
    [SerializeField] private int contactDamage = 10;
    public static readonly List<Obstacle> All = new List<Obstacle>();
    public static event Action Changed;

    public static void RaiseChanged() => Changed?.Invoke();

    public Vector3 Position => transform.position;
    public int ContactDamage => contactDamage;

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);
    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
            Break();
    }

    private void Break()
    {
        ParticleSystem particles = Instantiate(destroyedParticles, transform.position, Quaternion.identity);
        Destroy(gameObject);
        Changed?.Invoke();
    }
}
