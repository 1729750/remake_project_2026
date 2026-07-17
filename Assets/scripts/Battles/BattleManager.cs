using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BattleManager:MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [SerializeField] private CharacterManager playerCharacterManager;
    [SerializeField] private CharacterManager enemyCharacterManager;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private float startDelay = 3f;
    
    private TurnManager _turnManager;
    private TurnTimerOverlay _turnTimerOverlay;
    private float _startElapsed;
    private GameObject _cardPrefab;
    private CardDefinition[] _rewardCards;
    private CardVisual[] _rewardVisuals;
    private int _rewardSelectedIndex;
    
    public BattleState CurrentState { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Init()
    {
        playerCharacterManager.Init(); 
        enemyCharacterManager.Init();
        var overlayGO = new GameObject("TurnTimerOverlay");
        overlayGO.transform.SetParent(turnText.transform.parent);
        overlayGO.transform.localPosition = Vector3.zero;
        overlayGO.transform.localScale = Vector3.one;
        overlayGO.AddComponent<MeshFilter>();
        overlayGO.AddComponent<MeshRenderer>();
        var turnTimerOverlay = overlayGO.AddComponent<TurnTimerOverlay>();
        _turnManager = new TurnManager(turnDuration, turnTimerOverlay, turnText);
        
        _turnManager.OnTurnStarted += OnTurnStarted;
        _turnManager.OnTurnEnded += OnTurnEnded;
    }

    public void StartBattle(CharacterData playerData, CharacterData enemyData)
    {
        playerCharacterManager.CharacterInit(UnpackCardCollection(playerData.GetDeck()).ToArray(), playerData.GetMaxHealth());
        enemyCharacterManager.CharacterInit(UnpackCardCollection(enemyData.GetDeck()).ToArray(), enemyData.GetMaxHealth());


        _startElapsed = 0f;
        _turnTimerOverlay?.SetFill(0f);
        SetState(BattleState.BattleStarting);
    }

    // CardCollection 에셋을 런타임 덱으로 풀어낸다. 원본 에셋이 오염되지 않도록
    // 각 CardDefinition을 Instantiate로 깊은 복사해서 반환한다.
    public static List<CardDefinition> UnpackCardCollection(CardCollection collection)
    {
        var result = new List<CardDefinition>();
        if (collection == null) return result;
        foreach (var def in collection.GetCards())
            if (def != null)
                result.Add(Instantiate(def));
        return result;
    }

    public void Tick(float deltaTime)
    {
        if (CurrentState == BattleState.BattleStarting)
        {
            _startElapsed += deltaTime;
            _turnTimerOverlay?.SetFill(_startElapsed / startDelay);
            if (_startElapsed >= startDelay)
            {
                OnBattleStarted();
            }
        }
        else if (CurrentState == BattleState.Turn)
        {
            _turnManager.Tick(deltaTime);
        }
    }

    private void SetState(BattleState state)
    {
        CurrentState = state;
    }

    private void OnBattleStarted()
    {
        _turnManager.StartTurn();
    }

    private void OnTurnStarted()
    {
        SetState(BattleState.TurnStart);
        Debug.Log($"Turn {_turnManager.GetCurrentTurn()} Started");
        playerCharacterManager.OnTurnStart();
        enemyCharacterManager.OnTurnStart();
        SetState(BattleState.Turn);
    }

    private void OnTurnEnded()
    {
        SetState(BattleState.TurnEnd);
        Debug.Log($"Turn {_turnManager.GetCurrentTurn()} Ended");
        playerCharacterManager.OnTurnEnd();
        enemyCharacterManager.OnTurnEnd();
        if (CurrentState != BattleState.BattleFinish)
        {
            _turnManager.StartTurn();
        }
    }

    public void NotifyDefeat(CharacterManager loser)
    {
        if (CurrentState != BattleState.BattleFinish)
        {
            SetState(BattleState.BattleFinish);
            if (loser == playerCharacterManager)
            {
                Application.Quit();
            }
            else
            {
                GameManager.Instance.EndBattle();
            }
        }
    }

    public void ShowReward(CardDefinition[] rewardCards)
    {
        if (_cardPrefab == null)
            _cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (rewardPanel == null || _cardPrefab == null) return;

        rewardPanel.SetActive(true);

        int count = Mathf.Min(rewardCards.Length, rewardPanel.transform.childCount);
        _rewardCards = rewardCards;
        _rewardVisuals = new CardVisual[count];
        for (int i = 0; i < count; i++)
        {
            Transform slot = rewardPanel.transform.GetChild(i);
            for (int c = slot.childCount - 1; c >= 0; c--)
                Destroy(slot.GetChild(c).gameObject);

            if (rewardCards[i] == null) continue;

            GameObject obj = Instantiate(_cardPrefab, slot);
            obj.transform.localPosition = Vector3.zero;
            var visual = obj.GetComponent<CardVisual>();
            if (visual == null)
                visual = obj.AddComponent<CardVisual>();
            // 아직 소유자가 없는 카드라 owner 없이 표시 전용 CardInstance로 감싼다
            visual.SetCard(new CardInstance(rewardCards[i], null), true);
            _rewardVisuals[i] = visual;
        }

        _rewardSelectedIndex = 0;
        RefreshRewardSelection();
    }

    public void MoveRewardSelection(int delta)
    {
        if (_rewardVisuals == null || _rewardVisuals.Length == 0) return;

        int count = _rewardVisuals.Length;
        _rewardSelectedIndex = ((_rewardSelectedIndex + delta) % count + count) % count;
        RefreshRewardSelection();
    }

    public CardDefinition ConfirmReward()
    {
        if (_rewardVisuals == null || _rewardVisuals.Length == 0) return null;

        CardDefinition selected = _rewardCards[_rewardSelectedIndex];
        rewardPanel.SetActive(false);
        _rewardCards = null;
        _rewardVisuals = null;        
        return selected;
    }

    private void RefreshRewardSelection()
    {
        for (int i = 0; i < _rewardVisuals.Length; i++)
        {
            if (_rewardVisuals[i] != null)
                _rewardVisuals[i].SetSelected(i == _rewardSelectedIndex);
        }
    }
    
    public CharacterManager GetOpponent(CharacterManager user)
    {
        return user == playerCharacterManager ? enemyCharacterManager : playerCharacterManager;
    }

    public string GetEmoji(EffectType effectType)
    {
        switch (effectType)
        {
            case EffectType.Attack:     return "🗡️";
            case EffectType.Defend:     return "🛡️";
            case EffectType.Harden:     return "⚙️";
            case EffectType.Strength:   return "✊";
            case EffectType.Vulnerable: return "💔";
            case EffectType.Weak:       return "🥀";
            case EffectType.Guard:      return "🧘";
            default:                    return "?";
        }
    }

    public void Update()
    {
        if(GameManager.Instance.GetGameState() == GameState.Battle)
            Tick(Time.deltaTime);
    }
}

public enum BattleState
{
    BattleStarting,
    TurnStart,
    Turn,
    TurnEnd,
    BattleFinish
}
