using System;

[Serializable]
public class DefendEffect : Effect
{
    public DefendEffect(int magnitude) : base(EffectType.Defend, magnitude) { }

    public override void OnApply(CharacterManager subject)
    {
        subject.AddDefense(_magnitude);
    }
}
