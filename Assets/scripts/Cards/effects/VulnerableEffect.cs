using System;
using UnityEngine;

[Serializable]
public class VulnerableEffect : Effect
{
    private const int Priority = 1;

    public VulnerableEffect(int magnitude) : base(EffectType.Vulnerable, magnitude, Priority) { }

    // Vulnerable: target takes increased damage while applied
    public override void OnApplied(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(1.2f);
        }
    }

    public override void OnTurnEnded(CharacterManager subject)
    {
        _magnitude--;
        if (_magnitude <= 0)
            subject.RemoveEffect<VulnerableEffect>();
    }
}
