using System;
using UnityEngine;

[Serializable]
public class GuardEffect : Effect
{
    private const int Priority = 1;

    public GuardEffect(int magnitude) : base(EffectType.Guard, magnitude, Priority) { }

    // Guard: 지속되는 동안 받는 공격 피해 절반
    public override void OnApplied(CharacterManager b, CardEffect effect, bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(0.5f);
        }
    }

    public override void OnTurnStarted(CharacterManager subject)
    {
        _magnitude--;
        if (_magnitude <= 0)
        {
            subject.RemoveEffect<GuardEffect>();
        }
    }
}
