using System;
using UnityEngine;

[Serializable]
public class VulnerableEffect : Effect
{
    public VulnerableEffect(int magnitude) : base(EffectType.Vulnerable, magnitude) { }

    // Vulnerable: target takes increased damage while applied
    public override void OnApplyed(CharacterManager b, CardEffect effect,bool a)
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
