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
    [SerializeField] private CardType cardType;
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
    public CardType GetCardType() => cardType;
    public Sprite GetSprite() => sprite;
    public void AddEffect(CardEffect effect) => _effects.Add(effect);

    // 같은 EffectType의 효과가 이미 있으면 magnitude만 올리고, 없으면 넘겨받은 CardEffect를 그대로 추가한다.
    public void UpgradeEffect(CardEffect cardEffect)
    {
        EffectType effectType = cardEffect.GetEffect().GetEffectType();
        foreach (CardEffect effect in _effects)
        {
            if (effect.GetEffect().GetEffectType() == effectType)
            {
                effect.GetEffect().AddMagnitude(cardEffect.GetEffect().GetMagnitude());
                return;
            }
        }
        AddEffect(cardEffect);
    }
}
