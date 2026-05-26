public class VulnerableEffect : Effect
{
    public VulnerableEffect(int magnitude) : base(EffectType.Vulnerable, magnitude) { }

    // Vulnerable: target takes increased damage while applied
    public override void OnApplied(BattleManager subject)
    {
        subject.MultiplyDamageMultiplier(1.5f);
    }

    public override void OnTurnEnded(BattleManager subject)
    {
        _magnitude--;
        if (_magnitude <= 0)
            subject.RemoveEffect<VulnerableEffect>();
    }

    public override void OnExpired(BattleManager subject)
    {
        subject.MultiplyDamageMultiplier(1f / 1.5f);
    }
}
