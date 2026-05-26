public class Effect
{
    private EffectType _effectType;
    protected int _magnitude;

    public Effect(EffectType effectType, int magnitude)
    {
        _effectType = effectType;
        _magnitude = magnitude;
    }

    public EffectType GetEffectType() => _effectType;
    public int GetMagnitude() => _magnitude;
    
    public void AddMagnitude(int magnitude) => _magnitude += magnitude;
    public virtual void OnApplied(BattleManager subject) { }
    public virtual void OnExpired(BattleManager subject) { }
    public virtual void OnTurnStarted(BattleManager subject) { }
    public virtual void OnTurnEnded(BattleManager subject) { }
    public virtual void OnAttacking(BattleManager subject) { }
    public virtual void OnAttacked(BattleManager subject) { }
}
