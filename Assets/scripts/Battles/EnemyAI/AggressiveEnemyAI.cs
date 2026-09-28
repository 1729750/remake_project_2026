using UnityEngine;

// Attack 카드와 공격력 강화(Strength/Vulnerable)를 우선하고 방어는 거의 쓰지 않는 공격 일변도 AI.
// DefaultEnemyAI의 채점 로직을 그대로 쓰되, EffectType별 점수 배율(GetEffectWeight)만 공격 성향에 맞게 바꾼다.
[CreateAssetMenu(fileName = "AggressiveEnemyAI", menuName = "Scriptable Objects/Enemy AI/Aggressive")]
public class AggressiveEnemyAI : DefaultEnemyAI
{
    protected override float GetEffectWeight(EffectType type)
    {
        return type switch
        {
            EffectType.Attack         => 2.5f,
            EffectType.Strength       => 1.5f,
            EffectType.Vulnerable     => 1.5f,
            EffectType.Defend         => 0.3f,
            EffectType.Harden         => 0.3f,
            EffectType.Guard          => 0.3f,
            _ => 1f,
        };
    }
}
