using System;

// "글래스 캐논" 버프: 큐에 머무는 동안 자신의 공격 계열 피해를 2배로 만들지만, 1 이상의 피해를
// 받는 즉시 소멸한다 — 혼자서는 아무 의미가 없고(곱할 공격이 없으면 그냥 버려지는 큐 슬롯),
// 반드시 같은 턴에 공격 카드를 묶어 내야 값어치가 있다. magnitude를 쓰지 않는 고정 2배 효과.
[Serializable]
public class OverchargeEffect : Effect
{
    public OverchargeEffect(int magnitude) : base(EffectType.Overcharge, magnitude) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    public override EffectCategory SupportedCategories => EffectCategory.Continuous;

    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse)
    {
        if (effect.GetEffectTarget() == EffectTarget.Opponent && effect.IsAttackEffect())
            effect.Multiply(2f);
    }

    public override void OnDamaged(CharacterManager subject, int damageTaken)
    {
        subject.RemoveEffectInstance(this);
    }
}
