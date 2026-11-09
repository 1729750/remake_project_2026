namespace CardGame.Core;

public enum BattleOutcome { None, FirstWins, SecondWins, Draw }

/// <summary>
/// 전투 한 판의 턴 진행 (Unity BattleManager의 TurnStart / TurnEnd).
/// 게임 서버(Match)는 1.5초 타이머로, BalanceSim은 기다림 없이 이 순서를 그대로 밟는다.
/// </summary>
public sealed class Battle
{
    private readonly Action<MatchEvent>? events;

    public Combatant First { get; }
    public Combatant Second { get; }
    public int Turn { get; private set; } = -1;
    public BattleOutcome Outcome { get; private set; }
    public bool IsOver => Outcome != BattleOutcome.None;

    public Combatant? Winner => Outcome switch
    {
        BattleOutcome.FirstWins => First,
        BattleOutcome.SecondWins => Second,
        _ => null,
    };

    public Battle(Combatant first, Combatant second, Action<MatchEvent>? events = null)
    {
        First = first;
        Second = second;
        this.events = events;
        first.Opponent = second;
        second.Opponent = first;
        first.Events = events;
        second.Events = events;
    }

    public void StartTurn()
    {
        Turn++;
        First.OnTurnStart();
        Second.OnTurnStart();
    }

    /// <summary>
    /// 턴 종료. Unity는 플레이어 쪽이 먼저 처리되고 먼저 죽은 쪽이 지는데, 멀티에서는 순서가 불공평하니
    /// 양쪽 턴 종료를 모두 처리한 뒤 한꺼번에 판정한다 (둘 다 쓰러지면 무승부). 끝났으면 true.
    /// </summary>
    public bool EndTurn()
    {
        events?.Invoke(new MatchEvent("turnEnd", "", null, 0, null, false, false));
        First.OnTurnEnd();
        Second.OnTurnEnd();

        bool firstDown = First.IsDefeated;
        bool secondDown = Second.IsDefeated;

        if (firstDown && secondDown) Outcome = BattleOutcome.Draw;
        else if (firstDown) Outcome = BattleOutcome.SecondWins;
        else if (secondDown) Outcome = BattleOutcome.FirstWins;

        return IsOver;
    }

    /// <summary>시간 제한 등으로 끝낼 때 (BalanceSim의 최대 턴 수).</summary>
    public void ForceDraw() => Outcome = BattleOutcome.Draw;
}
