using UnityEngine;
using System;
[Serializable]
public class CooldownToCost:Effect
{
    public CooldownToCost(int magnitude):base(EffectType.CooldownToCost, magnitude) { }

    // CooldownToCost도 subject 자신의 카드를 유리하게 바꿔주는 자기 강화 유틸리티이므로 양수
    // magnitude는 User, 음수는 Opponent(효과 약화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    public override void OnExpired(CharacterManager subject) { }
    public override void OnTurnStarted(CharacterManager subject) { }
    public override void OnTurnEnded(CharacterManager subject) { }
    // actualUse가 true면 실제로 카드가 사용되는 시점의 호출이고, false면 시각화 갱신을 위한
    // 미리보기(dry-run) 호출이다 — 실제 상태를 바꾸는 부수효과(스택 소모 등)는 actualUse일 때만 해야 한다.
    public override void OnUse(CharacterManager subject, CardInstance self, bool actualUse) { }
    // subject: 이 CardInstance(자신이 속한 카드)를 사용하는 주체. cardInstance: 지금 막 사용되는 카드
    // (큐에 있던 자신이 아니라 새로 사용되는 카드 쪽). 큐에 있던 카드가 새 카드 사용에 반응할 때 쓴다.
    public override void OnUsingOther(CharacterManager subject, CardInstance cardInstance, bool actualUse)
    {
        int cooldown = cardInstance.GetCooldown();
        cardInstance.AddCost(cooldown-1);
        cardInstance.ChangeCooldownLeft(-cooldown+1);
        if (actualUse && !IsInfinite)
        {
            _magnitude -= 1;
            if (_magnitude <= 0)
            {
                subject.RemoveEffect<CooldownToCost>();
            }
        }
    }
    public override void OnTick(CharacterManager subject) { }
    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}