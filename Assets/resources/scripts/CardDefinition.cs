using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CardDefinition", menuName = "Scriptable Objects/CardDefinition")]
public class CardDefinition: ScriptableObject
{
    [SerializeField] private int cooldown;
    [SerializeField] private int cost;
    [SerializeField] private List<CardEffect> _effects;
    [SerializeField] private Sprite sprite;

    public CardDefinition(int cooldown, int cost, CardEffect[] effects, Sprite sprite)
    {
        this.cooldown = cooldown;
        this.cost = cost;
        this.sprite = sprite;
        _effects = effects.ToList();
    }

    public int GetCooldown() => cooldown;
    public int GetCost() => cost;
    public CardEffect[] GetEffects() => _effects.ToArray();
}
