using System;

// 큐에 머무는 동안 새로 큐에 올리는 방어 카드의 방어도를 magnitude만큼 더한다(음수면 상대에게 거는 디버프).
[Serializable]
public class HardenEffect : Effect
{
    private const int Priority = 2;

    public HardenEffect(int magnitude) : base(EffectType.Harden, magnitude, Priority) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    public override void OnApplyingOther(CharacterManager b, CardEffect effect, bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Defend)
            effect.Add(_magnitude);
    }
}
