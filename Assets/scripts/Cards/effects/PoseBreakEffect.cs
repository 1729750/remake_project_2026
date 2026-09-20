using System;

[Serializable]
public class PoseBreakEffect:Effect
{
    
    private const int Priority = 4;

    public PoseBreakEffect(int magnitude) : base(EffectType.PoseBreak, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    // 단순 스택형 상태 효과라 큐에 머무는 동안 유지되는 형태로도 부여할 수 있다.
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    // Instant 경로: 카드가 큐에서 다 됐을 때(CardInstance.Play → CharacterManager.ApplyEffect)
    // 호출된다. Continuous 경로(OnEnterQueue, 여기선 override 안 함)는 이 메서드를 타지 않고 별도
    // continuous 풀(IsInfinite==true)에만 합산하므로, instant 몫은 여기 base 그대로에 맡겨도 항상
    // continuous 몫과 분리된 채 유지된다. Continuous와 대칭이 보이도록 명시적으로 override해둔다.
    public override void OnApply(CharacterManager subject) => base.OnApply(subject);

    // PoseBreak: target deals reduced damage while applied
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(2f);
        }
    }

    public override void OnTurnStarted(CharacterManager subject)
    {
        if (IsInfinite) return;
        _magnitude--;
        // 타입이 아니라 이 인스턴스 자신만 제거한다 — 같은 타입의 continuous 풀(IsInfinite==true)이
        // 별도로 공존할 수 있으므로, 타입 기준 RemoveEffect<T>()는 엉뚱한 인스턴스를 지울 수 있다.
        if(_magnitude <= 0)
            subject.RemoveEffectInstance(this);
    }
}
