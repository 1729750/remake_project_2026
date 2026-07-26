using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CardDefinition", menuName = "Scriptable Objects/CardDefinition")]
public class CardDefinition: ScriptableObject
{
    [SerializeField] private int cooldown;
    [SerializeField] private int cost;
    [SerializeField] private List<CardEffect> effects;
    [SerializeField] private Sprite sprite;
    [SerializeField] private CardType cardType;
    [SerializeField] private Sprite backGround;
    public CardDefinition(int cooldown, int cost, CardEffect[] effects, Sprite sprite, Sprite backGround)
    {
        this.cooldown = cooldown;
        this.cost = cost;
        this.sprite = sprite;
        this.backGround = backGround;
        this.effects = effects.ToList();
    }

    public int GetCooldown() => cooldown;
    public int GetCost() => cost;
    public CardEffect[] GetEffects() => effects.ToArray();
    public CardType GetCardType() => cardType;
    public Sprite GetSprite() => sprite;
    public Sprite GetSpriteBackground() => backGround;
}
