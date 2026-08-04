using System;
using UnityEngine;

[Serializable]
public class VulnerableEffect : Effect
{
    private const int Priority = 1;

    public VulnerableEffect(int magnitude) : base(EffectType.Vulnerable, magnitude, Priority) { }

    // Vulnerable은 받는 피해를 늘리는 디버프이므로 양수 magnitude는 Opponent, 음수는 User(디버프 완화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    // Vulnerable: target takes increased damage while applied
    public override void OnAppliedOther(CharacterManager b, CardEffect effect,bool a)
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
