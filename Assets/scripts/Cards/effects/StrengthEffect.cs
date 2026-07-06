using System;

[Serializable]
public class StrengthEffect : Effect
{
    public StrengthEffect(int magnitude) : base(EffectType.Strength, magnitude) { }

    // Strength: permanently increases outgoing damage by a flat amount

    public override void OnApplying(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Add(_magnitude);
        }
    }

}
