using System;

[Serializable]
public class EnergyDrainEffect : Effect
{
    public EnergyDrainEffect(int magnitude) : base(EffectType.EnergyDrain, magnitude) { }

    // EnergyDrain은 subject의 코스트 회복을 매 턴 깎는 디버프이므로 양수 magnitude는 Opponent,
    // 음수는 User(디버프 완화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override void OnTurnStarted(CharacterManager subject)
    {
        subject.EnergyHeal(-1);
        _magnitude -= 1;
        if (_magnitude <= 0)
            subject.RemoveEffect<EnergyDrainEffect>();
    }
}
