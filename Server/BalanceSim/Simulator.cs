namespace BalanceSim;

/// <summary>한 판에서 한쪽이 카드 하나로 한 일.</summary>
public sealed class CardUsage
{
    public bool Drawn;          // 한 번이라도 손에 들었는지
    public int HandTurns;       // 턴 시작에 손에 있던 횟수
    public int PlayableTurns;   // 그중 바로 낼 수 있던 횟수 (코스트, 큐 빈칸)
    public int Uses;            // 큐에 넣은 횟수
    public int Plays;           // 큐에서 발동한 횟수
    public int Damage;          // 발동해서 상대에게 준 피해 (방어로 막힌 뒤)
}

/// <summary>카드 선택 단계에서 본 선택지 하나.</summary>
public sealed record OfferSeen(string Kind, string Card, bool Picked);

public sealed class SideRecord
{
    public required string Ai { get; init; }
    public List<CardDef> Deck { get; } = new();
    public List<OfferSeen> Offers { get; } = new();
    public int Skips;
    public Dictionary<string, CardUsage> Cards { get; } = new();
    public int Redraws;
    public int Guards;
    public int FinalHp;
    public double MinHpRatio = 1;

    public CardUsage Usage(string card) => Cards.TryGetValue(card, out var u) ? u : Cards[card] = new CardUsage();
}

public sealed class GameRecord
{
    public required SideRecord A { get; init; }
    public required SideRecord B { get; init; }
    public BattleOutcome Outcome;
    public bool Timeout;
    public int Turns;

    /// <summary>A 기준 점수: 승 1, 무 0.5, 패 0.</summary>
    public double ScoreA => Outcome switch
    {
        BattleOutcome.FirstWins => 1,
        BattleOutcome.SecondWins => 0,
        _ => 0.5,
    };
}

/// <summary>
/// AI 둘이 카드 선택 10라운드 → 전투를 한 판 진행한다. 게임 서버와 같은 CardGame.Core 규칙(Draft, Battle)을
/// 1.5초를 기다리지 않고 그대로 밟는다. 같은 seed면 같은 판이 나온다.
/// </summary>
public static class Simulator
{
    public const int MaxTurns = 400;     // 1.5초 턴 기준 10분. 넘으면 무승부(시간 초과)
    private const int MaxActionsPerTurn = 8;

    public static GameRecord Play(CardLibrary library, AiProfile aiA, AiProfile aiB, int seed)
    {
        var random = new Random(seed);
        var a = new SideRecord { Ai = aiA.Name };
        var b = new SideRecord { Ai = aiB.Name };
        a.Deck.AddRange(library.StartDeck);
        b.Deck.AddRange(library.StartDeck);

        for (int round = 0; round < Draft.Rounds; round++)
        {
            DraftRound(library, aiA.Draft, a, random);
            DraftRound(library, aiB.Draft, b, random);
        }

        var events = new List<MatchEvent>();
        var first = new Combatant("A", aiA.Name, library.MaxHealth, a.Deck, random);
        var second = new Combatant("B", aiB.Name, library.MaxHealth, b.Deck, random);
        var battle = new Battle(first, second, events.Add);
        var record = new GameRecord { A = a, B = b };

        while (!battle.IsOver)
        {
            if (battle.Turn + 1 >= MaxTurns)
            {
                battle.ForceDraw();
                record.Timeout = true;
                break;
            }

            battle.StartTurn();
            CountHand(first, a);
            CountHand(second, b);

            // 실제 게임은 두 사람이 같은 1.5초 안에 동시에 움직인다. 순서 유불리가 없게 먼저 움직이는 쪽을 섞는다
            if (random.Next(2) == 0)
            {
                Act(first, aiA.Battle, a, random);
                Act(second, aiB.Battle, b, random);
            }
            else
            {
                Act(second, aiB.Battle, b, random);
                Act(first, aiA.Battle, a, random);
            }

            battle.EndTurn();

            Attribute(events, a, b);
            events.Clear();
            TrackHp(first, a);
            TrackHp(second, b);
        }

        record.Outcome = battle.Outcome;
        record.Turns = battle.Turn + 1;
        a.FinalHp = Math.Max(first.Health, 0);
        b.FinalHp = Math.Max(second.Health, 0);
        return record;
    }

    private static void DraftRound(CardLibrary library, IDraftAi ai, SideRecord side, Random random)
    {
        PickOption[] options = Draft.RollOptions(side.Deck, library, random);
        int? choice = ai.Choose(side.Deck, options, random);

        for (int i = 0; i < options.Length; i++)
        {
            // 강화 선택지는 "어떤 카드가 강화되는지"로 센다
            string card = options[i].Kind == "enhance" ? side.Deck[options[i].Target].Name : options[i].Card.Name;
            side.Offers.Add(new OfferSeen(options[i].Kind, card, choice == i));
        }

        if (choice is int index) Draft.Apply(side.Deck, options[index]);
        else side.Skips++;
    }

    private static void CountHand(Combatant combatant, SideRecord side)
    {
        for (int i = 0; i < Combatant.HandSize; i++)
        {
            CardInstance? card = combatant.Hand[i];
            if (card == null) continue;
            CardUsage usage = side.Usage(card.Def.Name);
            usage.Drawn = true;
            usage.HandTurns++;
            if (combatant.CanPlay(i)) usage.PlayableTurns++;
        }
    }

    private static void Act(Combatant self, IBattleAi ai, SideRecord side, Random random)
    {
        for (int n = 0; n < MaxActionsPerTurn; n++)
        {
            int? action = ai.NextAction(self, random);
            if (action is not int index) return;

            if (index == Combatant.GuardAction) side.Guards++;
            if (index == Combatant.RedrawAction) side.Redraws++;

            CardInstance? before = index >= 0 && index < Combatant.HandSize ? self.Hand[index] : null;
            self.SelectCard(index);

            // 가드/리드로우는 한 턴에 한 번, 카드를 못 냈으면(손에 그대로) 이번 턴은 끝
            if (index < 0 || (before != null && self.Hand[index] == before)) return;
        }
    }

    // 발동(play) 뒤에 이어지는 피격(damage)을 그 카드의 피해로 센다
    private static void Attribute(List<MatchEvent> events, SideRecord a, SideRecord b)
    {
        SideRecord SideOf(string id) => id == "A" ? a : b;
        (SideRecord side, string card, string by)? current = null;

        foreach (MatchEvent e in events)
        {
            switch (e.Kind)
            {
                case "use" when e.Ok && e.Card != null:
                    SideOf(e.PlayerId).Usage(e.Card).Uses++;
                    break;
                case "play" when e.Card != null:
                    SideOf(e.PlayerId).Usage(e.Card).Plays++;
                    current = (SideOf(e.PlayerId), e.Card, e.PlayerId);
                    break;
                case "damage" when current != null && e.PlayerId != current.Value.by:
                    current.Value.side.Usage(current.Value.card).Damage += e.Amount;
                    break;
                case "turnEnd":
                    current = null;
                    break;
            }
        }
    }

    private static void TrackHp(Combatant combatant, SideRecord side) =>
        side.MinHpRatio = Math.Min(side.MinHpRatio, Math.Max(combatant.Health, 0) / (double)combatant.MaxHealth);
}
