
using System;

[Serializable]
public class WeakEffect : Effect
{
    private const int Priority = 1;

    public WeakEffect(int magnitude) : base(EffectType.Weak, magnitude, Priority) { }

    // Weak: target deals reduced damage while applied
    public override void OnApplying(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(0.8f);
        }
    }
    public override void OnTurnEnded(CharacterManager subject)
    {
        _magnitude--;
        if(_magnitude <= 0)
            subject.RemoveEffect<WeakEffect>();
    }
}
