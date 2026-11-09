using System;

// 큐에 머무는 동안 매 턴 시작마다 코스트를 magnitude만큼 추가로 회복한다.
[Serializable]
public class EnergyHealEffect : Effect
{
    public EnergyHealEffect(int magnitude) : base(EffectType.EnergyHeal, magnitude) { }

    public override void OnTurnStarted(CharacterManager subject)
    {
        subject.EnergyHeal(_magnitude);
    }
}
