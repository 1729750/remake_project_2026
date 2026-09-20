using UnityEngine;

// Defend/Harden/Guard 같은 방어 계열 카드의 점수를 크게 올려 방어도부터 쌓는 AI. 공격은 상대적으로 덜 선호한다.
// DefaultEnemyAI의 채점 로직을 그대로 쓰되, EffectType별 점수 배율(GetEffectWeight)만 방어 성향에 맞게 바꾼다.
[CreateAssetMenu(fileName = "DefenseEnemyAI", menuName = "Scriptable Objects/Enemy AI/Defense")]
public class DefenseEnemyAI : DefaultEnemyAI
{
    protected override float GetEffectWeight(EffectType type)
    {
        return type switch
        {
            EffectType.Defend         => 2.5f,
            EffectType.Harden         => 2.0f,
            EffectType.Guard          => 2.0f,
            EffectType.Attack         => 0.6f,
            _ => 1f,
        };
    }
}
