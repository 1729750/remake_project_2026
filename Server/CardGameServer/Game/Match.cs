namespace CardGameServer.Game;

public enum MatchPhase { Picking, Countdown, Battle, Finished }

/// <summary>카드 선택 단계의 선택지. Kind = add(덱에 추가) | enhance(Target번 덱 카드를 Card로 바꿈 = 강화 후 모습)</summary>
public sealed record PickOption(string Kind, CardDef Card, int Target);

/// <summary>
/// 방 하나의 게임 진행: 카드 선택(라운드마다 3장 중 1장, 또는 건너뛰기) → 카운트다운 → 전투 → 결과.
/// 모든 판정은 여기서 하고, 바뀔 때마다 두 플레이어에게 각자 시점의 상태를 보낸다.
///
/// 스레드: Tick(타이머)과 입력(소켓)이 다른 스레드에서 오므로 공개 메서드는 전부 lock 안에서 처리한다.
/// </summary>
public sealed class Match
{
    public const int PickRounds = 10;
    public const double PickSeconds = 15;
    public const double CountdownSeconds = 3;   // Unity BattleManager.startDelay
    public const double TurnSeconds = 1.5;      // SinglePlayMode 씬의 turnDuration

    private readonly object gate = new();
    private readonly CardLibrary library;
    private readonly Random random = new();
    private readonly Seat[] seats;

    private MatchPhase phase = MatchPhase.Picking;
    private int round;
    private double timeLeft;
    private int turn = -1;
    private string result = "";      // winner playerId, "draw"
    private string reason = "";
    private readonly List<MatchEvent> events = new();

    public bool IsFinished
    {
        get { lock (gate) return phase == MatchPhase.Finished; }
    }

    private sealed class Seat(PlayerConnection player, IEnumerable<CardDef> startDeck)
    {
        public PlayerConnection Player { get; } = player;
        public List<CardDef> Deck { get; } = startDeck.ToList();
        public PickOption[] Offers { get; set; } = [];
        public bool Picked { get; set; }
        public bool WantsRematch { get; set; }
        public Combatant? Combatant { get; set; }
    }

    public Match(CardLibrary library, PlayerConnection first, PlayerConnection second)
    {
        this.library = library;
        seats = [new Seat(first, library.StartDeck), new Seat(second, library.StartDeck)];
    }

    public void Start()
    {
        lock (gate)
        {
            StartPickRound();
            BroadcastState();
        }
    }

    // =========================================================
    // Tick (Room 루프에서 주기적으로 호출)
    // =========================================================

    public void Tick(double deltaSeconds)
    {
        lock (gate)
        {
            if (phase == MatchPhase.Finished) return;

            timeLeft -= deltaSeconds;
            if (timeLeft > 0) return;

            // 타이머는 50ms마다 확인하므로 조금 넘쳐서 끝난다. 넘친 만큼 다음 단계에서 빼서 턴 길이가 밀리지 않게 한다
            double overshoot = -timeLeft;

            switch (phase)
            {
                case MatchPhase.Picking:
                    // 시간 안에 못 고른 사람은 무작위로 골라 준다
                    foreach (Seat seat in seats.Where(s => !s.Picked))
                        Pick(seat, random.Next(seat.Offers.Length));
                    AdvancePicking();
                    break;

                case MatchPhase.Countdown:
                    StartBattle();
                    break;

                case MatchPhase.Battle:
                    EndTurn();
                    break;
            }

            if (phase != MatchPhase.Finished)
                timeLeft = Math.Max(timeLeft - overshoot, 0.01);

            BroadcastState();
        }
    }

    // =========================================================
    // Input
    // =========================================================

    public void HandlePick(PlayerConnection player, int index)
    {
        lock (gate)
        {
            Seat? seat = Find(player);
            if (seat == null || phase != MatchPhase.Picking || seat.Picked) return;
            if (index < 0 || index >= seat.Offers.Length) return;

            Pick(seat, index);
            if (seats.All(s => s.Picked))
                AdvancePicking();
            BroadcastState();
        }
    }

