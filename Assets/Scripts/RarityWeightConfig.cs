using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RarityWeightConfig", menuName = "Cards/Rarity Weight Config")]
public class RarityWeightConfig : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public CardRarity rarity;
        public float weight;
    }

    [SerializeField] private List<Entry> weights = new List<Entry>();

    // Looks up the configured weight for a rarity from this asset.
    // NOTE: this reads the asset's own serialized data. For anything that can
    // change at runtime (e.g. a "boost legendary odds" upgrade), don't call
    // this directly every draw and don't mutate `weights` here - instead copy
    // these values into a runtime Dictionary<CardRarity, float> once (see
    // HandManager) and mutate/read that copy so the source asset stays clean.
    public float GetWeight(CardRarity rarity)
    {
        foreach (var entry in weights)
        {
            if (entry.rarity == rarity)
                return entry.weight;
        }

        // TODO: decide the desired fallback for an unconfigured rarity (0 vs 1).
        return 0f;
    }

    public Dictionary<CardRarity, float> ToRuntimeDictionary()
    {
        var dict = new Dictionary<CardRarity, float>();
        foreach (var entry in weights)
        {
            dict[entry.rarity] = entry.weight;
        }
        return dict;
    }
}
