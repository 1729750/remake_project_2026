namespace BalanceSim;

/// <summary>AI 한 판 관전용 기록: 카드 선택 10라운드와 전투 매 턴의 상태/로그.</summary>
public sealed class Replay
{
    public string AiA { get; set; } = "";
    public string AiB { get; set; } = "";
    public int Seed { get; set; }
    public string Mode { get; set; } = "";
    public List<DraftPick> DraftA { get; } = new();
    public List<DraftPick> DraftB { get; } = new();
    public List<string> DeckA { get; set; } = new();
    public List<string> DeckB { get; set; } = new();
    public List<TurnFrame> Turns { get; } = new();
    public string Outcome { get; set; } = "";

    public static string Label(CardDef card) => card.Level > 0 ? $"{card.Name}+{card.Level}" : card.Name;

    public static string Describe(CardDef card) =>
        $"{Label(card)} (코스트 {card.Cost}, 쿨 {card.Cooldown}: {string.Join(", ", card.Effects.Select(e => $"{e.Type} {e.Magnitude}{(e.Target == EffectTarget.User ? "" : "→상대")}"))})";

    public static SideFrame Capture(Combatant c) => new(
        Math.Max(c.Health, 0), c.MaxHealth, c.Cost, c.GetDefense(), c.IsGuard,
        c.Effects.Select(e => $"{e.Type} {e.Magnitude}{(e.IsInfinite ? "∞" : "")}").ToList(),
        c.Hand.Select(h => h == null ? "" : Label(h.Def)).ToList(),
        c.Queue.Where(q => q != null).Select(q => new QueueCard(Label(q!.Def), q.CooldownLeft)).ToList(),
        c.Deck.Count);

    public static string Describe(MatchEvent e) => e.Kind switch
    {
        "use" => e.Ok ? $"{e.PlayerId}: {e.Card} 큐에 넣음" : $"{e.PlayerId}: {e.Card} 코스트 부족",
        "play" => $"{e.PlayerId}: {e.Card} 발동",
        "damage" => $"{e.PlayerId}: 피해 {e.Amount}{(e.Guarded ? " (가드)" : "")}",
        "shield" => $"{e.PlayerId}: 방어로 {e.Amount} 막음",
        "effect" => $"{e.PlayerId}: {e.Effect} 효과",
        "turnEnd" => "— 턴 종료 —",
        _ => e.Kind,
    };
}

public sealed record DraftPick(int Round, List<string> Options, int Chosen);

public sealed record QueueCard(string Card, int CooldownLeft);

public sealed record SideFrame(int Hp, int MaxHp, int Cost, int Defense, bool Guard, List<string> Effects,
    List<string> Hand, List<QueueCard> Queue, int Deck);

/// <summary>턴이 끝난 뒤의 양쪽 상태와 그 턴에 일어난 일.</summary>
public sealed record TurnFrame(int Turn, SideFrame A, SideFrame B, List<string> Log);
