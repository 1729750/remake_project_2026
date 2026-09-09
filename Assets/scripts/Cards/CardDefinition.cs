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
    // Instant 카드에는 Instant로만 쓰는 효과만, Continuous 카드에는 Continuous로만 쓰는 효과만
    // 추가할 수 있다. Mix 카드는 어느 쪽이든 받아준다. 추가하려는 효과의 appliedCategory가 두
    // 비트(Instant|Continuous)를 다 켰으면 카드 자체를 Mix로 승격시키고 받아준다.
    public void AddEffect(CardEffect effect)
    {
        if (!TryReconcileCardType(effect)) return;
        _effects.Add(effect);
    }

    private bool TryReconcileCardType(CardEffect effect)
    {
        if (!ValidateSupportedCategory(effect)) return false;

        EffectCategory applied = effect.GetAppliedCategory();
        if (applied == (EffectCategory.Instant | EffectCategory.Continuous))
        {
            cardType = CardType.Mix;
            return true;
        }
        if (cardType == CardType.Mix) return true;

        CardType requiredType = applied == EffectCategory.Continuous ? CardType.Continuous : CardType.Instant;
        if (cardType != requiredType)
        {
            Debug.LogError($"[{name}] Cannot add a {applied} effect to a {cardType} card.");
            return false;
        }
        return true;
    }

    // appliedCategory가 그 EffectType이 실제로 지원하는 모드를 벗어나지 않는지 확인한다
    // (예: Attack에 Continuous를 고르는 건 무엇을 "되돌릴지" 정의할 수 없어 허용하지 않는다).
    private bool ValidateSupportedCategory(CardEffect effect)
    {
        EffectCategory supported = effect.GetSupportedCategories();
        EffectCategory applied = effect.GetAppliedCategory();
        if ((applied & ~supported) == 0) return true;

        Debug.LogError($"[{name}] {effect.GetEffect().GetEffectType()} is set to {applied}, but only supports {supported}.");
        return false;
    }

    // 인스펙터에서 _effects를 직접 편집한 경우(AddEffect를 거치지 않은 경우)를 위한 안전망.
    // 두 비트를 다 켠 효과가 하나라도 있으면 카드를 Mix로 승격시키고, 그 외에는 cardType과 안
    // 맞는 효과가 있으면 콘솔에 에러만 남긴다(디자이너가 의도적으로 고른 cardType을 임의로
    // 덮어쓰지 않는다).
    private void OnValidate()
    {
        if (_effects == null) return;

        bool hasMixEffect = _effects.Exists(e => e?.GetEffect() != null &&
            e.GetAppliedCategory() == (EffectCategory.Instant | EffectCategory.Continuous));
        if (hasMixEffect)
        {
            cardType = CardType.Mix;
            return;
        }

        foreach (CardEffect effect in _effects)
        {
            if (effect?.GetEffect() == null) continue;
            if (!ValidateSupportedCategory(effect)) continue;

            EffectCategory applied = effect.GetAppliedCategory();
            CardType requiredType = applied == EffectCategory.Continuous ? CardType.Continuous : CardType.Instant;
            if (cardType != CardType.Mix && cardType != requiredType)
                Debug.LogError($"[{name}] {cardType} card contains a {applied} effect ({effect.GetEffect().GetEffectType()}) — fix the effect or the card's CardType.");
        }
    }

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

    // 같은 EffectType이면서 같은 appliedCategory(Instant/Continuous)의 효과가 이미 있으면 magnitude만
    // 올리고, 없으면 넘겨받은 CardEffect를 그대로 추가한다. 타입만 보고 병합하면 Continuous 효과가
    // Instant 몫과(혹은 그 반대로) 섞여버릴 수 있어 category까지 함께 맞춰본다 — RewardManager.CanEnhance의
    // hasMergeTarget 판정과 반드시 같은 기준을 써야 한다(다르면 CanEnhance가 "새로 추가될 것"이라고
    // 허용한 경우에 여기서 엉뚱한 기존 효과에 병합해버릴 수 있다).
    public void UpgradeEffect(CardEffect cardEffect)
    {
        EffectType effectType = cardEffect.GetEffect().GetEffectType();
        EffectCategory appliedCategory = cardEffect.GetAppliedCategory();
        foreach (CardEffect effect in _effects)
        {
            if (effect.GetEffect().GetEffectType() == effectType && effect.GetAppliedCategory() == appliedCategory)
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
            CardEffect clonedCardEffect = new CardEffect(clonedEffect, cardEffect.GetEffectTarget());
            // appliedCategory(어느 모드로 쓸지 저자가 명시적으로 고른 값)도 함께 복제한다 — 그러지
            // 않으면 Continuous로 골라둔 효과가 클론에서는 자동 해석(Instant)으로 되돌아간다.
            clonedCardEffect.SetAppliedCategory(cardEffect.GetAppliedCategory());
            clone._effects.Add(clonedCardEffect);
        }
        return clone;
    }
}
