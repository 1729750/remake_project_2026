using System.Collections.Generic;

// EnemyAIBehavior.Decide에 매 틱 전달되는 전투 상황 스냅샷. 카드 해석(CardInstance.ResolveUse 등)이
// 여전히 CharacterManager 인스턴스를 필요로 하므로 Self/Opponent 참조 자체도 담아 두지만, 대부분의
// 판단은 아래의 평면 데이터(체력/코스트/이펙트/손패/큐/덱/턴 정보)만으로 할 수 있게 구성했다.
public readonly struct BattleSnapshot
{
    public readonly CharacterManager Self;
    public readonly CharacterManager Opponent;

    public readonly int SelfHealth;
    public readonly int SelfMaxHealth;
    public readonly int SelfDefense;
    public readonly int SelfCost;
    public readonly Effect[] SelfEffects;
    public readonly CardInstance[] SelfHand;
    public readonly CardInstance[] SelfQueue;
    public readonly bool SelfQueueHasFreeSlot;
    public readonly IReadOnlyList<CardInstance> SelfDeck;

    public readonly int OpponentHealth;
    public readonly int OpponentMaxHealth;
    public readonly int OpponentDefense;
    public readonly Effect[] OpponentEffects;
    public readonly CardInstance[] OpponentQueue;

    public readonly int CurrentTurn;
    public readonly float TurnDuration;
    public readonly float RemainingTurnTime;

    public BattleSnapshot(
        CharacterManager self,
        CharacterManager opponent,
        int selfHealth, int selfMaxHealth, int selfDefense, int selfCost,
        Effect[] selfEffects, CardInstance[] selfHand, CardInstance[] selfQueue,
        bool selfQueueHasFreeSlot, IReadOnlyList<CardInstance> selfDeck,
        int opponentHealth, int opponentMaxHealth, int opponentDefense,
        Effect[] opponentEffects, CardInstance[] opponentQueue,
        int currentTurn, float turnDuration, float remainingTurnTime)
    {
        Self = self;
        Opponent = opponent;
        SelfHealth = selfHealth;
        SelfMaxHealth = selfMaxHealth;
        SelfDefense = selfDefense;
        SelfCost = selfCost;
        SelfEffects = selfEffects;
        SelfHand = selfHand;
        SelfQueue = selfQueue;
        SelfQueueHasFreeSlot = selfQueueHasFreeSlot;
        SelfDeck = selfDeck;
        OpponentHealth = opponentHealth;
        OpponentMaxHealth = opponentMaxHealth;
        OpponentDefense = opponentDefense;
        OpponentEffects = opponentEffects;
        OpponentQueue = opponentQueue;
        CurrentTurn = currentTurn;
        TurnDuration = turnDuration;
        RemainingTurnTime = remainingTurnTime;
    }
}
