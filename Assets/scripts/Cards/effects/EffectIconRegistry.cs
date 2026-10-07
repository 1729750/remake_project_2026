using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 Manager가 없는 Lobby에서도 Card.prefab이 효과 아이콘을 표시할 수 있게 하는
/// Scene 독립 아이콘 카탈로그.
/// </summary>
public sealed class EffectIconRegistry : MonoBehaviour
{
    [Serializable]
    private sealed class Entry
    {
        public EffectType effectType;
        public Sprite sprite;
    }

    public static EffectIconRegistry Instance { get; private set; }

    [SerializeField]
    private List<Entry> effectIcons = new();

    private readonly Dictionary<EffectType, Sprite>
        cache = new();

    private void Awake()
    {
        Instance = this;
        cache.Clear();

        foreach (Entry entry in effectIcons)
        {
            if (entry != null)
                cache[entry.effectType] = entry.sprite;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static bool TryGetEmoji(
        EffectType effectType,
        out Sprite sprite)
    {
        if (Instance != null &&
            Instance.cache.TryGetValue(effectType, out sprite))
        {
            return sprite != null;
        }

        sprite = null;
        return false;
    }
}
