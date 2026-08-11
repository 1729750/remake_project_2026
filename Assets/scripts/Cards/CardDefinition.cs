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
    [SerializeField] private Sprite spriteBackground;
    [SerializeField] private CardType cardType;

    // ScriptableObject는 new로 생성하면 안 되므로 CreateInstance로 만든 뒤 이 메서드로 초기화한다.
    public static CardDefinition Create(int cooldown, int cost, CardEffect[] effects, Sprite sprite, Sprite spriteBackground)
    {
        var definition = CreateInstance<CardDefinition>();
        definition.cooldown = cooldown;
        definition.cost = cost;
        definition.sprite = sprite;
        definition.spriteBackground = spriteBackground;
        definition._effects = effects.ToList();
        return definition;
    }

    public int GetCooldown() => cooldown;
    public int GetCost() => cost;
    public CardEffect[] GetEffects() => _effects.ToArray();
    public CardType GetCardType() => cardType;
    public Sprite GetSprite() => sprite;
    public Sprite GetSpriteBackground() => spriteBackground;
    public void AddEffect(CardEffect effect) => _effects.Add(effect);

    public void ChangeCooldown(int delta)
    {
        cooldown += delta;
        if (cooldown < 1) cooldown = 1;
    }

    public void ChangeCost(int delta)
    {
        cost += delta;
        if (cost < 0) cost = 0;
    }

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

    // 카드 강화 보상(effect + cost/cooldown delta)을 한 번에 적용한다.
    public void ApplyUpgrade(CardUpgrade upgrade)
    {
        if (upgrade.effect != null)
            UpgradeEffect(upgrade.effect);
        ChangeCost(upgrade.costDelta);
        ChangeCooldown(upgrade.cooldownDelta);
    }

    // 깊은 복제: CardEffect/Effect까지 전부 새로 만들어서, 원본 에셋이나 같은 소스에서 나온 다른
    // 클론과 강화(ApplyUpgrade/UpgradeEffect의 AddMagnitude) 상태를 공유하지 않게 한다.
    // RewardManager.GenerateRandomEnemy처럼 강화를 적용해야 하는 임시 카드가 필요할 때 사용한다.
    public CardDefinition Clone()
    {
        var clone = CreateInstance<CardDefinition>();
        clone.cooldown = cooldown;
        clone.cost = cost;
        clone.sprite = sprite;
        clone.spriteBackground = spriteBackground;
        clone.cardType = cardType;
        clone._effects = new List<CardEffect>(_effects.Count);
        foreach (CardEffect cardEffect in _effects)
        {
            Effect effect = cardEffect.GetEffect();
            Effect clonedEffect = new Effect(effect.GetEffectType(), effect.GetMagnitude());
            clone._effects.Add(new CardEffect(clonedEffect, cardEffect.GetEffectTarget()));
        }
        return clone;
    }
}
