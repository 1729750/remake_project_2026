using System;
using UnityEngine;

// 화상: AttackEffect의 자식(= class 기반 "공격 계열" 감지에 잡힘 — Strength/Weak/Vulnerable/Guard/
// PoseBreak 등 공격 보정 효과가 그대로 적용된다)이지만, 즉발 피해 대신 방어(큐의 Defend 카드)를
// 무시하는 DOT를 쌓는다. 그 대가로 지속형(스스로 매턴 감쇠)으로 너프되어 있다.
[Serializable]
public class BurningEffect : AttackEffect
{
    public BurningEffect(int magnitude) : base(magnitude) { }

    // Burning은 매 턴 subject에게 피해를 입히는 디버프이므로 양수 magnitude는 Opponent,
    // 음수는 User(디버프 완화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    // 매 턴 감소(OnTurnEnded에서 자체적으로 magnitude를 깎고 0 이하면 제거)로 수명을 관리하는
    // 타입이라 Continuous(큐 잔류 = 수명)로 재해석하면 그 감쇠 로직과 충돌한다 — Instant만 지원.
    public override EffectCategory SupportedCategories => EffectCategory.Instant;

    // 방어(큐의 Defend 카드)를 완전히 무시한다 — ApplyEffect가 OnApply를 부르기 전에 이 플래그를
    // 보고 ConsumeDefense를 건너뛴다.
    public override bool ConsumesDefense => false;

    public override void OnExpired(CharacterManager subject) { }

    public override void OnTurnEnded(CharacterManager subject)
    {
        subject.TakeDamage(_magnitude / 10);
        if (IsInfinite) return;
        _magnitude -= 1;
        if (_magnitude <= 0)
        {
            subject.RemoveEffect<BurningEffect>();
        }
    }

    // 즉발 피해 대신 지속 피해(스택)를 쌓는다 — 맞는 "순간의 타격"이 아니므로 0을 돌려줘
    // OnDamaging(AttackEffect 전용 훅)이 트리거되지 않게 한다.
    protected override int DealDamage(CharacterManager subject)
    {
        if (subject.GetIsGuard())
        {
            _magnitude /= 3;
        }
        ApplyStack(subject); // Effect의 기본 스택(병합/신규 등록) 동작 재사용 — AttackEffect.OnApply는 sealed라 base로는 못 탐
        return 0;
    }

    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
