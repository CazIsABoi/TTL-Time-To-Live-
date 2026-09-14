using UnityEngine;

// Persists which cards the player has unlocked across runs via PlayerPrefs,
// keyed on CardData.id. A card with unlockScore <= 0 is always unlocked.
public static class CardUnlocks
{
    private const string KeyPrefix = "CardUnlocked_";

    public static bool IsUnlocked(CardData card)
    {
        if (card == null) return false;
        if (card.unlockScore <= 0) return true;

        return PlayerPrefs.GetInt(KeyPrefix + card.id, 0) == 1;
    }

    public static void Unlock(CardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.id)) return;

        PlayerPrefs.SetInt(KeyPrefix + card.id, 1);
        PlayerPrefs.Save();
    }
}
