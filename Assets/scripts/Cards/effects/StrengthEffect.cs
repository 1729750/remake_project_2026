using System;

[Serializable]
public class StrengthEffect : Effect
{
    private const int Priority = 2;

    public StrengthEffect(int magnitude) : base(EffectType.Strength, magnitude, Priority) { }

    // Strength: permanently increases outgoing damage by a flat amount

    public override void OnApplying(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Add(_magnitude);
        }
    }

}
