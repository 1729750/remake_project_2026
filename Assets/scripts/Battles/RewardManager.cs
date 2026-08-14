using System;
using System.Collections.Generic;
using System.Linq;
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
    // 카드 획득 후보 풀. RewardCard()가 매번 이 중 3개를 중복 없이 랜덤으로 뽑아 보여준다.
    // GameManager.GenerateRandomEnemy도 GetRewardCards()로 이 풀을 그대로 가져다 쓴다.
    [SerializeField] private CardDefinition[] rewardCards;

    public CardDefinition[] GetRewardCards() => rewardCards;

    private GameObject _rewardDisplayPrefab;
    private RewardDisplay[] _rewardDisplays;
    private int _rewardDisplaySelectedIndex;

    private GameObject _cardPrefab;
    private CardInstance[] _rewardCardInstances;
    private int _rewardCardSelectedIndex;

    // 강화 후보로 등장하면 안 되는 EffectType(실제 카드 효과가 아니라 내부 마킹용).
    private static readonly EffectType[] EnhanceableEffectTypes = ((EffectType[])Enum.GetValues(typeof(EffectType)))
        .Where(type => type != EffectType.Disposable && type != EffectType.Preserve && type!=EffectType.Guard && type!=EffectType.Weak)
        .ToArray();
    // cost:cooldown 분배가 1:2 경향을 띄도록, 예산 1당 이 확률로 cooldown 쪽에 배분한다.
    private const float CooldownAllocationChance = 2f / 3f;

    private CardUpgrade[] _enhanceOptions;
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
        ShowRewardCard(PickRandomDistinct(rewardCards, 3));
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

        CardUpgrade[] options = new CardUpgrade[3];
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

    // 강화 후보 하나 = 카드 effect(Disposable/Preserve 제외) + 그 가격(GameManager.GetEffectPrice)에
    // magnitude를 곱한 예산을 cost/cooldown 감소량으로 랜덤 분배한 CardUpgrade.
    // effectType을 정한 뒤 GameManager로부터 magnitude 범위(min/max/unit)를 받아 그 안에서 magnitude를
    // 고르고, 그 magnitude로 price(예산)를 계산하는 순서를 따른다.
    // GameManager.GenerateRandomEnemy도 이 메서드로 강화 옵션을 뽑으므로 public.
    public static CardUpgrade RollEnhanceOption()
    {
        EffectType effectType = EnhanceableEffectTypes[UnityEngine.Random.Range(0, EnhanceableEffectTypes.Length)];

        EffectPriceInfo priceInfo = GameManager.GetEffectPriceInfo(effectType);
        int magnitude = RollMagnitude(priceInfo);

        Effect effect = Effect.Create(effectType, magnitude);
        EffectTarget target = ResolveEnhanceTarget(effect.TargetPolarity, magnitude);
        CardEffect cardEffect = new CardEffect(effect, target);

        int priceMagnitude = effect.DoesntUseMagnitude ? 1 : magnitude;
        int budget = Mathf.FloorToInt(priceInfo.price * (float)priceMagnitude);
        (int costUnits, int cooldownUnits) = DistributeBudget(budget);

        return new CardUpgrade(cardEffect, costUnits, cooldownUnits);
    }

    // source에서 최대 count개를 중복 없이 랜덤으로 뽑아 반환한다. source가 count보다 작으면 전부 반환한다.
    // 뽑고 난 뒤 source 자체는 건드리지 않는다(같은 카드가 다음 보상 라운드에 다시 나올 수 있다).
    private static T[] PickRandomDistinct<T>(IReadOnlyList<T> source, int count)
    {
        if (source == null || source.Count == 0) return Array.Empty<T>();

        List<T> pool = new List<T>(source);
        int pickCount = Mathf.Min(count, pool.Count);
        T[] result = new T[pickCount];
        for (int i = 0; i < pickCount; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            result[i] = pool[index];
            pool.RemoveAt(index);
        }
        return result;
    }

    // magnitudeMin~magnitudeMax 사이를 magnitudeUnit 간격으로 나눈 값 중 하나를 랜덤으로 고른다.
    // 예: min 10, max 20, unit 2 → 10/12/14/16/18/20 중 하나.
    private static int RollMagnitude(EffectPriceInfo info)
    {
        int unit = Mathf.Max(1, info.magnitudeUnit);
        int min = info.magnitudeMin;
        int max = Mathf.Max(min, info.magnitudeMax);

        int steps = (max - min) / unit + 1;
        return min + unit * UnityEngine.Random.Range(0, steps);
    }

    // budget을 1씩 나눠 각각 베르누이 시행으로 cost/cooldown에 배분한다.
    // CooldownAllocationChance(2/3)로 cooldown 쪽에 더 자주 배분되어 cost:cooldown이 대략 1:2가 된다.
    // budget은 price(음수 가능)와 magnitude(magnitudeMin이 음수면 음수 가능)의 곱이라 음수로 들어올 수
    // 있다. 음수면 for문이 그냥 0번 돌아 아무 일도 없는 것처럼 되어버리므로, isNegative 플래그로 부호만
    // 정규화(양수로 뒤집기)한 뒤 기존 루프를 그대로 재사용한다 — 즉 |budget| 단위로 같은 방향(cost/cooldown
    // 감소)만큼 배분된다.
    private static (int costUnits, int cooldownUnits) DistributeBudget(int budget)
    {
        bool isNegative = budget < 0;
        if (isNegative) budget = -budget;

        int costUnits = 0;
        int cooldownUnits = 0;
        for (int i = 0; i < budget; i++)
        {
            if (UnityEngine.Random.value < CooldownAllocationChance)
                cooldownUnits++;
            else
                costUnits++;
        }
        return (costUnits, cooldownUnits);
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

    // option을 def에 강화로 적용해도 되는지: effect가 3개 미만이면 항상 가능(새 슬롯에 추가),
    // 3개 이상이면 이미 같은 EffectType을 갖고 있어 병합(AddMagnitude)될 때만 가능하다.
    // 단, magnitude를 안 쓰는 효과(Disposable/Preserve/DivideCooldown류)는 이미 가진 카드에 또
    // 얹어봐야 의미가 없으므로 effect 개수와 무관하게 아예 제외한다.
    // ShowEnhanceDeckSelection(DeckDisplay 필터)과 GameManager.GenerateRandomEnemy(무작위 적 생성)가
    // 전부 이 판정을 공유하므로 public.
    public static bool CanEnhance(CardDefinition def, CardUpgrade option)
    {
        EffectType upgradeType = option.effect.GetEffect().GetEffectType();
        bool alreadyHasEffect = def.GetEffects().Any(effect => effect.GetEffect().GetEffectType() == upgradeType);

        if (option.effect.GetEffect().DoesntUseMagnitude && alreadyHasEffect)
            return false;

        if (def.GetEffects().Length < 3)
            return true;

        return alreadyHasEffect;
    }

    // 강화 후보 선택 확정: DeckDisplay를 띄워 강화할 카드를 고르게 한다.
    private void ShowEnhanceDeckSelection()
    {
        CardUpgrade option = ConfirmEnhanceSelection();
        PlayerInputManager.Instance.Unload();

        DeckDisplay deckDisplay = GameManager.SummonDeck(def => CanEnhance(def, option));
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
    private void SelectEnhance(CardUpgrade[] options)
    {
        _enhanceOptions = options;

        string[] labels = new string[options.Length];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = "";

        _enhanceDisplays = CreateRewardDisplayRow(labels);
        if (_enhanceDisplays == null) return;

        for (int i = 0; i < options.Length; i++)
            _enhanceDisplays[i].SetUpgrade(options[i]);

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
    private CardUpgrade ConfirmEnhanceSelection()
    {
        CardUpgrade selected = _enhanceOptions[_enhanceSelectedIndex];

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
    // ClearRewardCard는 원래 ShowRewardCard 시작 시점에만 불려서, "카드 획득"을 confirm한 뒤에는
    // 카드 오브젝트 3장이 계속 RewardPanel 밑에 남아있었다(다음에 다시 "카드 획득"을 고를 때만
    // 지워짐) — delete/enhance로 확정해도 마찬가지였다. 여기서 공통으로 정리해 어떤 경로로
    // confirm하든 남지 않게 한다.
    private void ConfirmReward()
    {
        ClearRewardCard();
        PlayerInputManager.Instance.Unload();
        GameManager.Instance.ShowEnemySelection();
    }
}
