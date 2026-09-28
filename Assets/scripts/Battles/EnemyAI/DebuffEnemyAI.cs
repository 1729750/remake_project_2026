using UnityEngine;

// Vulnerable/Weak/PoseBreak/EnergyDrain/Burning/AddDump처럼 상대에게 상태이상을 거는 카드를 최우선으로 노리는 AI.
// DefaultEnemyAI의 채점 로직을 그대로 쓰되, EffectType별 점수 배율(GetEffectWeight)만 상태이상 성향에 맞게 바꾼다.
[CreateAssetMenu(fileName = "DebuffEnemyAI", menuName = "Scriptable Objects/Enemy AI/Debuff")]
public class DebuffEnemyAI : DefaultEnemyAI
{
    protected override float GetEffectWeight(EffectType type)
    {
        return type switch
        {
            EffectType.Vulnerable     => 2.5f,
            EffectType.Weak           => 2.5f,
            EffectType.PoseBreak      => 2.5f,
            EffectType.EnergyDrain    => 2.5f,
            EffectType.Burning        => 2.5f,
            EffectType.AddDump        => 2.5f,
            EffectType.Attack         => 0.8f,
            EffectType.Defend         => 0.6f,
            _ => 1f,
        };
    }
}
