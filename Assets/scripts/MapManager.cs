using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 루트에 위치한 게임 오브젝트로 존재하며, GameManager.SetGameState가 GameState.SelectEnemy로
// 바뀔 때만 활성화된다. BattleManager.ShowReward와 같은 패턴으로 CharacterData 3개를 받아
// 전시해두고 플레이어가 좌/우로 고른 뒤 확정하면 GameManager.StartBattle을 호출한다.
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    // mapManager의 child로 이미 존재하는 DeckDisplay. 선택 인덱스가 바뀔 때마다 그 인덱스의
    // 상대 deck을 여기에 다시 채워 넣는 방식으로 재활용한다(인스턴스를 새로 만들지 않는다).
    [SerializeField] private DeckDisplay opponentDeckDisplay;

    // 전투 승리 트로피들이 나열되는 컨테이너(mapManager의 child로 미리 배치된 Trophies에 붙어 있다).
    [SerializeField] private TrophiesManager trophiesManager;

    // 적 선택 화면 전용 팝업 인스턴스. 후보 카드(MapVisual)와 opponentDeckDisplay에 전부 이
    // 인스턴스를 넘긴다(씬 전역 static Instance 대신).
    [SerializeField] private PopupManager popupManager;

    private CharacterData[] _candidates;
    private int _selectedIndex;

    private int _currentRound;
    private readonly List<CharacterData> _battleHistory = new List<CharacterData>();

    // DeckDisplay.PlayMoveSelectSound와 같은 패턴 — Left/Right로 후보를 옮길 때 MoveSelect1/2를
    // 번갈아 재생하기 위한 토글.
    private bool _moveSelectToggle;

    // enemyDisplay 프리팹 인스턴스들(mapManager 하위 어딘가)에 붙은 MapVisual을 게임 시작 시 캐싱해둔다.
    private MapVisual[] _enemyVisuals;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _enemyVisuals = GetComponentsInChildren<MapVisual>(true);
        foreach (MapVisual visual in _enemyVisuals)
            visual.SetPopupManager(popupManager);
        opponentDeckDisplay?.SetPopupManager(popupManager);
        ResetTrophies();
    }

    // mapManager가 (재)활성화될 때마다 기본 화면으로 되돌린다: trophies를 켜고 deckDisplay는 끈다.
    private void OnEnable()
    {
        SetDeckViewActive(false);
    }

    // opponentDeckDisplay와 trophies는 항상 상호배타적으로 켜진다 — 커서가 deck 쪽에 가 있을 때만
    // deckDisplay가 활성화되고, 그 외에는 trophies가 활성화된다.
    private void SetDeckViewActive(bool deckActive)
    {
        if (opponentDeckDisplay != null)
            opponentDeckDisplay.gameObject.SetActive(deckActive);
        if (trophiesManager != null)
            trophiesManager.gameObject.SetActive(!deckActive);
    }

    public void ShowEnemySelection(CharacterData[] candidates)
    {
        if (candidates == null || candidates.Length == 0) return;

        _candidates = candidates;
        _selectedIndex = 0;

        // candidates가 슬롯 수(_enemyVisuals.Length)보다 적을 수 있다(예: 보스전은 후보 1명만
        // 온다) — 남는 슬롯은 이전 라운드 데이터가 그대로 남지 않도록 비활성화한다.
        int count = Mathf.Min(_enemyVisuals.Length, candidates.Length);
        for (int i = 0; i < _enemyVisuals.Length; i++)
        {
            bool active = i < count;
            _enemyVisuals[i].gameObject.SetActive(active);
            if (active)
                _enemyVisuals[i].SetCharacter(candidates[i]);
        }

        RefreshSelectionHighlight();
    }

    public void MoveSelection(int delta)
    {
        if (_candidates == null || _candidates.Length == 0) return;

        int count = _candidates.Length;
        _selectedIndex = ((_selectedIndex + delta) % count + count) % count;
        RefreshSelectionHighlight();
        PlayMoveSelectSound();
    }

    private void PlayMoveSelectSound()
    {
        SoundManager.Instance?.Play(_moveSelectToggle ? EffectSound.MoveSelect1 : EffectSound.MoveSelect2);
        _moveSelectToggle = !_moveSelectToggle;
    }

    private void RefreshSelectionHighlight()
    {
        for (int i = 0; i < _enemyVisuals.Length; i++)
            _enemyVisuals[i]?.SetSelected(i == _selectedIndex);
    }

    // 현재 선택된 후보의 deck을 opponentDeckDisplay에 다시 채워 넣는다.
    private void RefreshOpponentDeckDisplay()
    {
        if (opponentDeckDisplay == null || _candidates == null || _candidates.Length == 0) return;

        CardCollection deck = _candidates[_selectedIndex]?.GetDeck();
        List<CardDefinition> cards = deck != null ? deck.GetCards().ToList() : new List<CardDefinition>();
        opponentDeckDisplay.SetDeck(cards, null, 0.7f);
    }

    // 적 후보 선택("Select") 컨텍스트 위에 opponentDeckDisplay 탐색용 컨텍스트를 새로 쌓는다.
    // RewardManager.RewardCardDelete와 같은 패턴 — DeckDisplay의 Move* 함수들을 그대로 바인딩한다.
    // Cancel을 누르면 즉시 Unload로 상위(적 후보 선택) 입력으로 돌아간다.
    public void LoadDeckDisplayInput()
    {
        if (opponentDeckDisplay == null) return;

        SetDeckViewActive(true);
        // opponentDeckDisplay를 활성화한 직후에 채운다 — 비활성 상태에서 SetDeck을 호출하면
        // 그 안에서 새로 instantiate되는 카드들의 Awake가 아직 실행되지 않은 채로 SetCardDefinition
        // 등이 곧장 불려서 초기화가 깨진다.
        RefreshOpponentDeckDisplay();

        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => opponentDeckDisplay.MoveSelectionHorizontal(-1),
            ["Right"]  = () => opponentDeckDisplay.MoveSelectionHorizontal(1),
            ["Up"]     = () => opponentDeckDisplay.MoveSelectionVertical(-1),
            ["Down"]   = () => opponentDeckDisplay.MoveSelectionVertical(1),
            ["Cancel"] = () =>
            {
                PlayerInputManager.Instance.Unload();
                opponentDeckDisplay.Deselect();
                SetDeckViewActive(false);
            },
        });
        opponentDeckDisplay.SelectFirst();
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

    // 전투 승리 직후(GameManager.EndBattle) 호출된다. 방금 확정했던(=방금 이긴) 상대의
    // CharacterData(ConfirmSelection이 _battleHistory에 넣어둔 마지막 항목)를 트로피로 추가한다.
    public void AddTrophyForLastBattle()
    {
        if (_battleHistory.Count == 0 || trophiesManager == null) return;
        trophiesManager.AddTrophy(_battleHistory[_battleHistory.Count - 1]);
    }

    // 지금까지(이번 런에서) 승리 트로피로 쌓인 CharacterData들. GameEndManager가 게임오버 화면에
    // 격파한 적 전체를 다시 전시할 때 가져다 쓴다.
    public IReadOnlyList<CharacterData> GetTrophyData()
    {
        return trophiesManager != null ? trophiesManager.GetTrophyData() : Array.Empty<CharacterData>();
    }

    // 트로피 행을 비운다. 매 게임(새 런) 시작 시 리셋되어야 한다.
    public void ResetTrophies()
    {
        trophiesManager?.ResetTrophies();
    }

    // 매 게임(새 런) 시작 시 호출된다: 트로피 행과 이번 런 동안 쌓인 전투 기록(battleHistory/
    // currentRound)을 전부 초기화한다. GameManager.GameStart가 이 함수를 호출한다.
    public void Init()
    {
        ResetTrophies();
        _battleHistory.Clear();
        _currentRound = 0;
    }

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
