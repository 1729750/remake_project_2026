using UnityEngine;

// 적 CharacterManager(!playerControlled)가 매 틱 묻는 "지금 뭘 할까"에 답하는 전략 자산.
// CharacterData에 붙여서 캐릭터별로 갈아끼울 수 있도록 CardDefinition과 같은 ScriptableObject로 둔다.
public abstract class EnemyAIBehavior : ScriptableObject
{
    // 손패 0~3번 카드 사용
    public const int ActionPlayCard0 = 0;
    public const int ActionPlayCard1 = 1;
    public const int ActionPlayCard2 = 2;
    public const int ActionPlayCard3 = 3;
    // 방어 자세를 취한다
    public const int ActionDefend = 4;
    // 아무것도 하지 않는다
    public const int ActionNone = 5;

    public abstract int Decide(in BattleSnapshot snapshot);
}
