using System;using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private GameState _currentState;

    [SerializeField] private BattleManager battleManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private CharacterData firstEnemyData;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _currentState = GameState.StartScreen;
        battleManager.Init();
        StartBattle(firstEnemyData);
    }

    public void StartBattle(CharacterData enemyData)
    {
        SetGameState(GameState.Battle);
        inputManager.LoadBattleInputActions();
        battleManager.StartBattle(PlayerManager.Instance.GetCharacterData(), enemyData);
    }

    public GameState GetGameState()
    {
        return _currentState;
    }

    public void SetGameState(GameState newState)
    {
        _currentState = newState;
    }
}

public enum GameState{
    StartScreen,
    Battle,
}