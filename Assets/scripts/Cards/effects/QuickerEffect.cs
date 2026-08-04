using UnityEngine;
using System;
[Serializable]
public class QuickerEffect: Effect
{
    public QuickerEffect(int magnitude):base(EffectType.Burning, magnitude) { }

    public override void OnExpired(CharacterManager subject)
    {
        subject.ChangeTickSpeed(-1);
    }

    public override void OnTurnStarted(CharacterManager subject)
    {
    }

    public override void OnTurnEnded(CharacterManager subject)
    {
        _magnitude -= 1;
        if (_magnitude <= 0)
        {
            subject.RemoveEffect<BurningEffect>();
        }
    }

    public override void OnApply(CharacterManager subject)
    {
        subject.ChangeTickSpeed(1);
    }
    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
