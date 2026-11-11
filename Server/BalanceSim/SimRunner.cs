namespace BalanceSim;

/// <summary>여러 판을 병렬로 돌려 Stats로 모은다. 명령줄 모드와 웹 모드가 같이 쓴다.</summary>
public static class SimRunner
{
    /// <summary>판의 절반은 같은 AI끼리(카드 지표용), 절반은 다른 AI끼리(상성용)가 되도록 조합을 배정한다.</summary>
    public static List<(AiProfile a, AiProfile b)> Pairs(IReadOnlyList<AiProfile> ais)
    {
        var cross = ais.SelectMany(x => ais.Where(y => y != x).Select(y => (x, y)));
        var mirror = ais.SelectMany(x => Enumerable.Repeat((x, x), Math.Max(1, ais.Count - 1)));
        return mirror.Concat(cross).ToList();
    }

    /// <summary>판마다 시드 = 기본 시드 + 판 번호 (같은 시드면 같은 결과). progress는 끝난 판마다 불린다.</summary>
    public static Stats Run(CardLibrary library, IReadOnlyList<AiProfile> ais, int games, int seed,
        Action? progress = null, CancellationToken cancel = default)
    {
        var pairs = Pairs(ais);
        var records = new GameRecord[games];

        Parallel.For(0, games, new ParallelOptions { CancellationToken = cancel }, i =>
        {
            var (aiA, aiB) = pairs[i % pairs.Count];
            records[i] = Simulator.Play(library, aiA, aiB, unchecked(seed * 1_000_003 + i));
            progress?.Invoke();
        });

        return Stats.From(records, library);
    }

    public static List<AiProfile> ParseAis(string list) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => AiProfile.All.TryGetValue(name, out var ai) ? ai : throw new ArgumentException($"모르는 AI: {name}"))
            .ToList();
}
