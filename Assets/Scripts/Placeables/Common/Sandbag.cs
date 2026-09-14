using UnityEngine;

public class Sandbag : MonoBehaviour, IStackableBuilding
{
    [SerializeField] GameObject topCover;

    void Awake()
    {
        if (topCover != null)
            topCover.SetActive(false);
    }

    public bool CanAcceptStack(CardData incoming) => true;

    public void OnStacked(CardData incoming)
    {
        if (topCover != null)
            topCover.SetActive(true);
    }
}