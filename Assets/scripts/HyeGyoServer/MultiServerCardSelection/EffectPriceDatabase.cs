using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EffectPriceDatabase_Multi",
    menuName = "Multi/Effect Price Database"
)]
public sealed class EffectPriceDatabase : ScriptableObject
{
    [Serializable]
    public class EnhanceOptionEntry
    {
        [Header("Effect")]
        public CardEffect effect;

        [Header("Card Change")]
        public int cost;

        public int cooldown;

        public CardUpgrade CreateUpgrade()
        {
            return new CardUpgrade(
                effect,
                cost,
                cooldown
            );
        }
    }

    [SerializeField]
    private List<EnhanceOptionEntry>
        enhanceOptions = new();

    public int Count
    {
        get
        {
            return enhanceOptions != null
                ? enhanceOptions.Count
                : 0;
        }
    }

    public bool TryCreateUpgrade(
        int index,
        out CardUpgrade upgrade)
    {
        upgrade = default;

        if (enhanceOptions == null)
        {
            return false;
        }

        if (index < 0 ||
            index >= enhanceOptions.Count)
        {
            return false;
        }

        EnhanceOptionEntry entry =
            enhanceOptions[index];

        if (entry == null)
        {
            return false;
        }

        upgrade =
            entry.CreateUpgrade();

        return true;
    }
}