using System;

[Serializable]
public class StrengthEffect : Effect
{
    private const int Priority = 2;

    public StrengthEffect(int magnitude) : base(EffectType.Strength, magnitude, Priority) { }

    // Strength는 자신의 공격력을 강화하는 버프이므로 양수 magnitude는 User, 음수는 Opponent(공격력 약화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    // Strength: permanently increases outgoing damage by a flat amount

    public override void OnApplyingOther(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Add(_magnitude);
        }
    }

}
