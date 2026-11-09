using System;

// 큐에 머무는 동안 대상이 코스트를 회복할 때마다 회복량이 magnitude만큼 줄어든다(0 밑으로는 안 내려감).
// 실제 차감은 CharacterManager.EnergyHeal이 대상에게 붙은 이 효과들의 magnitude 합으로 처리한다.
[Serializable]
public class EnergyDrainEffect : Effect
{
    public EnergyDrainEffect(int magnitude) : base(EffectType.EnergyDrain, magnitude) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;
}
