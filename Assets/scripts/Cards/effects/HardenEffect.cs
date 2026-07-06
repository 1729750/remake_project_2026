using UnityEngine;

public class HardenEffect : Effect
{
    public HardenEffect(int magnitude) : base(EffectType.Harden, magnitude) { }

    // Harden: every defense gain is increased by a flat amount equal to magnitude
    public override void OnApplying(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Defend)
        {
            effect.Add(_magnitude);
        }
    }
}
