// Effect.TargetPolarity로 각 Effect(서브클래스)가 내재적으로 갖는 극성. RewardManager.RollEnhanceOption이
// 강화 후보의 EffectTarget을 정할 때 이 값을 읽어와 magnitude 부호와 함께 판단한다.
// Positive: 정수 값이 양수면 User/음수면 Opponent, Negative: 그 반대, Neutral: 랜덤.
public enum EffectTargetPolarity
{
    Positive,
    Negative,
    Neutral
}
