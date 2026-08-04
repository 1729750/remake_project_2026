using UnityEngine;

public class HardenEffect : Effect
{
    private const int Priority = 2;

    public HardenEffect(int magnitude) : base(EffectType.Harden, magnitude, Priority) { }

    // Harden은 자신의 방어 획득을 강화하는 버프이므로 양수 magnitude는 User, 음수는 Opponent(방어 획득 약화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    // Harden: every defense gain is increased by a flat amount equal to magnitude
    public override void OnApplyingOther(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Defend)
        {
            effect.Add(_magnitude);
        }
    }
}
