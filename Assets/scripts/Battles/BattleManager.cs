using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BattleManager:MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [SerializeField] private CharacterManager playerCharacterManager;
    [SerializeField] private CharacterManager enemyCharacterManager;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private float startDelay = 3f;
    
    private TurnManager _turnManager;
    private TurnTimerOverlay _turnTimerOverlay;
    private float _startDelay;
    private float _startElapsed;
    
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
            _turnTimerOverlay?.SetFill(_startElapsed / _startDelay);
            if (_startElapsed >= _startDelay)
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
            GetOpponent(loser)?.NotifyVictory();
            SetState(BattleState.BattleFinish);
            Application.Quit();
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
