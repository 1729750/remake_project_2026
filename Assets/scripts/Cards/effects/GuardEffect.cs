using System;

// 큐에 머무는 동안 받는 공격 피해 절반.
[Serializable]
public class GuardEffect : Effect
{
    // 항상 가장 먼저 OnAppliedOther가 호출돼야 한다 — 방어(DefendEffect, Priority -1)가 소모하는
    // 공격 magnitude는 Guard의 배율까지 반영된 "실제" 값이어야 하기 때문이다.
    private const int Priority = 99;

    public GuardEffect(int magnitude) : base(EffectType.Guard, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    public override void OnAppliedOther(CharacterManager b, CardEffect effect, bool a)
    {
        if (effect.IsAttackEffect())
            effect.Multiply(0.5f);
    }
}