    /// <summary>이번 라운드는 카드를 받지 않는다.</summary>
    public void HandleSkip(PlayerConnection player)
    {
        lock (gate)
        {
            Seat? seat = Find(player);
            if (seat == null || phase != MatchPhase.Picking || seat.Picked) return;

            seat.Picked = true;
            if (seats.All(s => s.Picked))
                AdvancePicking();
            BroadcastState();
        }
    }

    public void HandlePlay(PlayerConnection player, int index)
    {
        lock (gate)
        {
            Seat? seat = Find(player);
            if (seat?.Combatant == null || phase != MatchPhase.Battle) return;

            seat.Combatant.SelectCard(index);
            BroadcastState();
        }
    }

    /// <summary>둘 다 요청하면 true (방이 새 Match를 만든다).</summary>
    public bool HandleRematch(PlayerConnection player)
    {
        lock (gate)
        {
            Seat? seat = Find(player);
            if (seat == null || phase != MatchPhase.Finished) return false;

            seat.WantsRematch = true;
            BroadcastState();
            return seats.All(s => s.WantsRematch);
        }
    }

    public void Forfeit(PlayerConnection leaver)
    {
        lock (gate)
        {
            if (phase == MatchPhase.Finished) return;
            Seat? other = seats.FirstOrDefault(s => s.Player != leaver);
            Finish(other?.Player.PlayerId ?? "draw", "상대가 나갔습니다.");
            BroadcastState();
        }
    }

    /// <summary>서버 쪽 오류로 게임을 이어갈 수 없을 때.</summary>
    public void Abort(string why)
    {
        lock (gate)
        {
            if (phase == MatchPhase.Finished) return;
            Finish("draw", why);
            BroadcastState();
        }
    }

    // =========================================================
    // Picking
    // =========================================================

    private void StartPickRound()
    {
        phase = MatchPhase.Picking;
        round++;
        timeLeft = PickSeconds;

        foreach (Seat seat in seats)
        {
            seat.Picked = false;
            seat.Offers = RollOptions(seat);
        }
    }

    // 팀 멀티 기획(CardOptionGenerator)과 같은 구성: [카드 추가, 강화, 무작위(강화 또는 추가)]
    // 강화할 카드가 없으면 그 칸은 카드 추가로 바뀐다
    private PickOption[] RollOptions(Seat seat)
    {
        PickOption AddCard() => new("add", library.Cards[random.Next(library.Cards.Count)], -1);

        PickOption EnhanceOrAdd()
        {
            var rolled = Enhance.Roll(seat.Deck, library, random);
            if (rolled == null) return AddCard();
            var (target, upgrade) = rolled.Value;
            return new PickOption("enhance", Enhance.Apply(seat.Deck[target], upgrade), target);
        }

        return [AddCard(), EnhanceOrAdd(), random.Next(2) == 0 ? EnhanceOrAdd() : AddCard()];
    }

    private static void Pick(Seat seat, int index)
    {
        PickOption option = seat.Offers[index];
        if (option.Kind == "enhance" && option.Target >= 0 && option.Target < seat.Deck.Count)
            seat.Deck[option.Target] = option.Card; // 같은 카드 자리에 강화된 카드
        else
            seat.Deck.Add(option.Card);
        seat.Picked = true;
    }

    private void AdvancePicking()
    {
        if (round < PickRounds)
        {
            StartPickRound();
            return;
        }

        phase = MatchPhase.Countdown;
        timeLeft = CountdownSeconds;
    }

    // =========================================================
    // Battle (Unity BattleManager)
    // =========================================================

    private void StartBattle()
    {
        foreach (Seat seat in seats)
            seat.Combatant = new Combatant(seat.Player.PlayerId, seat.Player.Name, library.MaxHealth, seat.Deck, random)
            {
                Events = events.Add,
            };

        seats[0].Combatant!.Opponent = seats[1].Combatant!;
        seats[1].Combatant!.Opponent = seats[0].Combatant!;

        phase = MatchPhase.Battle;
        StartTurn();
    }

    private void StartTurn()
    {
        turn++;
        timeLeft = TurnSeconds;
        foreach (Seat seat in seats)
            seat.Combatant!.OnTurnStart();
    }

