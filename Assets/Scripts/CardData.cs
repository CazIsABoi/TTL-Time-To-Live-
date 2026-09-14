using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Cards/Card Data")]
public class CardData : ScriptableObject
{
    public string id;
    public string title;
    [TextArea] public string description;
    public CardRarity rarity;

    // Footprint/cost used by GridBuildPlacer when this card is played.
    public int sizeX = 1;
    public int sizeY = 1;
    public int waveLifespan = 0; // 0 = permanent, > 0 = number of waves before auto-destroy

    // How many placements this card is worth when drawn (shown via the
    // "amount-label" UI element when > 1). The card is only discarded once
    // the runtime stack in HandManager reaches 0.
    public int amount = 1;

    // How many copies of this card exist in the draw pile at once (i.e. how
    // many times it can be drawn/be in the discard pile simultaneously
    // before being exhausted). Distinct from "amount", which controls how
    // many placements a single drawn copy is worth. 1 = only ever one copy
    // in the deck at a time (default/current behavior).
    [Min(1)]
    public int copiesInDeck = 1;

    // Gameplay category, used for filtering/UI grouping.
    public CardCategory category;

    // Score required to unlock this card for the draw pool. 0 (or less)
    // means the card is unlocked from the start. See CardUnlocks for the
    // persisted (PlayerPrefs) unlock state.
    public int unlockScore = 0;

    // USS class name derived from rarity (e.g. "rarity-common"), for applying
    // rarity-based styling (banner color etc.) via AddToClassList/USS.
    public string RarityCssClass => "rarity-" + rarity.ToString().ToLowerInvariant();

    // Prefab for what this card places/builds in the world when played
    // (NOT used for the hand UI itself, which binds onto the static
    // Slot1-5 VisualElements in Layout.uxml). Per-instance gameplay stats
    // (damage, cooldown, etc.) live on this prefab's own component(s).
    public GameObject prefab;
    public float yOffset = 0f;
    public bool canRotate = true;
    public bool stackable = false;
    [Min(2)]
    public int maxStack = 2;
}

[System.Serializable]
public class PlayerHand
{
    public List<CardData> drawPile;
    public List<CardData> discardPile;
    public CardData[] hand = new CardData[5];
}