using System;
using UnityEngine;

[Serializable]
public class BurningEffect: Effect
{
    public BurningEffect(int magnitude):base(EffectType.Burning, magnitude) { }

    // Burning은 매 턴 subject에게 피해를 입히는 디버프이므로 양수 magnitude는 Opponent,
    // 음수는 User(디버프 완화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    // 매 턴 감소(OnTurnEnded에서 자체적으로 magnitude를 깎고 0 이하면 제거)로 수명을 관리하는
    // 타입이라 Continuous(큐 잔류 = 수명)로 재해석하면 그 감쇠 로직과 충돌한다 — Instant만 지원.
    public override EffectCategory SupportedCategories => EffectCategory.Instant;

    public override void OnExpired(CharacterManager subject) { }

    public override void OnTurnEnded(CharacterManager subject)
    {
        subject.TakeDamage(_magnitude/10);
        if (IsInfinite) return;
        _magnitude -= 1;
        if (_magnitude <= 0)
        {
            subject.RemoveEffect<BurningEffect>();
        }
    }

    // Instant 경로: 카드가 큐에서 다 됐을 때(CardInstance.Play → CharacterManager.ApplyEffect) 호출된다.
    // Burning은 SupportedCategories가 Instant뿐이라 Continuous(OnEnterQueue)로는 쓰이지 않는다.
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
