using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 루트에 위치한 게임 오브젝트로 존재하며, GameManager.SetGameState가 GameState.SelectEnemy로
// 바뀔 때만 활성화된다. BattleManager.ShowReward와 같은 패턴으로 CharacterData 3개를 받아
// 전시해두고 플레이어가 좌/우로 고른 뒤 확정하면 GameManager.StartBattle을 호출한다.
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    private CharacterData[] _candidates;
    private int _selectedIndex;

    private int _currentRound;
    private readonly List<CharacterData> _battleHistory = new List<CharacterData>();

    // enemyDisplay 프리팹 인스턴스들(mapManager 하위 어딘가)에 붙은 MapVisual을 게임 시작 시 캐싱해둔다.
    private MapVisual[] _enemyVisuals;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _enemyVisuals = GetComponentsInChildren<MapVisual>(true);
    }

    public void ShowEnemySelection(CharacterData[] candidates)
    {
        if (candidates == null || candidates.Length == 0) return;

        _candidates = candidates;
        _selectedIndex = 0;

        int count = Mathf.Min(_enemyVisuals.Length, candidates.Length);
        for (int i = 0; i < count; i++)
            _enemyVisuals[i].SetCharacter(candidates[i]);

        RefreshSelectionHighlight();
    }

    public void MoveSelection(int delta)
    {
        if (_candidates == null || _candidates.Length == 0) return;

        int count = _candidates.Length;
        _selectedIndex = ((_selectedIndex + delta) % count + count) % count;
        RefreshSelectionHighlight();
    }

    private void RefreshSelectionHighlight()
    {
        for (int i = 0; i < _enemyVisuals.Length; i++)
            _enemyVisuals[i]?.SetSelected(i == _selectedIndex);
    }

    public void ConfirmSelection()
    {
        if (_candidates == null || _candidates.Length == 0) return;

        CharacterData selected = _candidates[_selectedIndex];
        _currentRound++;
        _battleHistory.Add(selected);
        _candidates = null;

        GameManager.Instance.StartBattle(selected);
    }

    public int GetCurrentRound() => _currentRound;
    public IReadOnlyList<CharacterData> GetBattleHistory() => _battleHistory;

    // 이후 후보 전시 UI에서 비주얼 요소로 사용할 파싱 함수들.
    // 현재 CharacterData는 maxHealth와 deck만 갖고 있으므로 그 안에서 뽑아낼 수 있는 값만 제공한다.
    public int GetHealth(CharacterData data)
    {
        return data != null ? data.GetMaxHealth() : 0;
    }

    // deck을 구성하는 카드들의 CardEffect를 모두 훑어 EffectType별 순위를 매긴다.
    // Attack/Defend는 딜/방어 수치라 이펙트 성향 판단에서 의미가 달라 제외하고,
    // 나머지 타입은 (magnitude 총합 x 등장 횟수)를 비중으로 삼아 내림차순으로 나열한다.
    public List<EffectType> GetMostFrequentEffectType(CharacterData data)
    {
        CardCollection deck = data != null ? data.GetDeck() : null;
        if (deck == null) return new List<EffectType>();

        var magnitudeSums = new Dictionary<EffectType, int>();
        var counts = new Dictionary<EffectType, int>();
        foreach (CardDefinition card in deck.GetCards())
        {
            if (card == null) continue;
            foreach (CardEffect cardEffect in card.GetEffects())
            {
                Effect effect = cardEffect?.GetEffect();
                if (effect == null) continue;

                EffectType type = effect.GetEffectType();
                if (type == EffectType.Attack || type == EffectType.Defend) continue;

                magnitudeSums[type] = magnitudeSums.TryGetValue(type, out int sum) ? sum + effect.GetMagnitude() : effect.GetMagnitude();
                counts[type] = counts.TryGetValue(type, out int c) ? c + 1 : 1;
            }
        }

        return counts.Keys
            .OrderByDescending(type => magnitudeSums[type] * counts[type])
            .ToList();
    }

    // Attack/Defend 각각의 (magnitude 총합 x 등장 횟수) 비중. AtkBar/DefBar 표시 비율 계산에 쓰인다.
    public int GetAttackWeight(CharacterData data) => GetEffectWeight(data, EffectType.Attack);
    public int GetDefenseWeight(CharacterData data) => GetEffectWeight(data, EffectType.Defend);

    private int GetEffectWeight(CharacterData data, EffectType targetType)
    {
        CardCollection deck = data != null ? data.GetDeck() : null;
        if (deck == null) return 0;

        int sum = 0;
        int count = 0;
        foreach (CardDefinition card in deck.GetCards())
        {
            if (card == null) continue;
            foreach (CardEffect cardEffect in card.GetEffects())
            {
                Effect effect = cardEffect?.GetEffect();
                if (effect == null || effect.GetEffectType() != targetType) continue;
                sum += effect.GetMagnitude();
                count++;
            }
        }

        return sum * count;
    }
}
