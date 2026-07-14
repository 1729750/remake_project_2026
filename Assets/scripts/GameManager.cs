using System;using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private GameState _currentState;

    [SerializeField] private BattleManager battleManager;
    [SerializeField] private InputManager inputManager;

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
        StartBattle();
    }

    public void StartBattle()
    {
        SetGameState(GameState.Battle);
        inputManager.LoadBattleInputActions();
        battleManager.StartBattle();
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