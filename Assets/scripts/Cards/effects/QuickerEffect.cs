using UnityEngine;
using System;
[Serializable]
public class QuickerEffect: Effect
{
    public QuickerEffect(int magnitude):base(EffectType.Quicker, magnitude) { }

    // Quicker는 subject 자신의 틱 속도를 올려주는 버프이므로 양수 magnitude는 User, 음수는
    // Opponent(효과 약화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

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
            subject.RemoveEffect<QuickerEffect>();
        }
    }

    public override void OnApply(CharacterManager subject)
    {
        subject.ChangeTickSpeed(1);
        PlayApplySound();
    }
    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
