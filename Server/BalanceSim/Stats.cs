namespace BalanceSim;

/// <summary>승률 하나 (승 1, 무 0.5)와 95% 신뢰구간.</summary>
public readonly record struct Rate(double Score, int Games)
{
    public double Value => Games > 0 ? Score / Games : double.NaN;

    /// <summary>정규 근사 95% 구간의 반폭.</summary>
    public double Margin => Games > 0 ? 1.96 * Math.Sqrt(Value * (1 - Value) / Games) : double.NaN;

    /// <summary>두 승률 차이(this − other)와 그 95% 구간 반폭.</summary>
    public (double diff, double margin) Minus(Rate other)
    {
        double diff = Value - other.Value;
        double margin = 1.96 * Math.Sqrt(Value * (1 - Value) / Math.Max(Games, 1) + other.Value * (1 - other.Value) / Math.Max(other.Games, 1));
        return (diff, margin);
    }
}

public sealed record CardRow(
    string Card,
    Rate InDeck, Rate NotInDeck, double Delta, double DeltaMargin,
    Rate Drawn,
    double PlayRate, double DeadRate, double UsesPerGame, double DamagePerPlay,
    int Offered, double PickRate,
    Rate Enhanced);

public sealed record EffectRow(string Effect, Rate With, Rate Without, double Delta, double DeltaMargin);

public sealed record MatchupRow(string AiA, string AiB, Rate WinA);

public sealed record GameSummary(
    int Games, double DrawRate, double TimeoutRate,
    double TurnsMedian, double TurnsP10, double TurnsP90,
    double ComebackRate, double WinnerHpAverage,
    double RedrawsPerSide, double GuardsPerSide, double SkipsPerSide, double EnhancePickShare);

/// <summary>
/// 판 기록들을 카드별 / 효과별 / AI 상성 / 판 전체 지표로 모은다. 양쪽(A, B)을 각각 한 "덱"으로 센다.
/// 카드·효과 지표는 같은 AI끼리 붙은 판(미러전)에서만 센다: AI마다 실력과 카드 고르는 버릇이 달라서,
/// 섞어 세면 "못하는 AI가 자주 고르는 카드"가 약해 보인다. 무작위 AI 미러전은 다른 AI가 있으면 뺀다.
/// </summary>
public sealed class Stats
{
    public required GameSummary Summary { get; init; }
    public required string CardSample { get; init; }
    public required List<CardRow> Cards { get; init; }
    public required List<EffectRow> Effects { get; init; }
    public required List<MatchupRow> Matchups { get; init; }

    public static Stats From(IReadOnlyList<GameRecord> games, CardLibrary library)
    {
        static List<(SideRecord side, double score, GameRecord game)> SidesOf(IEnumerable<GameRecord> list) =>
            list.SelectMany(g => new[] { (side: g.A, score: g.ScoreA, game: g), (side: g.B, score: 1 - g.ScoreA, game: g) }).ToList();

        var sides = SidesOf(games);
        var mirrors = games.Where(g => g.A.Ai == g.B.Ai).ToList();
        var skilledMirrors = mirrors.Where(g => g.A.Ai != "random").ToList();
        var cardGames = skilledMirrors.Count > 0 ? skilledMirrors : mirrors.Count > 0 ? mirrors : games.ToList();
        var cardSides = SidesOf(cardGames);

        return new Stats
        {
            Summary = Summarize(games, sides),
            CardSample = $"{string.Join(", ", cardGames.Select(g => g.A.Ai).Distinct())} 미러전 {cardGames.Count:N0}판",
            Cards = library.Cards.Select(c => CardStats(c.Name, cardSides)).OrderByDescending(r => r.Delta).ToList(),
            Effects = Enum.GetValues<EffectType>()
                .Select(t => EffectStats(t, cardSides))
                .Where(r => r.With.Games > 0)
                .OrderByDescending(r => r.Delta).ToList(),
            Matchups = games.GroupBy(g => (g.A.Ai, g.B.Ai))
                .Select(grp => new MatchupRow(grp.Key.Item1, grp.Key.Item2, new Rate(grp.Sum(g => g.ScoreA), grp.Count())))
                .OrderBy(r => r.AiA).ThenBy(r => r.AiB).ToList(),
        };
    }

