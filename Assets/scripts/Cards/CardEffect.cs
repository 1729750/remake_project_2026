using System;
using UnityEngine;

[Serializable]
public class CardEffect
{
    [SerializeField] private Effect _effect;
    [SerializeField] private EffectTarget _target;
    // 이 카드 위에서 이 효과를 실제로 어느 모드로 쓸지(카드 저자가 선택). 0(미설정)이면 "자동" —
    // GetAppliedCategory()가 GetSupportedCategories()로부터 합리적인 기본값을 골라준다. 기존에
    // 저장된 카드(이 필드가 생기기 전)는 전부 0으로 역직렬화되므로 자동으로 이전과 같은 동작을 한다.
    [SerializeField] private EffectCategory appliedCategory;
    private float multiplier = 1f;
    private int adder = 0;

    public void Reset()
    {
        multiplier = 1f;
        adder = 0;
    }

    public void Add(int i)
    {
        adder += i;
    }
    
    public void Multiply(float i)
    {
        multiplier *= i;
    }
    public CardEffect(Effect effect, EffectTarget target)
    {
        _effect = effect;
        _target = target;
    }

    public Effect GetEffect() => _effect;
    public EffectTarget GetEffectTarget() => _target;

    // _effect는 CardDefinition 애셋에 그대로 저장된 authoring용 데이터라(SerializeField, 서브클래스로
    // 역직렬화되지 않음) SupportedCategories 같은 서브클래스 오버라이드를 직접 읽으면 항상 base
    // Effect의 기본값(Instant)만 나온다. EffectType으로 진짜 런타임 서브클래스를 새로 만들어서 읽는다.
    public EffectCategory GetSupportedCategories() => Effect.Create(_effect.GetEffectType(), 0).SupportedCategories;

    // 실제로 이 카드 위의 이 효과가 어느 모드로 발동할지. 명시적으로 골라둔 값(appliedCategory != 0)이
    // 있으면 그걸 쓰고, 없으면(기존 카드 포함) 자동으로 고른다: Instant를 지원하면 Instant(기존
    // 동작과 동일), 아니면(Defend처럼 Continuous만 지원) 그 하나를 쓴다.
    public EffectCategory GetAppliedCategory()
    {
        if (appliedCategory != 0) return appliedCategory;
        EffectCategory supported = GetSupportedCategories();
        return supported.HasFlag(EffectCategory.Instant) ? EffectCategory.Instant : supported;
    }

    public void SetAppliedCategory(EffectCategory category) => appliedCategory = category;

    public CharacterManager GetTarget(CharacterManager user)
    {
        return _target == EffectTarget.User ? user : BattleManager.Instance.GetOpponent(user);
    }

    public int GetMagnitude()
    {
        return (int)Math.Floor(_effect.GetMagnitude() * multiplier) + adder;
    }
}
