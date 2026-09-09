using System;
using UnityEngine;

[Serializable]
public class GuardEffect : Effect
{
    // 항상 가장 먼저 OnAppliedOther가 호출돼야 한다 — 방어(DefendEffect, Priority -1)가 소모하는
    // 공격 magnitude는 Guard의 배율까지 반영된 "실제" 값이어야 하기 때문이다.
    private const int Priority = 99;

    public GuardEffect(int magnitude) : base(EffectType.Guard, magnitude, Priority) { }

    // Guard는 자신이 받는 피해를 줄이는 버프이므로 양수 magnitude는 User, 음수는 Opponent(받는 피해 감소 약화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    // 단순 스택형 상태 효과라 큐에 머무는 동안 유지되는 형태로도 부여할 수 있다.
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    // Instant 경로: 카드가 큐에서 다 됐을 때(CardInstance.Play → CharacterManager.ApplyEffect)
    // 호출된다. Continuous 경로(OnEnterQueue, 여기선 override 안 함)는 이 메서드를 타지 않고 별도
    // continuous 풀(IsInfinite==true)에만 합산하므로, instant 몫은 여기 base 그대로에 맡겨도 항상
    // continuous 몫과 분리된 채 유지된다. Continuous와 대칭이 보이도록 명시적으로 override해둔다.
    public override void OnApply(CharacterManager subject) => base.OnApply(subject);

    // Guard: 지속되는 동안 받는 공격 피해 절반
    public override void OnAppliedOther(CharacterManager b, CardEffect effect, bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(0.5f);
        }
    }

    public override void OnTurnStarted(CharacterManager subject)
    {
        if (IsInfinite) return;
        _magnitude--;
        // 타입이 아니라 이 인스턴스 자신만 제거한다 — 같은 타입의 continuous 풀(IsInfinite==true)이
        // 별도로 공존할 수 있으므로, 타입 기준 RemoveEffect<T>()는 엉뚱한 인스턴스를 지울 수 있다.
        if (_magnitude <= 0)
        {
            subject.RemoveEffectInstance(this);
        }
    }
}
