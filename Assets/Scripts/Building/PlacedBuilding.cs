using System.Collections.Generic;
using UnityEngine;
using static GridBuildPlacer;

public class PlacedBuilding : MonoBehaviour
{
    public List<Vector2Int> cells = new();
    public CardData sourceCard;
    public int stackCount = 1;

    public bool CanStack(CardData incoming)
    {
        if (incoming == null || sourceCard == null) return false;
        if (!incoming.stackable || !sourceCard.stackable) return false;
        if (incoming != sourceCard && incoming.id != sourceCard.id) return false;
        if (stackCount >= Mathf.Max(2, incoming.maxStack)) return false;

        var hook = GetComponent<IStackableBuilding>();
        return hook == null || hook.CanAcceptStack(incoming);
    }

    public void AddStack(CardData incoming)
    {
        stackCount++;
        GetComponent<IStackableBuilding>()?.OnStacked(incoming);
    }
}