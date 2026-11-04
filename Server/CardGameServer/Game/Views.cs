namespace CardGameServer.Game;

// 클라이언트로 보내는 상태 스냅샷. Unity JsonUtility로 읽기 때문에
//  - 배열 안에 null을 넣지 않는다 (빈 손패 칸은 name == "" 인 카드)
//  - enum은 문자열로 보낸다

public sealed record StateView
{
    public required string Phase { get; init; }              // picking | countdown | battle | finished
    public string OpponentName { get; init; } = "";
    public double TimeLeft { get; init; }                    // 현재 단계(선택 라운드/카운트다운/턴)의 남은 초

    // picking
    public int Round { get; init; }
    public int TotalRounds { get; init; }
    public CardView[] Offers { get; init; } = [];
    public bool Picked { get; init; }
    public bool OpponentPicked { get; init; }
    public string[] MyDeck { get; init; } = [];

    // battle
    public int Turn { get; init; }
    public double TurnDuration { get; init; }
    public CombatantView? Me { get; init; }
    public CombatantView? Opponent { get; init; }

    // finished
    public string Result { get; init; } = "";                // win | lose | draw
    public string Reason { get; init; } = "";
    public bool RematchRequested { get; init; }
    public bool OpponentRematchRequested { get; init; }
}

public sealed record CombatantView
{
    public required string Name { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Defense { get; init; }
    public int Cost { get; init; }
    public int MaxCost { get; init; }
    public bool Guard { get; init; }
    public int SelectedIndex { get; init; }
    public int DeckCount { get; init; }
    public int HandCount { get; init; }
    public EffectView[] Effects { get; init; } = [];
    public CardView[] Hand { get; init; } = [];              // 상대 것은 Id만 (뒷면 표시용)
    public CardView[] Queue { get; init; } = [];
}

public sealed record EffectView(string Type, int Magnitude, bool Infinite);

public sealed record CardEffectView(string Type, int Magnitude, string Target, string Category);

public sealed record CardView
{
    public int Id { get; init; }                             // 0이면 빈 칸
    public string Name { get; init; } = "";                  // 상대 손패는 Id만 있고 Name은 ""
    public string CardType { get; init; } = "";
    public int Cost { get; init; }
    public int Cooldown { get; init; }
    public int CooldownLeft { get; init; }
    public bool Playable { get; init; }
    public CardEffectView[] Effects { get; init; } = [];

    public static readonly CardView Empty = new();

    private static string CategoryName(EffectCategory category) =>
        category == (EffectCategory.Instant | EffectCategory.Continuous) ? "Mix" : category.ToString();

    /// <summary>선택지처럼 아직 덱에 없는 카드: 정의 그대로.</summary>
    public static CardView FromDef(CardDef def, int id) => new()
    {
        Id = id,
        Name = def.Name,
        CardType = def.CardType.ToString(),
        Cost = def.Cost,
        Cooldown = def.Cooldown,
        CooldownLeft = def.Cooldown,
        Effects = def.Effects.Select(e => new CardEffectView(e.Type.ToString(), e.Magnitude, e.Target.ToString(),
            CategoryName(new CardEffect(e).GetAppliedCategory()))).ToArray(),
    };

    /// <summary>손패: 지금 버프를 반영한 코스트/수치와 사용 가능 여부.</summary>
    public static CardView FromHand(CardInstance card, Combatant owner, bool queueHasSpace)
    {
        var (cost, magnitudes) = owner.Preview(card);
        return FromInstance(card, cost, magnitudes) with { Playable = queueHasSpace && cost <= owner.Cost };
    }

    public static CardView FromQueue(CardInstance card) =>
        FromInstance(card, card.GetCost(), card.Effects.Select(e => e.Type == EffectType.Defend
            ? card.GetQueuedMagnitude(e)
            : e.Def.Magnitude).ToArray());

    public static CardView Hidden(CardInstance card) => new() { Id = card.Id };

    private static CardView FromInstance(CardInstance card, int cost, int[] magnitudes) => new()
    {
        Id = card.Id,
        Name = card.Def.Name,
        CardType = card.Def.CardType.ToString(),
        Cost = cost,
        Cooldown = card.Cooldown,
        CooldownLeft = card.CooldownLeft,
        Effects = card.Effects.Select((e, i) => new CardEffectView(e.Type.ToString(), magnitudes[i],
            e.Def.Target.ToString(), CategoryName(e.GetAppliedCategory()))).ToArray(),
    };
}
