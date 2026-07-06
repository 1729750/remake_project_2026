using UnityEngine;

public class BattleManager
{
    public static BattleManager Instance { get; private set; }

    private readonly CharacterManager _playerCharacterManager;
    private readonly CharacterManager _enemyCharacterManager;
    private readonly TurnManager _turnManager;
    private readonly TurnTimerOverlay _turnTimerOverlay;
    private readonly float _startDelay;
    private float _startElapsed;

    public GameState CurrentState { get; private set; }

    public BattleManager(CharacterManager playerCharacterManager, CharacterManager enemyCharacterManager,
        TurnManager turnManager, TurnTimerOverlay turnTimerOverlay, float startDelay)
    {
        Instance = this;
        _playerCharacterManager = playerCharacterManager;
        _enemyCharacterManager = enemyCharacterManager;
        _turnManager = turnManager;
        _turnTimerOverlay = turnTimerOverlay;
        _startDelay = startDelay;

        _turnManager.OnTurnStarted += OnTurnStarted;
        _turnManager.OnTurnEnded += OnTurnEnded;
    }

    public void StartBattle()
    {
        _playerCharacterManager.Init();
        _enemyCharacterManager.Init();

        _startElapsed = 0f;
        _turnTimerOverlay?.SetFill(0f);
        SetState(GameState.GameStarting);
    }

    public void Tick(float deltaTime)
    {
        if (CurrentState == GameState.GameStarting)
        {
            _startElapsed += deltaTime;
            _turnTimerOverlay?.SetFill(_startElapsed / _startDelay);
            if (_startElapsed >= _startDelay)
            {
                OnGameStarted();
            }
        }
        else if (CurrentState == GameState.Turn)
        {
            _turnManager.Tick(deltaTime);
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
        _playerCharacterManager.OnTurnStart();
        _enemyCharacterManager.OnTurnStart();
        SetState(GameState.Turn);
    }

    private void OnTurnEnded()
    {
        SetState(GameState.TurnEnd);
        Debug.Log($"Turn {_turnManager.GetCurrentTurn()} Ended");
        _playerCharacterManager.OnTurnEnd();
        _enemyCharacterManager.OnTurnEnd();
        if (CurrentState != GameState.GameFinish)
        {
            _turnManager.StartTurn();
        }
    }

    public void NotifyDefeat(CharacterManager loser)
    {
        if (CurrentState != GameState.GameFinish)
        {
            GetOpponent(loser)?.NotifyVictory();
            SetState(GameState.GameFinish);
            Application.Quit();
        }
    }

    public CharacterManager GetOpponent(CharacterManager user)
    {
        return user == _playerCharacterManager ? _enemyCharacterManager : _playerCharacterManager;
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
}
