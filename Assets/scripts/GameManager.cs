using System;using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private GameState _currentState;

    [SerializeField] private BattleManager battleManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private CharacterData firstEnemyData;
    // 임시: 보상 카드 로딩 로직이 생기기 전까지 인스펙터에서 직접 지정
    [SerializeField] private CardDefinition[] rewardCards;

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
        EndBattle();
        //StartBattle(firstEnemyData);
    }

    public void StartBattle(CharacterData enemyData)
    {
        SetGameState(GameState.Battle);
        inputManager.LoadBattleInputActions();
        battleManager.StartBattle(PlayerManager.Instance.GetCharacterData(), enemyData);
    }

    public void EndBattle()
    {
        SetGameState(GameState.BattleEnd);
        inputManager.LoadSelectInputActions();
        battleManager.ShowReward(rewardCards);
    }

    public void ConfirmReward()
    {
        CardDefinition selected = battleManager.ConfirmReward();
        if (selected != null)
            PlayerManager.Instance.AddCard(selected);
        // TODO: 다음 전투 시작 등 이후 흐름 연결
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
    BattleEnd,
}