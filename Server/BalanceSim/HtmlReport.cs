using System.Globalization;
using System.Net;
using System.Text;

namespace BalanceSim;

/// <summary>
/// 결과를 브라우저로 볼 수 있는 한 장짜리 report.html (외부 파일/라이브러리 없음, 라이트/다크 모드).
/// 카드 포함 승률 차이와 95% 구간, 바로 못 내는 비율, 효과별 차이, AI 상성, 전체 카드 표(열 클릭 정렬).
/// </summary>
public static class HtmlReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(string dir, Stats s, string title)
    {
        GameSummary g = s.Summary;
        var body = new StringBuilder();

        body.Append($"<h1>{E(title)}</h1>");
        body.Append($"<p class='sub'>{DateTime.Now:yyyy-MM-dd HH:mm} · {g.Games:N0}판 · 카드·효과 지표 표본: {E(s.CardSample)}</p>");

        body.Append("<div class='tiles'>");
        Tile(body, "판 길이 중앙값", $"{g.TurnsMedian:0}턴", $"약 {g.TurnsMedian * 1.5:0}초 · 10~90%: {g.TurnsP10:0}~{g.TurnsP90:0}턴");
        Tile(body, "무승부", Pct(g.DrawRate), $"시간 초과 {Pct(g.TimeoutRate)}");
        Tile(body, "역전승", Pct(g.ComebackRate), "이긴 쪽 HP가 30% 이하까지 갔던 판");
        Tile(body, "이긴 쪽 남은 HP", $"{g.WinnerHpAverage:0.0}", "평균 (최대 100)");
        Tile(body, "고른 선택지 중 강화", Pct(g.EnhancePickShare), $"한쪽당 건너뛰기 {g.SkipsPerSide:0.0}회");
        body.Append("</div>");

        var measured = s.Cards.Where(r => !double.IsNaN(r.Delta)).OrderByDescending(r => r.Delta).ToList();
        int strong = measured.Count(IsStrong), weak = measured.Count(IsWeak);
        body.Append("<section><h2>카드별 포함 승률 차이</h2>");
        body.Append($"<p>그 카드가 덱에 있을 때 승률 − 없을 때 승률. 막대 끝 선은 95% 구간이고, ±{Report.OutlierPp}%p를 넘으면서 구간이 0을 안 걸치는 카드를 색으로 표시했습니다. 센 카드 {strong}장, 약한 카드 {weak}장.</p>");
        body.Append(DivergingChart(measured.Select(r => (r.Card, r.Delta * 100, r.DeltaMargin * 100, Tone(r))).ToList(), "%p"));
        var starters = s.Cards.Where(r => double.IsNaN(r.Delta)).Select(r => r.Card).ToList();
        if (starters.Count > 0)
            body.Append($"<p class='note'>시작 덱 카드({E(string.Join(", ", starters))})는 모든 덱에 있어 이 차트에서 빠집니다. 아래 표의 사용률과 바로 못 내는 비율을 보세요.</p>");
        body.Append("</section>");

        var dead = s.Cards.Where(r => r.DeadRate >= 0.5).OrderByDescending(r => r.DeadRate).ToList();
        body.Append("<section><h2>손에 들어도 바로 못 내는 카드</h2>");
        body.Append("<p>손에 있던 턴 중 코스트가 모자라거나 큐가 가득 차서 낼 수 없던 비율 (50% 이상만). 최대 코스트 15, 턴당 회복 2.</p>");
        body.Append(BarChart(dead.Select(r => (r.Card, r.DeadRate * 100, r.DeadRate >= 0.9)).ToList(), "%", 100));
        body.Append("</section>");

        body.Append("<section><h2>효과별 포함 승률 차이</h2>");
        body.Append("<p>그 효과를 가진 카드가 덱에 한 장이라도 있을 때와 없을 때의 승률 차이. 모든 덱에 있는 효과(공격, 방어, 힘)는 빠집니다.</p>");
        var effects = s.Effects.Where(r => !double.IsNaN(r.Delta)).ToList();
        body.Append(DivergingChart(effects.Select(r => (r.Effect, r.Delta * 100, r.DeltaMargin * 100,
            r.Delta * 100 >= Report.OutlierPp && r.Delta - r.DeltaMargin > 0 ? "strong" : r.Delta * 100 <= -Report.OutlierPp && r.Delta + r.DeltaMargin < 0 ? "weak" : "")).ToList(), "%p"));
        body.Append("</section>");

        body.Append("<section><h2>AI 상성</h2><table><thead><tr><th>AI</th><th>상대</th><th class='n'>판 수</th><th class='n'>승률</th><th class='n'>95% 구간</th></tr></thead><tbody>");
        foreach (MatchupRow m in s.Matchups.Where(m => m.AiA != m.AiB && string.CompareOrdinal(m.AiA, m.AiB) < 0))
            body.Append($"<tr><td>{E(m.AiA)}</td><td>{E(m.AiB)}</td><td class='n'>{m.WinA.Games:N0}</td><td class='n'>{Pct(m.WinA.Value)}</td><td class='n'>± {N(m.WinA.Margin * 100, "0.0")}%p</td></tr>");
        body.Append("</tbody></table><p class='note'>random = 무작위(기준선), default = 싱글 DefaultEnemyAI, greedy = 같은 점수를 코스트로 나눈 효율.</p></section>");

        body.Append("<section><h2>전체 카드</h2><p>열 이름을 누르면 정렬됩니다.</p>");
        body.Append(CardTable(s.Cards));
        body.Append("</section>");

        File.WriteAllText(Path.Combine(dir, "report.html"), Page(title, body.ToString()), Encoding.UTF8);
    }

    public static void WriteComparison(string dir, Stats before, Stats after, string patchName)
    {
        GameSummary b = before.Summary, a = after.Summary;
        var body = new StringBuilder();
        body.Append($"<h1>패치 비교: {E(patchName)}</h1>");
        body.Append($"<p class='sub'>{DateTime.Now:yyyy-MM-dd HH:mm} · 같은 시드로 패치 전/후 각 {b.Games:N0}판 · 표본: {E(after.CardSample)}</p>");

        body.Append("<section><h2>판 전체</h2><table><thead><tr><th>지표</th><th class='n'>패치 전</th><th class='n'>패치 후</th></tr></thead><tbody>");
        Row(body, "판 길이 중앙값", $"{b.TurnsMedian:0}턴", $"{a.TurnsMedian:0}턴");
        Row(body, "무승부", Pct(b.DrawRate), Pct(a.DrawRate));
        Row(body, "역전승", Pct(b.ComebackRate), Pct(a.ComebackRate));
        Row(body, "고른 선택지 중 강화", Pct(b.EnhancePickShare), Pct(a.EnhancePickShare));
        Row(body, "센 카드 / 약한 카드", $"{before.Cards.Count(IsStrong)} / {before.Cards.Count(IsWeak)}", $"{after.Cards.Count(IsStrong)} / {after.Cards.Count(IsWeak)}");
        body.Append("</tbody></table></section>");

        var afterByCard = after.Cards.ToDictionary(r => r.Card);
        var rows = before.Cards.Where(r => !double.IsNaN(r.Delta))
            .Select(r => (card: r.Card, from: r.Delta * 100, to: afterByCard[r.Card].Delta * 100))
            .OrderByDescending(x => x.to).ToList();
        body.Append("<section><h2>카드별 포함 승률 차이: 패치 전 → 후</h2>");
        body.Append("<p>회색 점이 패치 전, 파란 점이 패치 후. 가운데 0에 가까울수록 균형에 가깝습니다.</p>");
        body.Append(ArrowChart(rows));
        body.Append("</section>");

        body.Append("<p class='note'>패치 전/후 각각의 자세한 결과는 before/report.html, after/report.html.</p>");
        File.WriteAllText(Path.Combine(dir, "compare.html"), Page($"패치 비교: {patchName}", body.ToString()), Encoding.UTF8);
    }

    // =========================================================
    // 차트 (SVG)
    // =========================================================

    private const int Width = 860, LabelWidth = 150, RowHeight = 22;

    // 0을 가운데 둔 가로 막대 + 95% 구간 선
    private static string DivergingChart(List<(string label, double value, double margin, string tone)> rows, string unit)
    {
        if (rows.Count == 0) return "<p class='note'>데이터 없음</p>";
        double max = Math.Max(Report.OutlierPp * 2, rows.Max(r => Math.Abs(r.value) + (double.IsNaN(r.margin) ? 0 : r.margin)));
        max = Math.Ceiling(max / 10) * 10; // 반 눈금도 정수가 되게
        int plot = Width - LabelWidth - 60, top = 24, height = top + rows.Count * RowHeight + 10;
        double X(double v) => LabelWidth + plot / 2.0 + v / max * plot / 2.0;

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox='0 0 {Width} {height}' role='img'>");
        foreach (double t in new[] { -max, -max / 2, 0, max / 2, max })
        {
            sb.Append($"<text class='axis' x='{F(X(t))}' y='14' text-anchor='middle'>{(t > 0 ? "+" : "")}{t:0}{unit}</text>");
            sb.Append($"<line class='{(t == 0 ? "zero" : "grid")}' x1='{F(X(t))}' x2='{F(X(t))}' y1='{top - 4}' y2='{height - 6}'/>");
        }
        foreach (double t in new[] { -Report.OutlierPp, Report.OutlierPp })
            sb.Append($"<line class='ref' x1='{F(X(t))}' x2='{F(X(t))}' y1='{top - 4}' y2='{height - 6}'/>");

        for (int i = 0; i < rows.Count; i++)
        {
            var (label, value, margin, tone) = rows[i];
            double y = top + i * RowHeight, mid = y + RowHeight / 2.0;
            double x0 = X(Math.Min(0, value)), x1 = X(Math.Max(0, value));
            sb.Append($"<text class='label' x='{LabelWidth - 8}' y='{F(mid + 4)}' text-anchor='end'>{E(label)}</text>");
            sb.Append($"<rect class='bar {tone}' x='{F(x0)}' y='{F(y + 4)}' width='{F(Math.Max(1, x1 - x0))}' height='{RowHeight - 8}'><title>{E(label)}: {value:+0.0;-0.0;0.0}{unit} ± {margin:0.0}</title></rect>");
            if (!double.IsNaN(margin))
                sb.Append($"<line class='ci' x1='{F(X(value - margin))}' x2='{F(X(value + margin))}' y1='{F(mid)}' y2='{F(mid)}'/>");
            double tx = value >= 0 ? X(value + (double.IsNaN(margin) ? 0 : margin)) + 6 : X(value - (double.IsNaN(margin) ? 0 : margin)) - 6;
            sb.Append($"<text class='value' x='{F(tx)}' y='{F(mid + 4)}' text-anchor='{(value >= 0 ? "start" : "end")}'>{value:+0.0;-0.0;0.0}</text>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    // 0부터 시작하는 가로 막대 (비율)
    private static string BarChart(List<(string label, double value, bool highlight)> rows, string unit, double max)
    {
        if (rows.Count == 0) return "<p class='note'>해당 카드 없음</p>";
        int plot = Width - LabelWidth - 70, top = 8, height = top + rows.Count * RowHeight + 6;
        var sb = new StringBuilder();
        sb.Append($"<svg viewBox='0 0 {Width} {height}' role='img'>");
        for (int i = 0; i < rows.Count; i++)
        {
            var (label, value, highlight) = rows[i];
            double y = top + i * RowHeight, w = value / max * plot;
            sb.Append($"<text class='label' x='{LabelWidth - 8}' y='{F(y + RowHeight / 2.0 + 4)}' text-anchor='end'>{E(label)}</text>");
            sb.Append($"<rect class='bar {(highlight ? "weak" : "")}' x='{LabelWidth}' y='{F(y + 4)}' width='{F(Math.Max(1, w))}' height='{RowHeight - 8}'><title>{E(label)}: {value:0.0}{unit}</title></rect>");
            sb.Append($"<text class='value' x='{F(LabelWidth + w + 6)}' y='{F(y + RowHeight / 2.0 + 4)}'>{value:0.0}{unit}</text>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    // 패치 전(회색 점) → 후(파란 점)
    private static string ArrowChart(List<(string card, double from, double to)> rows)
    {
        if (rows.Count == 0) return "<p class='note'>데이터 없음</p>";
        double max = Math.Ceiling(Math.Max(Report.OutlierPp * 2, rows.Max(r => Math.Max(Math.Abs(r.from), Math.Abs(r.to)))) / 10) * 10;
        int plot = Width - LabelWidth - 60, top = 24, height = top + rows.Count * RowHeight + 10;
        double X(double v) => LabelWidth + plot / 2.0 + v / max * plot / 2.0;
        var sb = new StringBuilder();
        sb.Append($"<svg viewBox='0 0 {Width} {height}' role='img'>");
        foreach (double t in new[] { -max, -max / 2, 0, max / 2, max })
        {
            sb.Append($"<text class='axis' x='{F(X(t))}' y='14' text-anchor='middle'>{(t > 0 ? "+" : "")}{t:0}%p</text>");
            sb.Append($"<line class='{(t == 0 ? "zero" : "grid")}' x1='{F(X(t))}' x2='{F(X(t))}' y1='{top - 4}' y2='{height - 6}'/>");
        }
        for (int i = 0; i < rows.Count; i++)
        {
            var (card, from, to) = rows[i];
            double mid = top + i * RowHeight + RowHeight / 2.0;
            sb.Append($"<text class='label' x='{LabelWidth - 8}' y='{F(mid + 4)}' text-anchor='end'>{E(card)}</text>");
            sb.Append($"<line class='move' x1='{F(X(from))}' x2='{F(X(to))}' y1='{F(mid)}' y2='{F(mid)}'/>");
            sb.Append($"<circle class='before' cx='{F(X(from))}' cy='{F(mid)}' r='4'><title>{E(card)} 패치 전 {from:+0.0;-0.0;0.0}%p</title></circle>");
            sb.Append($"<circle class='after' cx='{F(X(to))}' cy='{F(mid)}' r='5'><title>{E(card)} 패치 후 {to:+0.0;-0.0;0.0}%p</title></circle>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    // =========================================================
    // 표, 페이지
    // =========================================================

    private static string CardTable(List<CardRow> cards)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='scroll'><table class='sortable'><thead><tr>");
        foreach (string h in new[] { "카드", "덱에 든 판", "포함 승률", "없을 때", "차이(%p)", "95% 구간", "손에 든 판 승률", "사용률", "바로 못 냄", "판당 사용", "발동당 피해", "선택률", "강화된 판", "강화 판 승률" })
            sb.Append($"<th>{h}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (CardRow r in cards)
        {
            sb.Append($"<tr class='{Tone(r)}'><td>{E(r.Card)}</td>");
            Num(sb, r.InDeck.Games, $"{r.InDeck.Games:N0}");
            Num(sb, r.InDeck.Value * 100, Pct(r.InDeck.Value));
            Num(sb, r.NotInDeck.Value * 100, Pct(r.NotInDeck.Value));
            Num(sb, r.Delta * 100, double.IsNaN(r.Delta) ? "시작 덱" : (r.Delta * 100).ToString("+0.0;-0.0;0.0", Inv));
            Num(sb, r.DeltaMargin * 100, double.IsNaN(r.DeltaMargin) ? "" : "± " + N(r.DeltaMargin * 100, "0.0"));
            Num(sb, r.Drawn.Value * 100, Pct(r.Drawn.Value));
            Num(sb, r.PlayRate * 100, Pct(r.PlayRate));
            Num(sb, r.DeadRate * 100, Pct(r.DeadRate));
            Num(sb, r.UsesPerGame, N(r.UsesPerGame, "0.00"));
            Num(sb, r.DamagePerPlay, N(r.DamagePerPlay, "0.0"));
            Num(sb, r.PickRate * 100, r.Offered == 0 ? "" : Pct(r.PickRate));
            Num(sb, r.Enhanced.Games, $"{r.Enhanced.Games:N0}");
            Num(sb, r.Enhanced.Value * 100, Pct(r.Enhanced.Value));
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    private static string Page(string title, string body) => $$"""
<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{E(title)}}</title>
<style>
:root { --bg:#fff; --fg:#1d1d1f; --muted:#6b6b70; --line:#e3e3e6; --grid:#ececef; --bar:#b8b8bd; --accent:#2b6fd6; --weak:#c8553d; --tile:#f6f6f8; }
@media (prefers-color-scheme: dark) { :root { --bg:#17171a; --fg:#ececef; --muted:#a0a0a8; --line:#33333a; --grid:#2a2a30; --bar:#5c5c64; --accent:#5b97ff; --weak:#e07a62; --tile:#212126; } }
* { box-sizing:border-box; }
body { margin:0; padding:24px 16px 48px; background:var(--bg); color:var(--fg); font:14px/1.5 -apple-system, "Segoe UI", "Malgun Gothic", sans-serif; }
main { max-width:900px; margin:0 auto; }
h1 { font-size:22px; margin:0 0 4px; } h2 { font-size:17px; margin:32px 0 6px; }
.sub, .note { color:var(--muted); } .note { font-size:13px; }
.tiles { display:grid; grid-template-columns:repeat(auto-fit, minmax(160px, 1fr)); gap:10px; margin:18px 0; }
.tile { background:var(--tile); border-radius:10px; padding:12px 14px; }
.tile .k { color:var(--muted); font-size:12px; } .tile .v { font-size:22px; font-weight:600; } .tile .d { color:var(--muted); font-size:12px; }
svg { width:100%; height:auto; display:block; margin:8px 0; font-size:12px; }
svg .label { fill:var(--fg); } svg .axis, svg .value { fill:var(--muted); }
svg .grid { stroke:var(--grid); } svg .zero { stroke:var(--muted); } svg .ref { stroke:var(--muted); stroke-dasharray:3 3; opacity:.6; }
svg .bar { fill:var(--bar); } svg .bar.strong { fill:var(--accent); } svg .bar.weak { fill:var(--weak); }
svg .ci { stroke:var(--fg); stroke-width:1.2; opacity:.55; }
svg .move { stroke:var(--bar); stroke-width:2; } svg .before { fill:var(--bar); } svg .after { fill:var(--accent); }
table { border-collapse:collapse; width:100%; font-size:13px; }
th, td { padding:6px 8px; border-bottom:1px solid var(--line); text-align:left; white-space:nowrap; }
th { color:var(--muted); font-weight:500; } td.n, th.n { text-align:right; font-variant-numeric:tabular-nums; }
.sortable th { cursor:pointer; user-select:none; } .sortable th:hover { color:var(--fg); }
tr.strong td:first-child { border-left:3px solid var(--accent); } tr.weak td:first-child { border-left:3px solid var(--weak); }
.scroll { overflow-x:auto; }
</style>
</head>
<body><main>
{{body}}
</main>
<script>
document.querySelectorAll('table.sortable').forEach(table => {
  table.querySelectorAll('th').forEach((th, col) => th.addEventListener('click', () => {
    const body = table.tBodies[0], rows = [...body.rows];
    const desc = th.dataset.dir !== 'desc'; th.dataset.dir = desc ? 'desc' : 'asc';
    const key = r => { const c = r.cells[col]; return c.dataset.v !== undefined ? parseFloat(c.dataset.v) : c.textContent; };
    rows.sort((a, b) => { const x = key(a), y = key(b);
      if (typeof x === 'number' && typeof y === 'number') { const nx = isNaN(x) ? -1e9 : x, ny = isNaN(y) ? -1e9 : y; return desc ? ny - nx : nx - ny; }
      return desc ? String(y).localeCompare(String(x)) : String(x).localeCompare(String(y)); });
    rows.forEach(r => body.appendChild(r));
  }));
});
</script>
</body>
</html>
""";

    // =========================================================
    // 작은 도우미
    // =========================================================

    private static bool IsStrong(CardRow r) => r.Delta * 100 >= Report.OutlierPp && r.Delta - r.DeltaMargin > 0;
    private static bool IsWeak(CardRow r) => r.Delta * 100 <= -Report.OutlierPp && r.Delta + r.DeltaMargin < 0;
    private static string Tone(CardRow r) => IsStrong(r) ? "strong" : IsWeak(r) ? "weak" : "";

    private static void Tile(StringBuilder sb, string key, string value, string detail) =>
        sb.Append($"<div class='tile'><div class='k'>{E(key)}</div><div class='v'>{E(value)}</div><div class='d'>{E(detail)}</div></div>");

    private static void Row(StringBuilder sb, string name, string before, string after) =>
        sb.Append($"<tr><td>{E(name)}</td><td class='n'>{E(before)}</td><td class='n'>{E(after)}</td></tr>");

    private static void Num(StringBuilder sb, double sortValue, string text) =>
        sb.Append($"<td class='n' data-v='{(double.IsNaN(sortValue) ? "NaN" : F(sortValue))}'>{E(text)}</td>");

    private static string Pct(double v) => double.IsNaN(v) ? "" : (v * 100).ToString("0.0", Inv) + "%";
    private static string N(double v, string format) => double.IsNaN(v) ? "" : v.ToString(format, Inv);
    private static string F(double v) => v.ToString("0.##", Inv);
    private static string E(string s) => WebUtility.HtmlEncode(s);
}
