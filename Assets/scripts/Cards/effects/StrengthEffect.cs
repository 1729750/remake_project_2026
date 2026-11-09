using System;

// 큐에 머무는 동안 자신의 공격 피해를 magnitude만큼 더한다(음수면 상대에게 거는 약화 디버프).
[Serializable]
public class StrengthEffect : Effect
{
    private const int Priority = 2;

    public StrengthEffect(int magnitude) : base(EffectType.Strength, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    public override void OnApplyingOther(CharacterManager b, CardEffect effect, bool a)
    {
        if (effect.IsAttackEffect())
            effect.Add(_magnitude);
    }
}
