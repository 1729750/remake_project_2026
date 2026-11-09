// 강화 비용 통계: 서버의 실제 강화 규칙(Game/Enhance.cs)으로 시작 덱을 k번 강화했을 때
// 쓸 수 없는 카드(코스트 > 최대 코스트 15)가 생기는 비율과, 효과별 1회 강화의 평균 비용을 출력한다.
// 사용법: dotnet run --project Server/tools/EnhanceStats [-- 판수]
using CardGameServer.Game;

int runs = args.Length > 0 ? int.Parse(args[0]) : 5000;
const int Rounds = 10;

string cardsPath = Path.Combine(AppContext.BaseDirectory, "Data", "cards.json");
CardLibrary library = CardLibrary.Load(cardsPath);
var random = new Random(20261109);

// ---------------------------------------------------------
// 1) 강화를 k번 골랐을 때
// ---------------------------------------------------------
var anyUnusable = new int[Rounds + 1];
var maxCostSum = new double[Rounds + 1];
var maxCooldownSum = new double[Rounds + 1];
var unusableCards = new int[Rounds + 1];
var enhancedCards = new int[Rounds + 1];

for (int run = 0; run < runs; run++)
{
    var deck = library.StartDeck.ToList();
    for (int k = 1; k <= Rounds; k++)
    {
        var rolled = Enhance.Roll(deck, library, random);
        if (rolled != null)
        {
            var (target, upgrade) = rolled.Value;
            deck[target] = Enhance.Apply(deck[target], upgrade);
        }

        if (deck.Any(c => c.Cost > Combatant.MaxCost)) anyUnusable[k]++;
        maxCostSum[k] += deck.Max(c => c.Cost);
        maxCooldownSum[k] += deck.Max(c => c.Cooldown);
        unusableCards[k] += deck.Count(c => c.Cost > Combatant.MaxCost);
        enhancedCards[k] += deck.Count(c => c.Level > 0);
    }
}

Console.WriteLine($"# 강화를 k번 골랐을 때 (시작 덱 {library.StartDeck.Count}장, {runs}판)");
Console.WriteLine("k,쓸수없는카드있는판%,강화된카드중쓸수없는%,덱최대코스트평균,덱최대쿨다운평균");
for (int k = 1; k <= Rounds; k++)
{
    Console.WriteLine(string.Join(",", k,
        (100.0 * anyUnusable[k] / runs).ToString("0.0"),
        (100.0 * unusableCards[k] / Math.Max(1, enhancedCards[k])).ToString("0.0"),
        (maxCostSum[k] / runs).ToString("0.0"),
        (maxCooldownSum[k] / runs).ToString("0.0")));
}

// ---------------------------------------------------------
// 2) 효과별 강화 1회의 평균 비용 (가격표 기준 기댓값)
// ---------------------------------------------------------
Console.WriteLine();
Console.WriteLine("# 효과별 강화 1회 평균 비용 (기댓값)");
Console.WriteLine("효과,방식,가격,수치범위,평균예산,평균코스트+,평균쿨다운+");
foreach (EffectType type in Enhance.EnhanceableTypes)
{
    EffectPrice price = library.PriceOf(type);
    bool noMagnitude = type is EffectType.DivideCooldown or EffectType.DefenseToCooldown;
    int unit = Math.Max(1, price.MagnitudeUnit);
    var magnitudes = Enumerable.Range(0, Math.Max(0, (price.MagnitudeMax - price.MagnitudeMin) / unit) + 1)
        .Select(i => price.MagnitudeMin + i * unit).ToList();

    EffectCategory supported = Effect.SupportedCategoriesOf(type);
    foreach (EffectCategory category in new[] { EffectCategory.Instant, EffectCategory.Continuous })
    {
        if ((supported & category) == 0) continue;
        double budget = magnitudes.Average(m => Math.Floor(price.Price * (noMagnitude ? 1 : m)) * (category == EffectCategory.Continuous ? 5 : 1));
        Console.WriteLine(string.Join(",", type, category == EffectCategory.Continuous ? "지속" : "즉발", price.Price,
            $"{price.MagnitudeMin}~{price.MagnitudeMax}", budget.ToString("0.0"),
            (budget / 3).ToString("0.0"), (budget * 2 / 3).ToString("0.0")));
    }
}
