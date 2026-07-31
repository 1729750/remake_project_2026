using UnityEngine;

// BattleManager 밖으로 분리된 보상 패널. 다른 오브젝트 위에 얹히는 패널이라 BattleManager와
// enable이 상호배타적일 필요는 없고, GameManager.LoadAppropriateManager가 GameState.BattleEnd
// 여부로 독립적으로 켜고 끈다.
//
// RewardDisplay(카드 획득/삭제/강화, 왼쪽부터 순서대로) 프리팹을 3번 instantiate해서
// transform의 직속 자식으로 붙이고 _rewardDisplays로 추적한다. 플레이어가 그중 하나를
// 고르면 GameManager의 해당 함수(RewardCard/RewardCardDelete/RewardCardEnhance)가 호출되고,
// 확정 즉시 3개 다 파괴한다(다음 보상 라운드에 다시 새로 instantiate).
// "카드 획득" 흐름이 확정되면 카드 오브젝트도 마찬가지로 transform의 직속 자식으로 생성된다
// (개명 전엔 그냥 Reward*였던 함수들이 지금의 RewardCard* 함수들).
public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    private const int RewardDisplayOptionCount = 3;

    private GameObject _rewardDisplayPrefab;
    private RewardDisplay[] _rewardDisplays;
    private int _rewardDisplaySelectedIndex;

    private GameObject _cardPrefab;
    private CardDefinition[] _rewardCards;
    private CardVisual[] _rewardCardVisuals;
    private int _rewardCardSelectedIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // RewardDisplay를 3개 instantiate해서 transform의 직속 자식으로 붙이고, 그 커서(HighLight)를
    // 나타낼 수 있도록 좌우로 나란히 늘어놓는다. RewardDisplay 컴포넌트를 이때 캐싱해둬서
    // 선택이 바뀔 때마다 Find를 다시 호출하지 않도록 한다.
    public void ShowRewardDisplay()
    {
        if (_rewardDisplayPrefab == null)
            _rewardDisplayPrefab = Resources.Load<GameObject>("Prefabs/RewardDisplay");
        if (_rewardDisplayPrefab == null) return;

        _rewardDisplays = new RewardDisplay[RewardDisplayOptionCount];
        for (int i = 0; i < RewardDisplayOptionCount; i++)
            _rewardDisplays[i] = Instantiate(_rewardDisplayPrefab, transform).GetComponent<RewardDisplay>();

        SpriteRenderer card = _rewardDisplays[0].transform.Find("Card")?.GetComponent<SpriteRenderer>();
        float spacing = (card != null ? card.bounds.size.x : 1f) * 1.2f;
        for (int i = 0; i < RewardDisplayOptionCount; i++)
            _rewardDisplays[i].transform.localPosition = new Vector3((i - (RewardDisplayOptionCount - 1) / 2f) * spacing, 0f, 0f);

        _rewardDisplaySelectedIndex = 0;
        RefreshRewardDisplaySelection();
    }

    public void MoveRewardDisplaySelection(int delta)
    {
        if (_rewardDisplays == null || _rewardDisplays.Length == 0) return;

        int count = _rewardDisplays.Length;
        _rewardDisplaySelectedIndex = ((_rewardDisplaySelectedIndex + delta) % count + count) % count;
        RefreshRewardDisplaySelection();
    }

    // 왼쪽부터 순서대로 카드 획득/삭제/강화에 대응한다.
    public void ConfirmRewardSelection()
    {
        switch (_rewardDisplaySelectedIndex)
        {
            case 0: GameManager.Instance.RewardCard(); break;
            case 1: GameManager.Instance.RewardCardDelete(); break;
            case 2: GameManager.Instance.RewardCardEnhance(); break;
        }
    }

    // 확정되면 instantiate해뒀던 RewardDisplay 3개를 전부 파괴한다.
    public void ClearRewardDisplay()
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

    // rewardCards에 맞는 카드 오브젝트를 만들어 transform의 직속 자식으로 그대로 넣는다.
    public void ShowRewardCard(CardDefinition[] rewardCards)
    {
        ClearRewardCard();

        if (_cardPrefab == null)
            _cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (_cardPrefab == null) return;

        _rewardCards = rewardCards;
        _rewardCardVisuals = new CardVisual[rewardCards.Length];
        for (int i = 0; i < rewardCards.Length; i++)
        {
            if (rewardCards[i] == null) continue;

            GameObject obj = Instantiate(_cardPrefab, transform);
            var visual = obj.GetComponent<CardVisual>();
            if (visual == null)
                visual = obj.AddComponent<CardVisual>();
            // 아직 소유자가 없는 카드라 owner 없이 표시 전용 CardInstance로 감싼다
            visual.SetCard(new CardInstance(rewardCards[i], null), true);
            _rewardCardVisuals[i] = visual;
        }

        float spacing = (_rewardCardVisuals.Length > 0 && _rewardCardVisuals[0] != null
            ? _rewardCardVisuals[0].GetBackgroundSize().x : 1f) * 1.2f;
        for (int i = 0; i < _rewardCardVisuals.Length; i++)
        {
            if (_rewardCardVisuals[i] == null) continue;
            _rewardCardVisuals[i].transform.localPosition = new Vector3((i - (rewardCards.Length - 1) / 2f) * spacing, 0f, 0f);
        }

        _rewardCardSelectedIndex = 0;
        RefreshRewardCardSelection();
    }

    public void MoveRewardCardSelection(int delta)
    {
        if (_rewardCardVisuals == null || _rewardCardVisuals.Length == 0) return;

        int count = _rewardCardVisuals.Length;
        _rewardCardSelectedIndex = ((_rewardCardSelectedIndex + delta) % count + count) % count;
        RefreshRewardCardSelection();
    }

    public CardDefinition ConfirmRewardCard()
    {
        if (_rewardCardVisuals == null || _rewardCardVisuals.Length == 0) return null;

        return _rewardCards[_rewardCardSelectedIndex];
    }

    // ShowRewardCard가 다시 불릴 때(다음 보상 라운드) 이전에 만들어둔 카드들을 정리한다.
    private void ClearRewardCard()
    {
        if (_rewardCardVisuals == null) return;
        foreach (CardVisual visual in _rewardCardVisuals)
            if (visual != null) Destroy(visual.gameObject);
        _rewardCardVisuals = null;
        _rewardCards = null;
    }

    private void RefreshRewardCardSelection()
    {
        for (int i = 0; i < _rewardCardVisuals.Length; i++)
        {
            if (_rewardCardVisuals[i] != null)
                _rewardCardVisuals[i].SetSelected(i == _rewardCardSelectedIndex);
        }
    }
}