    private void EndTurn()
    {
        // Unity는 플레이어 쪽이 먼저 처리되고 먼저 죽은 쪽이 지는데, 멀티에서는 순서가 불공평하니
        // 양쪽 턴 종료를 모두 처리한 뒤 한꺼번에 판정한다 (둘 다 쓰러지면 무승부).
        events.Add(new MatchEvent("turnEnd", "", null, 0, null, false, false));
        foreach (Seat seat in seats)
            seat.Combatant!.OnTurnEnd();

        bool firstDown = seats[0].Combatant!.IsDefeated;
        bool secondDown = seats[1].Combatant!.IsDefeated;

        if (firstDown && secondDown)
            Finish("draw", "둘 다 쓰러졌습니다.");
        else if (firstDown)
            Finish(seats[1].Player.PlayerId, $"{seats[0].Player.Name}의 HP가 0이 되었습니다.");
        else if (secondDown)
            Finish(seats[0].Player.PlayerId, $"{seats[1].Player.Name}의 HP가 0이 되었습니다.");
        else
            StartTurn();
    }

    private void Finish(string winner, string why)
    {
        phase = MatchPhase.Finished;
        result = winner;
        reason = why;
        timeLeft = 0;
    }

    // =========================================================
    // State
    // =========================================================

    private Seat? Find(PlayerConnection player) => seats.FirstOrDefault(s => s.Player == player);

    private void BroadcastState()
    {
        foreach (Seat seat in seats)
            seat.Player.Send(new ServerMessage("state", State: BuildState(seat)));
        events.Clear();
    }

    private StateView BuildState(Seat seat)
    {
        Seat other = seats[0] == seat ? seats[1] : seats[0];

        return new StateView
        {
            Phase = phase.ToString().ToLowerInvariant(),
            OpponentName = other.Player.Name,
            TimeLeft = Math.Max(timeLeft, 0),
            Round = round,
            TotalRounds = PickRounds,
            Offers = phase == MatchPhase.Picking
                ? seat.Offers.Select((o, i) => CardView.FromDef(o.Card, i + 1) with { OfferKind = o.Kind, Target = o.Target }).ToArray()
                : [],
            Picked = seat.Picked,
            OpponentPicked = other.Picked,
            MyDeck = seat.Deck.Select((c, i) => CardView.FromDef(c, i + 1)).ToArray(),
            Turn = turn,
            TurnDuration = TurnSeconds,
            Me = seat.Combatant == null ? null : BuildCombatant(seat.Combatant, showHand: true),
            Opponent = other.Combatant == null ? null : BuildCombatant(other.Combatant, showHand: false),
            Result = phase != MatchPhase.Finished ? "" :
                result == "draw" ? "draw" :
                result == seat.Player.PlayerId ? "win" : "lose",
            Reason = reason,
            RematchRequested = seat.WantsRematch,
            OpponentRematchRequested = other.WantsRematch,
            Events = events.Select(e => new EventView(e.Kind, e.PlayerId == seat.Player.PlayerId, e.Card ?? "", e.Amount,
                e.Effect ?? "", e.Guarded, e.Ok)).ToArray(),
        };
    }

    private static CombatantView BuildCombatant(Combatant c, bool showHand)
    {
        bool queueHasSpace = c.Queue.Any(q => q == null);

        return new CombatantView
        {
            Name = c.Name,
            Hp = Math.Max(c.Health, 0),
            MaxHp = c.MaxHealth,
            Defense = c.GetDefense(),
            Cost = c.Cost,
            MaxCost = Combatant.MaxCost,
            Guard = c.IsGuard,
            SelectedIndex = showHand ? c.SelectedIndex : -1,
            DeckCount = c.Deck.Count,
            HandCount = c.Hand.Count(h => h != null),
            Effects = c.Effects.Select(e => new EffectView(e.Type.ToString(), e.Magnitude, e.IsInfinite)).ToArray(),
            Hand = c.Hand.Select(h => h == null ? CardView.Empty
                : showHand ? CardView.FromHand(h, c, queueHasSpace) : CardView.Hidden(h)).ToArray(),
            Queue = c.Queue.Where(q => q != null).Select(q => CardView.FromQueue(q!)).ToArray(),
        };
    }
}
