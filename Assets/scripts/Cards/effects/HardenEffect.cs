using UnityEngine;

public class HardenEffect : Effect
{
    private const int Priority = 2;

    public HardenEffect(int magnitude) : base(EffectType.Harden, magnitude, Priority) { }

    // Harden: every defense gain is increased by a flat amount equal to magnitude
    public override void OnApplying(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Defend)
        {
            effect.Add(_magnitude);
        }
    }
}
