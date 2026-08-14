using System;

[Serializable]
public class PoseBreakEffect:Effect
{
    
    private const int Priority = 4;

    public PoseBreakEffect(int magnitude) : base(EffectType.PoseBreak, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    // PoseBreak: target deals reduced damage while applied
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(2f);
        }
    }

    public override void OnTurnStarted(CharacterManager subject)
    {
        _magnitude--;
        if(_magnitude <= 0)
            subject.RemoveEffect<PoseBreakEffect>();
    }
}
