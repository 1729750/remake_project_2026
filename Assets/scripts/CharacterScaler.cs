using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// GameManager가 MapManager에 넘기기 전 CharacterData 사본에 강화를 적용하는 스케일러.
// MonoBehaviour가 아닌 순수 C# 클래스 — GameManager가 필요할 때 인스턴스를 만들어 쓴다.
// 원본 CharacterData(에셋)는 절대 건드리지 않고, Clone()한 사본만 수정해서 반환한다.
public class CharacterScaler
{
    // 카드 자신의 cost 스탯을 게임플레이 중 실제로 바꾸는(늘리거나 줄이는) 이펙트 목록.
    // Rule 1(고cost 카드 강화 억제)의 예외 판정과 Rule 2-1(cost 정렬 방향)의 기준으로 쓰인다.
    // 밸런싱 조정 대상이라 여기 모아둔다.
    private static readonly HashSet<EffectType> CostAffectingEffectTypes = new HashSet<EffectType>
    {
        EffectType.EnergyHeal,
        EffectType.EnergyDrain,
        EffectType.CostToCooldown,
        EffectType.CooldownToCost,
    };

    private const int CostSoftCap = 15;
    private const int RepeatPickPenalty = 2;

    // enemyA, enemyB: 기존 후보 풀에서 뽑힌 CharacterData(디자이너가 미리 만들어둔 것. 없으면 null).
    // randomEnemy: GameManager.GenerateRandomEnemy가 덱만 채워서 넘긴 CharacterData.
    // enemySelectionCount: 지금까지 플레이어가 적을 확정한 횟수. enemyA/B는 *2, randomEnemy는 *3+1로
    // 각각의 강화 횟수 산식에 쓰인다.
    public CharacterData[] Scale(CharacterData enemyA, CharacterData enemyB, CharacterData randomEnemy, int enemySelectionCount)
    {
        CharacterData copyA = enemyA != null ? enemyA.Clone() : null;
        CharacterData copyB = enemyB != null ? enemyB.Clone() : null;
        CharacterData copyRandom = randomEnemy != null ? randomEnemy.Clone() : null;

        if (copyA != null) ApplyWeightedUpgrades(copyA, enemySelectionCount * 2);
        if (copyB != null) ApplyWeightedUpgrades(copyB, enemySelectionCount * 2);
        if (copyRandom != null) ApplyRandomUpgrades(copyRandom, enemySelectionCount * 3 + 1);

        return new[] { copyA, copyB, copyRandom };
    }

    // 기존 GameManager.GenerateRandomEnemy와 동일한 완전 무작위 강화: 매 시도마다 RollEnhanceOption을
    // 굴리고 CanEnhance를 통과하는 카드 중 하나를 균등 랜덤으로 골라 적용한다.
    private static void ApplyRandomUpgrades(CharacterData data, int attempts)
    {
        List<CardDefinition> deck = data.GetDeck().GetCards().ToList();
        for (int i = 0; i < attempts; i++)
        {
            CardUpgrade option = RewardManager.RollEnhanceOption();
            List<CardDefinition> eligible = deck.Where(def => RewardManager.CanEnhance(def, option)).ToList();
            if (eligible.Count == 0) continue;

            CardDefinition target = eligible[Random.Range(0, eligible.Count)];
            target.ApplyUpgrade(option);
        }
    }

    // 가중치 기반 강화: attempts번 시도(randomEnemy와 마찬가지로 enemySelectionCount 기반 — 호출부 참고).
    // Rule 1/2/2-1과 직전에 고른 카드 페널티를 적용해 대상을 고른다.
    private static void ApplyWeightedUpgrades(CharacterData data, int attempts)
    {
        List<CardDefinition> deck = data.GetDeck().GetCards().ToList();
        CardDefinition lastPicked = null;

        for (int i = 0; i < attempts; i++)
        {
            CardUpgrade option = RewardManager.RollEnhanceOption();
            List<CardDefinition> eligible = deck.Where(def => RewardManager.CanEnhance(def, option)).ToList();
            if (eligible.Count == 0) continue;

            EffectType rolledType = option.effect.GetEffect().GetEffectType();
            CardDefinition target = PickWeighted(eligible, rolledType, lastPicked);
            if (target == null) continue; // 가중치 총합 0 → 이번 시도는 건너뛴다(강화를 최대한 미룸)

            target.ApplyUpgrade(option);
            lastPicked = target;
        }
    }

    private static bool HasCostAffectingEffect(CardDefinition def) =>
        def.GetEffects().Any(e => CostAffectingEffectTypes.Contains(e.GetEffect().GetEffectType()));

    private static bool AlreadyHasEffect(CardDefinition def, EffectType type) =>
        def.GetEffects().Any(e => e.GetEffect().GetEffectType() == type);

    // Rule 2 + 2-1: 이미 그 effect를 가진 카드(m장)가 상위 랭크(n..n-m+1)를, 나머지(n-m장)가
    // 하위 랭크(n-m..1)를 가져간다. 각 그룹 내부는 cost-affecting effect 보유 여부에 따라
    // cost 내림차순(있으면)/오름차순(없으면)으로 랭크를 매긴다.
    // Rule 1: cost가 CostSoftCap을 넘는 카드는 cost-affecting effect가 없으면 가중치 0(강화를 최대한
    // 미룸), 있으면 다른 카드와 동일하게 계산한다.
    // 마지막으로 직전에 고른 카드(lastPicked)에는 RepeatPickPenalty만큼 페널티를 준다.
    private static CardDefinition PickWeighted(List<CardDefinition> eligible, EffectType rolledType, CardDefinition lastPicked)
    {
        int n = eligible.Count;

        List<CardDefinition> already = eligible.Where(def => AlreadyHasEffect(def, rolledType)).ToList();
        List<CardDefinition> rest = eligible.Where(def => !AlreadyHasEffect(def, rolledType)).ToList();

        Dictionary<CardDefinition, int> rank = new Dictionary<CardDefinition, int>();
        int cursor = n;
        foreach (CardDefinition def in RankByCost(already))
            rank[def] = cursor--;
        foreach (CardDefinition def in RankByCost(rest))
            rank[def] = cursor--;

        Dictionary<CardDefinition, int> weights = new Dictionary<CardDefinition, int>();
        foreach (CardDefinition def in eligible)
        {
            int weight = rank[def];

            if (def.GetCost() > CostSoftCap && !HasCostAffectingEffect(def))
                weight = 0;

            if (def == lastPicked)
                weight -= RepeatPickPenalty;

            weights[def] = Mathf.Max(0, weight);
        }

        int total = weights.Values.Sum();
        if (total <= 0) return null;

        int roll = Random.Range(1, total + 1); // 1 ~ total 사이의 정수
        foreach (CardDefinition def in eligible)
        {
            roll -= weights[def];
            if (roll <= 0) return def;
        }
        return eligible[eligible.Count - 1]; // 방어용 fallback(정상 경로에서는 도달하지 않음)
    }

    // cost-affecting effect가 있으면 cost 내림차순, 없으면 오름차순으로 정렬한다.
    private static IEnumerable<CardDefinition> RankByCost(List<CardDefinition> cards)
    {
        return cards.OrderBy(def => HasCostAffectingEffect(def) ? -def.GetCost() : def.GetCost());
    }
}
