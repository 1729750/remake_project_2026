using UnityEngine;
using System;
using System.Linq;
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
        if (IsInfinite) return;
        _magnitude -= 1;
        if (_magnitude <= 0)
        {
            subject.RemoveEffect<QuickerEffect>();
        }
    }

    // 기존 버그: 여기서 base.OnApply(ApplyStack)를 타지 않고 곧장 ChangeTickSpeed(1)만 호출해서
    // 끝냈던 탓에, 이 인스턴스가 영영 subject._effects에 등록되지 않았다 — 그래서 OnTurnEnded(감쇠)/
    // OnExpired(ChangeTickSpeed(-1))가 등록된 인스턴스 목록을 순회하는 CharacterManager.OnTurnEnd에서
    // 단 한 번도 호출되지 못해, Quicker를 쓸 때마다 tickSpeed가 영구히 누적되기만 했다(절대 안 내려감).
    // 이미 활성 상태일 때(stack 병합)는 속도를 또 올리지 않고, 처음 얻을 때만 1회 올린 뒤 base.OnApply로
    // 정상적으로 등록/스택되게 한다.
    public override void OnApply(CharacterManager subject)
    {
        bool alreadyActive = subject.GetEffects().Any(e => e is QuickerEffect);
        if (!alreadyActive)
            subject.ChangeTickSpeed(1);
        base.OnApply(subject);
    }
    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
