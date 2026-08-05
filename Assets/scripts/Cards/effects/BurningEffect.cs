using System;
using UnityEngine;

[Serializable]
public class BurningEffect: Effect
{
    public BurningEffect(int magnitude):base(EffectType.Burning, magnitude) { }
    public override void OnExpired(CharacterManager subject) { }

    public override void OnTurnEnded(CharacterManager subject)
    {
        subject.TakeDamage(_magnitude/10);
        _magnitude -= 1;
        if (_magnitude <= 0)
        {
            subject.RemoveEffect<BurningEffect>();
        }
    }

    public override void OnApply(CharacterManager subject)
    {
        if (subject.GetIsGuard())
        {
            _magnitude /= 3;
        }
        base.OnApply(subject);
    }

    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
