using System;
using System.Collections.Generic;
using UnityEngine;

// BattleManager 밖으로 분리된 보상 패널. 다른 오브젝트 위에 얹히는 패널이라 BattleManager와
// enable이 상호배타적일 필요는 없고, GameManager.LoadAppropriateManager가 GameState.BattleEnd
// 여부로 독립적으로 켜고 끈다.
//
// 카드 획득/삭제/강화 흐름(Select input context 관리 포함)을 전부 이 클래스가 갖고 있고,
// PlayerManager의 덱을 직접 건드려야 하는 지점(AddCard/DiscardCard/EnhanceCard)과
// ShowEnemySelection/SummonDeck처럼 RewardManager 바깥의 상태/자원을 다루는 지점만
// GameManager.Instance를 통해 호출한다.
//
// RewardDisplay(카드 획득/삭제/강화, 왼쪽부터 순서대로) 프리팹을 3번 instantiate해서
// transform의 직속 자식으로 붙이고 _rewardDisplays로 추적한다. 확정 즉시 3개 다 파괴한다
// (다음 보상 라운드에 다시 새로 instantiate). "카드 획득" 흐름이 확정되면 카드 오브젝트도
// 마찬가지로 transform의 직속 자식으로 생성된다.
public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    private static readonly string[] RewardDisplayLabels = { "카드 획득", "카드 제거", "카드 강화" };

    [SerializeField] private Sprite[] rewardSprites;
    // 임시: 보상 카드 로딩 로직이 생기기 전까지 인스펙터에서 직접 지정
    [SerializeField] private CardDefinition[] rewardCards;

    private GameObject _rewardDisplayPrefab;
    private RewardDisplay[] _rewardDisplays;
    private int _rewardDisplaySelectedIndex;

    private GameObject _cardPrefab;
    private CardInstance[] _rewardCardInstances;
    private int _rewardCardSelectedIndex;

    private CardEffect[] _enhanceOptions;
    private RewardDisplay[] _enhanceDisplays;
    private int _enhanceSelectedIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 카드 획득/삭제/강화 중 무엇을 할지 고르는 첫 화면. RewardDisplay를 3개 instantiate해서
    // transform의 직속 자식으로 붙이고, 그 커서(HighLight)를 나타낼 수 있도록 좌우로 나란히 늘어놓는다.
    public void ShowRewardDisplay()
    {
        _rewardDisplays = CreateRewardDisplayRow(RewardDisplayLabels);
        if (_rewardDisplays == null) return;

        _rewardDisplaySelectedIndex = 0;
        RefreshRewardDisplaySelection();

        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => MoveRewardDisplaySelection(-1),
            ["Right"]  = () => MoveRewardDisplaySelection(1),
            ["Select"] = ConfirmRewardSelection,
        });
    }

    // RewardDisplay 프리팹을 labels.Length개 instantiate해서 transform의 직속 자식으로 좌우로 나란히 늘어놓는다.
    private RewardDisplay[] CreateRewardDisplayRow(string[] labels)
    {
        if (_rewardDisplayPrefab == null)
            _rewardDisplayPrefab = Resources.Load<GameObject>("Prefabs/RewardDisplay");
        if (_rewardDisplayPrefab == null) return null;

        RewardDisplay[] displays = new RewardDisplay[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            displays[i] = Instantiate(_rewardDisplayPrefab, transform).GetComponent<RewardDisplay>();
            displays[i].Init(labels[i],rewardSprites[i]);
        }

        SpriteRenderer card = displays[0].transform.Find("Card")?.GetComponent<SpriteRenderer>();
        float spacing = (card != null ? card.bounds.size.x : 1f) * 2f;
        for (int i = 0; i < displays.Length; i++)
            displays[i].transform.localPosition = new Vector3((i - (labels.Length - 1) / 2f) * spacing, 0f, 0f);

        return displays;
    }

    private void MoveRewardDisplaySelection(int delta)
    {
        if (_rewardDisplays == null || _rewardDisplays.Length == 0) return;

        int count = _rewardDisplays.Length;
        _rewardDisplaySelectedIndex = ((_rewardDisplaySelectedIndex + delta) % count + count) % count;
        RefreshRewardDisplaySelection();
    }

    // 왼쪽부터 순서대로 카드 획득/삭제/강화에 대응한다.
    private void ConfirmRewardSelection()
    {
        switch (_rewardDisplaySelectedIndex)
        {
            case 0: RewardCard(); break;
            case 1: RewardCardDelete(); break;
            case 2: RewardCardEnhance(); break;
        }
    }

    // 확정되면 instantiate해뒀던 RewardDisplay 3개를 전부 파괴한다.
    private void ClearRewardDisplay()
    {
        if (_rewardDisplays != null)
            foreach (RewardDisplay display in _rewardDisplays)
                if (display != null) Destroy(display.gameObject);
        _rewardDisplays = null;
    }

    private void RefreshRewardDisplaySelection()
    {
        for (int i = 0; i < _rewardDisplays.Length; i++)
            _rewardDisplays[i].SetSelected(i == _rewardDisplaySelectedIndex);
    }

    // RewardDisplay 왼쪽 패널: 카드 획득.
    private void RewardCard()
    {
        PlayerInputManager.Instance.Unload();
        ClearRewardDisplay();
        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => MoveRewardCardSelection(-1),
            ["Right"]  = () => MoveRewardCardSelection(1),
            ["Select"] = ApplyRewardCardSelection,
        });
        ShowRewardCard(rewardCards);
    }

    private void ApplyRewardCardSelection()
    {
        CardDefinition selected = ConfirmRewardCard();
        if (selected != null)
            GameManager.Instance.AddCard(selected);

        ConfirmReward();
    }

    // RewardDisplay 가운데 패널: 화면 크기 DeckDisplay를 띄워 버릴 카드를 고른다.
    private void RewardCardDelete()
    {
        PlayerInputManager.Instance.Unload();
        ClearRewardDisplay();

        DeckDisplay deckDisplay = GameManager.SummonDeck();

        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => deckDisplay.MoveSelectionHorizontal(-1),
            ["Right"]  = () => deckDisplay.MoveSelectionHorizontal(1),
            ["Up"]     = () => deckDisplay.MoveSelectionVertical(-1),
            ["Down"]   = () => deckDisplay.MoveSelectionVertical(1),
            ["Select"] = () =>
            {
                GameManager.Instance.DiscardCard(deckDisplay.GetSelectedIndex());
                Destroy(deckDisplay.gameObject);
                ConfirmReward();
            },
        });
    }

    // RewardDisplay 오른쪽 패널: 카드 강화. 강화 후보(EffectType/정수 값/EffectTarget) 3개를
    // 랜덤으로 뽑아 보여주고 고르게 한다.
    private void RewardCardEnhance()
    {
        PlayerInputManager.Instance.Unload();
        ClearRewardDisplay();

        CardEffect[] options = new CardEffect[3];
        for (int i = 0; i < options.Length; i++)
            options[i] = RollEnhanceOption();

        SelectEnhance(options);
        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => MoveEnhanceSelection(-1),
            ["Right"]  = () => MoveEnhanceSelection(1),
            ["Select"] = ShowEnhanceDeckSelection,
        });
    }

    private static CardEffect RollEnhanceOption()
    {
        var effectTypes = (EffectType[])Enum.GetValues(typeof(EffectType));
        EffectType effectType = effectTypes[UnityEngine.Random.Range(0, effectTypes.Length)];
        int magnitude = UnityEngine.Random.Range(1, 4);

        Effect effect = Effect.Create(effectType, magnitude);
        EffectTarget target = ResolveEnhanceTarget(effect.TargetPolarity, magnitude);
        return new CardEffect(effect, target);
    }

    private static EffectTarget ResolveEnhanceTarget(EffectTargetPolarity polarity, int magnitude)
    {
        bool positiveMagnitude = magnitude > 0;
        switch (polarity)
        {
            case EffectTargetPolarity.Positive: return positiveMagnitude ? EffectTarget.User : EffectTarget.Opponent;
            case EffectTargetPolarity.Negative: return positiveMagnitude ? EffectTarget.Opponent : EffectTarget.User;
            default:                            return UnityEngine.Random.value < 0.5f ? EffectTarget.User : EffectTarget.Opponent;
        }
    }

    // 강화 후보 선택 확정: DeckDisplay를 띄워 강화할 카드를 고르게 한다.
    private void ShowEnhanceDeckSelection()
    {
        CardEffect option = ConfirmEnhanceSelection();
        PlayerInputManager.Instance.Unload();

        bool Filter(CardDefinition def)
        {
            if (def.GetEffects().Length < 3)
                return true;
            foreach (CardEffect effect in def.GetEffects())
            {
                if (effect.GetEffect().GetEffectType() == option.GetEffect().GetEffectType()) return true;
            }

            return false;
        }

        
        
        DeckDisplay deckDisplay = GameManager.SummonDeck(Filter);
        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => deckDisplay.MoveSelectionHorizontal(-1),
            ["Right"]  = () => deckDisplay.MoveSelectionHorizontal(1),
            ["Up"]     = () => deckDisplay.MoveSelectionVertical(-1),
            ["Down"]   = () => deckDisplay.MoveSelectionVertical(1),
            ["Select"] = () =>
            {
                GameManager.Instance.EnhanceCard(deckDisplay.GetSelectedIndex(), option);
                Destroy(deckDisplay.gameObject);
                ConfirmReward();
            },
        });
    }

    // RewardDisplay 오른쪽 패널(카드 강화)에서 뽑아둔 강화 후보 CardEffect 3개를 보여준다.
    // RewardText는 비워 두고, 각 후보는 RewardDisplay 정 가운데의 effectDisplay(아이콘+수치)로 표기한다.
    private void SelectEnhance(CardEffect[] options)
    {
        _enhanceOptions = options;

        string[] labels = new string[options.Length];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = "";

        _enhanceDisplays = CreateRewardDisplayRow(labels);
        if (_enhanceDisplays == null) return;

        for (int i = 0; i < options.Length; i++)
            _enhanceDisplays[i].SetEffect(options[i]);

        _enhanceSelectedIndex = 0;
        RefreshEnhanceSelection();
    }

    private void MoveEnhanceSelection(int delta)
    {
        if (_enhanceDisplays == null || _enhanceDisplays.Length == 0) return;

        int count = _enhanceDisplays.Length;
        _enhanceSelectedIndex = ((_enhanceSelectedIndex + delta) % count + count) % count;
        RefreshEnhanceSelection();
    }

    // 강화 후보 선택을 확정하고, 표시해뒀던 RewardDisplay 3개를 정리한다.
    private CardEffect ConfirmEnhanceSelection()
    {
        CardEffect selected = _enhanceOptions[_enhanceSelectedIndex];

        if (_enhanceDisplays != null)
            foreach (RewardDisplay display in _enhanceDisplays)
                if (display != null) Destroy(display.gameObject);
        _enhanceDisplays = null;
        _enhanceOptions = null;

        return selected;
    }

    private void RefreshEnhanceSelection()
    {
        for (int i = 0; i < _enhanceDisplays.Length; i++)
            _enhanceDisplays[i].SetSelected(i == _enhanceSelectedIndex);
    }

    // rewardCards에 맞는 카드 오브젝트를 만들어 transform의 직속 자식으로 그대로 넣는다.
    private void ShowRewardCard(CardDefinition[] cardOptions)
    {
        ClearRewardCard();

        if (_cardPrefab == null)
            _cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (_cardPrefab == null) return;

        _rewardCardInstances = new CardInstance[cardOptions.Length];
        for (int i = 0; i < cardOptions.Length; i++)
        {
            if (cardOptions[i] == null) continue;

            GameObject obj = Instantiate(_cardPrefab, transform);
            var visual = obj.GetComponent<CardVisual>();
            if (visual == null)
                visual = obj.AddComponent<CardVisual>();

            // 아직 소유자가 없는 카드라 owner 없이 표시 전용 CardInstance로 감싼다
            var instance = new CardInstance(cardOptions[i], null);
            instance.SetVisual(visual);
            instance.SetFace(true);
            instance.SetLayer("UI");
            obj.transform.localScale = Vector3.one*4;
            _rewardCardInstances[i] = instance;
        }

        float spacing = (_rewardCardInstances.Length > 0 && _rewardCardInstances[0] != null
            ? _rewardCardInstances[0].GetBackgroundSize().x : 1f) * 2f;
        for (int i = 0; i < _rewardCardInstances.Length; i++)
        {
            if (_rewardCardInstances[i] == null) continue;
            _rewardCardInstances[i].GetVisual().transform.localPosition = new Vector3((i - (cardOptions.Length - 1) / 2f) * spacing, 0f, 0f);
        }

        _rewardCardSelectedIndex = 0;
        RefreshRewardCardSelection();
    }

    private void MoveRewardCardSelection(int delta)
    {
        if (_rewardCardInstances == null || _rewardCardInstances.Length == 0) return;

        int count = _rewardCardInstances.Length;
        _rewardCardSelectedIndex = ((_rewardCardSelectedIndex + delta) % count + count) % count;
        RefreshRewardCardSelection();
    }

    private CardDefinition ConfirmRewardCard()
    {
        if (_rewardCardInstances == null || _rewardCardInstances.Length == 0) return null;

        return _rewardCardInstances[_rewardCardSelectedIndex]?.GetDefinition();
    }

    // ShowRewardCard가 다시 불릴 때(다음 보상 라운드) 이전에 만들어둔 카드들을 정리한다.
    private void ClearRewardCard()
    {
        if (_rewardCardInstances == null) return;
        foreach (CardInstance instance in _rewardCardInstances)
            if (instance != null && instance.GetVisual() != null) Destroy(instance.GetVisual().gameObject);
        _rewardCardInstances = null;
    }

    private void RefreshRewardCardSelection()
    {
        for (int i = 0; i < _rewardCardInstances.Length; i++)
            _rewardCardInstances[i]?.SetSelected(i == _rewardCardSelectedIndex);
    }

    // 보상 화면(카드 획득/삭제/강화 중 무엇이든)을 완전히 닫는 공통 지점.
    // reward 쪽에서 마지막으로 남아있던 input context를 여기서 pop한다.
    private void ConfirmReward()
    {
        PlayerInputManager.Instance.Unload();
        GameManager.Instance.ShowEnemySelection();
    }
}
