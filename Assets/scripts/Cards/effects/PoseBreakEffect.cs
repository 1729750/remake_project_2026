using System;

// 큐에 머무는 동안 대상이 받는 공격 피해 2배.
[Serializable]
public class PoseBreakEffect : Effect
{
    private const int Priority = 4;

    public PoseBreakEffect(int magnitude) : base(EffectType.PoseBreak, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool a)
    {
        if (effect.IsAttackEffect())
            effect.Multiply(2f);
    }
}
