
using System;

[Serializable]
public class WeakEffect : Effect
{
    private const int Priority = 1;

    public WeakEffect(int magnitude) : base(EffectType.Weak, magnitude, Priority) { }

    // Weak은 가하는 피해를 줄이는 디버프이므로 양수 magnitude는 Opponent, 음수는 User(디버프 완화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    // Weak: target deals reduced damage while applied
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Attack)
        {
            effect.Multiply(1.2f);
        }

        if (!a) return;
        _magnitude--;
        if(_magnitude <= 0)
            subject.RemoveEffect<WeakEffect>();
    }
}
