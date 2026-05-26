public class StrengthEffect : Effect
{
    public StrengthEffect(int magnitude) : base(EffectType.Strength, magnitude) { }

    // Strength: permanently increases outgoing damage by a flat amount
    public override void OnApplied(BattleManager subject)
    {
        subject.AddDamageAdder(_magnitude);
    }

    public override void OnExpired(BattleManager subject)
    {
        subject.AddDamageAdder(-_magnitude);
    }
}