    private static GameSummary Summarize(IReadOnlyList<GameRecord> games, List<(SideRecord side, double score, GameRecord game)> sides)
    {
        var turns = games.Select(g => (double)g.Turns).OrderBy(t => t).ToList();
        double Percentile(double p) => turns.Count == 0 ? 0 : turns[(int)Math.Clamp(Math.Round(p * (turns.Count - 1)), 0, turns.Count - 1)];

        var decided = games.Where(g => g.Outcome is BattleOutcome.FirstWins or BattleOutcome.SecondWins).ToList();
        SideRecord WinnerOf(GameRecord g) => g.Outcome == BattleOutcome.FirstWins ? g.A : g.B;

        int offers = sides.Sum(s => s.side.Offers.Count(o => o.Picked));
        int enhancePicks = sides.Sum(s => s.side.Offers.Count(o => o.Picked && o.Kind == "enhance"));

        return new GameSummary(
            Games: games.Count,
            DrawRate: games.Count(g => g.Outcome == BattleOutcome.Draw) / (double)Math.Max(games.Count, 1),
            TimeoutRate: games.Count(g => g.Timeout) / (double)Math.Max(games.Count, 1),
            TurnsMedian: Percentile(0.5), TurnsP10: Percentile(0.1), TurnsP90: Percentile(0.9),
            ComebackRate: decided.Count == 0 ? 0 : decided.Count(g => WinnerOf(g).MinHpRatio <= 0.3) / (double)decided.Count,
            WinnerHpAverage: decided.Count == 0 ? 0 : decided.Average(g => (double)WinnerOf(g).FinalHp),
            RedrawsPerSide: sides.Average(s => (double)s.side.Redraws),
            GuardsPerSide: sides.Average(s => (double)s.side.Guards),
            SkipsPerSide: sides.Average(s => (double)s.side.Skips),
            EnhancePickShare: offers == 0 ? 0 : enhancePicks / (double)offers);
    }

    private static CardRow CardStats(string card, List<(SideRecord side, double score, GameRecord game)> sides)
    {
        var with = sides.Where(s => s.side.Deck.Any(c => c.Name == card)).ToList();
        var without = sides.Where(s => s.side.Deck.All(c => c.Name != card)).ToList();
        var drawn = with.Where(s => s.side.Cards.TryGetValue(card, out var u) && u.Drawn).ToList();
        var enhanced = with.Where(s => s.side.Deck.Any(c => c.Name == card && c.Level > 0)).ToList();

        Rate inDeck = new(with.Sum(s => s.score), with.Count);
        Rate notInDeck = new(without.Sum(s => s.score), without.Count);
        var (delta, margin) = inDeck.Minus(notInDeck);

        var usages = with.Select(s => s.side.Cards.TryGetValue(card, out var u) ? u : new CardUsage()).ToList();
        int handTurns = usages.Sum(u => u.HandTurns);
        int playable = usages.Sum(u => u.PlayableTurns);
        int uses = usages.Sum(u => u.Uses);
        int plays = usages.Sum(u => u.Plays);
        int damage = usages.Sum(u => u.Damage);

        var offers = sides.SelectMany(s => s.side.Offers).Where(o => o.Card == card && o.Kind == "add").ToList();

        return new CardRow(card, inDeck, notInDeck, delta, margin,
            new Rate(drawn.Sum(s => s.score), drawn.Count),
            PlayRate: handTurns == 0 ? 0 : Math.Min(1, uses / (double)handTurns),
            DeadRate: handTurns == 0 ? 0 : 1 - playable / (double)handTurns,
            UsesPerGame: with.Count == 0 ? 0 : uses / (double)with.Count,
            DamagePerPlay: plays == 0 ? 0 : damage / (double)plays,
            Offered: offers.Count,
            PickRate: offers.Count == 0 ? 0 : offers.Count(o => o.Picked) / (double)offers.Count,
            Enhanced: new Rate(enhanced.Sum(s => s.score), enhanced.Count));
    }

    private static EffectRow EffectStats(EffectType type, List<(SideRecord side, double score, GameRecord game)> sides)
    {
        bool Has(SideRecord s) => s.Deck.Any(c => c.Effects.Any(e => e.Type == type));
        var with = sides.Where(s => Has(s.side)).ToList();
        var without = sides.Where(s => !Has(s.side)).ToList();
        Rate w = new(with.Sum(s => s.score), with.Count);
        Rate wo = new(without.Sum(s => s.score), without.Count);
        var (delta, margin) = w.Minus(wo);
        return new EffectRow(type.ToString(), w, wo, delta, margin);
    }
}
