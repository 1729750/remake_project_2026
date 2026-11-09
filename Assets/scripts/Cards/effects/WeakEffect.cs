using System;

// 큐에 머무는 동안 대상이 가하는 공격 피해 20% 감소.
[Serializable]
public class WeakEffect : Effect
{
    private const int Priority = 1;

    public WeakEffect(int magnitude) : base(EffectType.Weak, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool a)
    {
        if (effect.IsAttackEffect())
            effect.Multiply(0.8f);
    }
}
