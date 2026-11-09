using System.Globalization;
using System.Text;

namespace BalanceSim;

/// <summary>결과를 CSV(카드, 효과, AI 상성)와 읽기 쉬운 요약(summary.md)으로 쓴다.</summary>
public static class Report
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // 강하다/약하다 판단 기준: 포함 승률 차이가 이 이상이고 신뢰구간이 0을 걸치지 않을 때
    public const double OutlierPp = 3.0;

    public static void Write(string dir, Stats stats, string title)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "cards.csv"), CardsCsv(stats), Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "effects.csv"), EffectsCsv(stats), Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "matchups.csv"), MatchupsCsv(stats), Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "summary.md"), Summary(stats, title), Encoding.UTF8);
    }

    public static void WriteComparison(string dir, Stats before, Stats after, string patchName)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "compare.csv"), CompareCsv(before, after), Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "compare.md"), CompareSummary(before, after, patchName), Encoding.UTF8);
    }

    // =========================================================
    // CSV
    // =========================================================

    private static string P(double v) => double.IsNaN(v) ? "" : (v * 100).ToString("0.0", Inv);
    private static string N(double v, string format = "0.00") => double.IsNaN(v) ? "" : v.ToString(format, Inv);

    private static string CardsCsv(Stats s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("card,decks_with,winrate_with_pct,winrate_without_pct,delta_pp,ci95_pp,drawn_winrate_pct,play_rate_pct,dead_rate_pct,uses_per_game,damage_per_play,offered,pick_rate_pct,enhanced_decks,enhanced_winrate_pct");
        foreach (CardRow r in s.Cards)
        {
            sb.AppendLine(string.Join(",", r.Card, r.InDeck.Games, P(r.InDeck.Value), P(r.NotInDeck.Value), P(r.Delta), P(r.DeltaMargin),
                P(r.Drawn.Value), P(r.PlayRate), P(r.DeadRate), N(r.UsesPerGame), N(r.DamagePerPlay, "0.0"),
                r.Offered, P(r.PickRate), r.Enhanced.Games, P(r.Enhanced.Value)));
        }
        return sb.ToString();
    }

    private static string EffectsCsv(Stats s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("effect,decks_with,winrate_with_pct,winrate_without_pct,delta_pp,ci95_pp");
        foreach (EffectRow r in s.Effects)
            sb.AppendLine(string.Join(",", r.Effect, r.With.Games, P(r.With.Value), P(r.Without.Value), P(r.Delta), P(r.DeltaMargin)));
        return sb.ToString();
    }

    private static string MatchupsCsv(Stats s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ai_a,ai_b,games,winrate_a_pct,ci95_pp");
        foreach (MatchupRow r in s.Matchups)
            sb.AppendLine(string.Join(",", r.AiA, r.AiB, r.WinA.Games, P(r.WinA.Value), P(r.WinA.Margin)));
        return sb.ToString();
    }

    private static string CompareCsv(Stats before, Stats after)
    {
        var sb = new StringBuilder();
        sb.AppendLine("card,delta_before_pp,delta_after_pp,change_pp,drawn_winrate_before_pct,drawn_winrate_after_pct,play_rate_before_pct,play_rate_after_pct");
        var afterByCard = after.Cards.ToDictionary(r => r.Card);
        foreach (CardRow b in before.Cards)
        {
            CardRow a = afterByCard[b.Card];
            sb.AppendLine(string.Join(",", b.Card, P(b.Delta), P(a.Delta), P(a.Delta - b.Delta),
                P(b.Drawn.Value), P(a.Drawn.Value), P(b.PlayRate), P(a.PlayRate)));
        }
        return sb.ToString();
    }

    // =========================================================
    // 요약 (사람이 읽는 것)
    // =========================================================

    private static bool IsStrong(CardRow r) => r.Delta * 100 >= OutlierPp && r.Delta - r.DeltaMargin > 0;
    private static bool IsWeak(CardRow r) => r.Delta * 100 <= -OutlierPp && r.Delta + r.DeltaMargin < 0;

    public static string Summary(Stats s, string title)
    {
        GameSummary g = s.Summary;
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"- 판 수 {g.Games:N0} · 무승부 {P(g.DrawRate)}% (시간 초과 {P(g.TimeoutRate)}%)");
        sb.AppendLine($"- 판 길이(턴) 중앙값 {g.TurnsMedian:0} · 10% {g.TurnsP10:0} · 90% {g.TurnsP90:0}  (1.5초 턴: 중앙값 {g.TurnsMedian * 1.5:0}초)");
        sb.AppendLine($"- 역전승(이긴 쪽 HP가 30% 이하까지 갔던 판) {P(g.ComebackRate)}% · 이긴 쪽 남은 HP 평균 {g.WinnerHpAverage:0.0}");
        sb.AppendLine($"- 한쪽당 리드로우 {g.RedrawsPerSide:0.00}회 · 가드 {g.GuardsPerSide:0.00}회 · 선택 건너뛰기 {g.SkipsPerSide:0.0}회 · 고른 선택지 중 강화 {P(g.EnhancePickShare)}%");
        sb.AppendLine();

        sb.AppendLine($"카드·효과 지표 표본: {s.CardSample}");
        sb.AppendLine();
        sb.AppendLine($"## 센 카드 (포함 승률 +{OutlierPp}%p 이상, 95% 구간이 0을 안 걸침)");
        AppendCards(sb, s.Cards.Where(IsStrong));
        sb.AppendLine($"## 약한 카드 (포함 승률 −{OutlierPp}%p 이하)");
        AppendCards(sb, s.Cards.Where(IsWeak).OrderBy(r => r.Delta));
        sb.AppendLine("## 손에 들어도 바로 못 내는 카드 (코스트 부족이나 큐가 가득 찬 턴 70% 이상)");
        AppendCards(sb, s.Cards.Where(r => r.DeadRate >= 0.7).OrderByDescending(r => r.DeadRate));

        sb.AppendLine("## 표본 부족 (덱에 들어간 판 300 미만, 판단 보류)");
        var thin = s.Cards.Where(r => !double.IsNaN(r.Delta) || r.InDeck.Games == 0).Where(r => r.InDeck.Games < 300).ToList();
        sb.AppendLine(thin.Count == 0 ? "- 없음" : "- " + string.Join(", ", thin.Select(r => $"{r.Card}({r.InDeck.Games})")));
        sb.AppendLine();
        sb.AppendLine("## AI 상성 (앞 AI의 승률)");
        foreach (MatchupRow m in s.Matchups.Where(m => m.AiA != m.AiB && string.CompareOrdinal(m.AiA, m.AiB) < 0))
            sb.AppendLine($"- {m.AiA} vs {m.AiB}: {P(m.WinA.Value)}% ± {P(m.WinA.Margin)} ({m.WinA.Games:N0}판)");
        sb.AppendLine();
        sb.AppendLine("시작 덱 카드는 모든 덱에 있어서 포함 승률 차이가 비어 있다. 손에 든 판 승률(drawn_winrate)과 강화된 판 승률을 본다.");
        return sb.ToString();
    }

    private static void AppendCards(StringBuilder sb, IEnumerable<CardRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) sb.AppendLine("- 없음");
        foreach (CardRow r in list)
        {
            string inclusion = double.IsNaN(r.Delta)
                ? $"시작 덱 카드 (손에 든 판 승률 {P(r.Drawn.Value)}%)"
                : $"포함 승률 {P(r.InDeck.Value)}% (없을 때 {P(r.NotInDeck.Value)}%, 차이 {P(r.Delta)} ± {P(r.DeltaMargin)}%p)";
            sb.AppendLine($"- {r.Card}: {inclusion} · 사용률 {P(r.PlayRate)}% · 바로 못 내는 비율 {P(r.DeadRate)}% · 발동당 피해 {N(r.DamagePerPlay, "0.0")}");
        }
        sb.AppendLine();
    }

    private static string CompareSummary(Stats before, Stats after, string patchName)
    {
        var sb = new StringBuilder();
        GameSummary b = before.Summary, a = after.Summary;
        sb.AppendLine($"# 패치 비교: {patchName} (같은 시드)");
        sb.AppendLine();
        sb.AppendLine("| 지표 | 패치 전 | 패치 후 |");
        sb.AppendLine("| --- | --- | --- |");
        sb.AppendLine($"| 판 길이 중앙값(턴) | {b.TurnsMedian:0} | {a.TurnsMedian:0} |");
        sb.AppendLine($"| 무승부 | {P(b.DrawRate)}% | {P(a.DrawRate)}% |");
        sb.AppendLine($"| 역전승 | {P(b.ComebackRate)}% | {P(a.ComebackRate)}% |");
        sb.AppendLine($"| 고른 선택지 중 강화 | {P(b.EnhancePickShare)}% | {P(a.EnhancePickShare)}% |");
        sb.AppendLine($"| 센 카드 수 / 약한 카드 수 | {before.Cards.Count(IsStrong)} / {before.Cards.Count(IsWeak)} | {after.Cards.Count(IsStrong)} / {after.Cards.Count(IsWeak)} |");
        sb.AppendLine();
        sb.AppendLine("## 포함 승률 차이가 많이 바뀐 카드");
        var afterByCard = after.Cards.ToDictionary(r => r.Card);
        foreach (CardRow r in before.Cards
                     .Where(r => !double.IsNaN(r.Delta))
                     .OrderByDescending(r => Math.Abs(afterByCard[r.Card].Delta - r.Delta)).Take(8))
        {
            CardRow x = afterByCard[r.Card];
            sb.AppendLine($"- {r.Card}: {P(r.Delta)} → {P(x.Delta)}%p");
        }
        return sb.ToString();
    }
}
