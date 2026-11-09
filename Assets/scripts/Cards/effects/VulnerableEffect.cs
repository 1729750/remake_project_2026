using System;

// 큐에 머무는 동안 대상이 받는 공격 피해 20% 증가.
[Serializable]
public class VulnerableEffect : Effect
{
    private const int Priority = 1;

    public VulnerableEffect(int magnitude) : base(EffectType.Vulnerable, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool a)
    {
        if (effect.IsAttackEffect())
            effect.Multiply(1.2f);
    }
}
