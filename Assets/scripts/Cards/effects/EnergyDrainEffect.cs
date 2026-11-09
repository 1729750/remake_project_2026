using System;

// 충전형 코스트 흡수: magnitude = 남은 충전 수. CharacterManager.EnergyHeal이 코스트를 회복시킬
// 때마다(회복량>0) 이 효과가 있으면 회복량을 1 깎고(0 밑으로는 안 내려감) 충전을 1 소모한다.
// 충전이 0이 되면 사라진다. 매턴 자동으로 깎던 옛 방식(Continuous 지원) 대신 Instant 전용으로
// 단순화했다 — "에너지 흡수는 즉발화" 요청사항.
[Serializable]
public class EnergyDrainEffect : Effect
{
    public EnergyDrainEffect(int magnitude) : base(EffectType.EnergyDrain, magnitude) { }

    // EnergyDrain은 subject의 코스트 회복을 깎는 디버프이므로 양수 magnitude는 Opponent,
    // 음수는 User(디버프 완화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override EffectCategory SupportedCategories => EffectCategory.Instant;

    // 충전을 하나 소모한다. 남은 충전이 0이 되면 스스로 제거한다.
    public void ConsumeCharge(CharacterManager subject)
    {
        _magnitude -= 1;
        if (_magnitude <= 0)
            subject.RemoveEffectInstance(this);
    }
}
