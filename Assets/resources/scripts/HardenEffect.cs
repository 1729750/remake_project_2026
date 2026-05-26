public class HardenEffect : Effect
{
    public HardenEffect(int magnitude) : base(EffectType.Harden, magnitude) { }

    // Harden: every defense gain is increased by a flat amount equal to magnitude
    public override void OnApplied(BattleManager subject)
    {
        subject.AddDefenseAdder(_magnitude);
    }

    public override void OnExpired(BattleManager subject)
    {
        subject.AddDefenseAdder(-_magnitude);
    }
}
