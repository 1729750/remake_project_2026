using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private BattleManager playerBattleManager;
    [SerializeField] private BattleManager enemyBattleManager;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private float startDelay = 3f;

    private TurnManager _turnManager;
    private TurnTimerOverlay _turnTimerOverlay;
    private float _startElapsed;
    public GameState CurrentState { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        var overlayGO = new GameObject("TurnTimerOverlay");
        overlayGO.transform.SetParent(turnText.transform.parent);
        overlayGO.transform.localPosition = Vector3.zero;
        overlayGO.transform.localScale = Vector3.one;
        overlayGO.AddComponent<MeshFilter>();
        overlayGO.AddComponent<MeshRenderer>();
        _turnTimerOverlay = overlayGO.AddComponent<TurnTimerOverlay>();

        _turnManager = new TurnManager(turnDuration, _turnTimerOverlay, turnText);
        _turnManager.OnTurnStarted += OnTurnStarted;
        _turnManager.OnTurnEnded += OnTurnEnded;
        playerBattleManager.Init();
        enemyBattleManager.Init();
        
        _startElapsed = 0f;
        _turnTimerOverlay?.SetFill(0f);
        SetState(GameState.GameStarting);
    }

    void Update()
    {
        if (CurrentState == GameState.GameStarting)
        {
            _startElapsed += Time.deltaTime;
            _turnTimerOverlay?.SetFill(_startElapsed / startDelay);
            if (_startElapsed >= startDelay)
            {
                OnGameStarted();
            }
        }
        else if (CurrentState == GameState.Turn)
        {
            _turnManager.Tick(Time.deltaTime);
        }
    }

    private void SetState(GameState state)
    {
        CurrentState = state;
        Debug.Log(CurrentState);
    }

    private void OnGameStarted()
    {
       _turnManager.StartTurn();
    }

    private void OnTurnStarted()
    {
        SetState(GameState.TurnStart);
        Debug.Log($"Turn {_turnManager.GetCurrentTurn()} Started");
        playerBattleManager.OnTurnStart();
        enemyBattleManager.OnTurnStart();
        SetState(GameState.Turn);
    }

    private void OnTurnEnded()
    {
        SetState(GameState.TurnEnd);
        Debug.Log($"Turn {_turnManager.GetCurrentTurn()} Ended");
        playerBattleManager.OnTurnEnd();
        enemyBattleManager.OnTurnEnd();
        if (CurrentState != GameState.GameFinish)
        {
            _turnManager.StartTurn();
        }
    }


    public void NotifyDefeat(BattleManager loser)
    {
        if (CurrentState != GameState.GameFinish)
        {
            GetOpponent(loser)?.NotifyVictory();
            SetState(GameState.GameFinish);
            Application.Quit();
        }
    }

    public BattleManager GetOpponent(BattleManager user)
    {
        return user == playerBattleManager ? enemyBattleManager : playerBattleManager;
    }

    public string GetEmoji(EffectType effectType)
    {
        switch (effectType)
        {
            case EffectType.Attack:    return "🗡️";
            case EffectType.Defend:    return "🛡️";
            case EffectType.Harden:    return "⚙️";
            case EffectType.Strength:  return "✊";
            case EffectType.Vulnerable: return "💔";
            case EffectType.Weak:      return "🥀";
            default:                   return "?";
        }
    }
}
