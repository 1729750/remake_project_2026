public class WeakEffect : Effect
{
    public WeakEffect(int magnitude) : base(EffectType.Weak,magnitude) { }

    // Weak: target deals reduced damage while applied
    public override void OnApplied(BattleManager subject)
    {
        subject.MultiplyDamageMultiplier(0.8f);
    }

    public override void OnTurnEnded(BattleManager subject)
    {
        _magnitude--;
        if(_magnitude <= 0)
            subject.RemoveEffect<WeakEffect>();
    }

    public override void OnExpired(BattleManager subject)
    {
        subject.MultiplyDamageMultiplier(1f / 0.8f);  
    }
}
