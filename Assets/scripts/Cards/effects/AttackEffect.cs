using System;

[Serializable]
public class AttackEffect : Effect
{
    public AttackEffect(int magnitude) : base(EffectType.Attack, magnitude) { }

    public override void OnApply(CharacterManager subject, CardEffect cardEffect)
    {
        subject.Attacked(cardEffect.GetMagnitude());
    }
}
