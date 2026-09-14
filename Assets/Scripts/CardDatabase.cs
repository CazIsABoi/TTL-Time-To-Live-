using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardDatabase", menuName = "Cards/Card Database")]
public class CardDatabase : ScriptableObject
{
    // Every CardData asset that exists in the game.
    public List<CardData> allCards = new List<CardData>();
}
