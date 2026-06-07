
using System;

[Serializable]
public class WeakEffect : Effect
{
    public WeakEffect(int magnitude) : base(EffectType.Weak,magnitude) { }

    // Weak: target deals reduced damage while applied
    public override void OnApplying(BattleManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(0.8f);
        }
    }
    public override void OnTurnEnded(BattleManager subject)
    {
        _magnitude--;
        if(_magnitude <= 0)
            subject.RemoveEffect<WeakEffect>();
    }
}
