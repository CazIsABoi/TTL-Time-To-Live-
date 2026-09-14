using UnityEngine;

public class DestroyParticleSystem : MonoBehaviour
{
    [SerializeField] private float destroyDelay = 1f;
    void Start()
    {
        Destroy(gameObject, destroyDelay);
    }
}
