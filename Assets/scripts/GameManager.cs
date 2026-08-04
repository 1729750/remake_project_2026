using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private GameState _currentState;

    [SerializeField] private BattleManager battleManager;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private MapManager mapManager;
    [SerializeField] private CharacterData firstEnemyData;
    // 임시: 적 후보 산출 로직이 생기기 전까지 인스펙터에서 직접 지정 (3개)
    [SerializeField] private CharacterData[] enemyCandidates;

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
        //ShowEnemySelection();
    }

    public void StartBattle(CharacterData enemyData)
    {
        SetGameState(GameState.Battle);
        inputManager.Unload();
        battleManager.StartBattle(PlayerManager.Instance.GetCharacterData(), enemyData);
    }


    public void EndBattle()
    {
        SetGameState(GameState.BattleEnd);
        inputManager.Unload();
        rewardManager.ShowRewardDisplay();
    }

    // RewardManager가 카드 획득을 확정할 때 부르는, PlayerManager 덱을 직접 건드리는 지점.
    public void AddCard(CardDefinition card)
    {
        PlayerManager.Instance.AddCard(card);
    }

    // RewardManager가 카드 삭제를 확정할 때 부르는, PlayerManager 덱을 직접 건드리는 지점.
    public void DiscardCard(int index)
    {
        PlayerManager.Instance.DiscardCard(index);
    }

    // RewardManager가 카드 강화를 확정할 때 부르는, PlayerManager 덱을 직접 건드리는 지점.
    public void EnhanceCard(int index, CardEffect option)
    {
        PlayerManager.Instance.GetDeck()[index].UpgradeEffect(option);
    }

    public void ShowEnemySelection()
    {
        SetGameState(GameState.SelectEnemy);
        inputManager.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => mapManager.MoveSelection(-1),
            ["Right"]  = () => mapManager.MoveSelection(1),
            ["Select"] = mapManager.ConfirmSelection,
        });
        mapManager.ShowEnemySelection(enemyCandidates);
    }

    public GameState GetGameState()
    {
        return _currentState;
    }

    public void SetGameState(GameState newState)
    {
        _currentState = newState;
        LoadAppropriateManager();
    }

    // manager들의 enable/disable은 오직 이 지점을 통해서만 이뤄진다.
    // 현재 gameState를 담당하는 manager만 enable하고 나머지는 전부 disable한다.
    private void LoadAppropriateManager()
    {

        bool battleActive = false;
        bool mapActive = false;
        switch (_currentState)
        {
            case GameState.StartScreen:
                break;
            case GameState.BattleEnd:
                battleActive = true;
                break;
            case  GameState.Battle:
                battleActive = true;
                break;
            case GameState.SelectEnemy:
                mapActive = true;
                break;
            default:
                break;
        }

        battleManager.gameObject.SetActive(battleActive);
        mapManager.gameObject.SetActive(mapActive);

        // rewardPanel은 다른 오브젝트 위에 얹히는 패널이라 battleActive/mapActive와 상호배타적이지 않다.
        rewardManager.gameObject.SetActive(_currentState == GameState.BattleEnd);
    }

    static public DeckDisplay SummonDeck()
    {
        DeckDisplay deckDisplay = Instantiate(Resources.Load<GameObject>("Prefabs/DeckDisplay")).GetComponent<DeckDisplay>();
        Camera cam = Camera.main;
        float screenHeight = 2f * cam.orthographicSize;
        float screenWidth = screenHeight * cam.aspect;
        deckDisplay.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, deckDisplay.transform.position.z);
        deckDisplay.SetSize(new Vector2(screenWidth/3*2, screenHeight/3*2));
        deckDisplay.SetDeck(PlayerManager.Instance.GetDeck());
        return deckDisplay;
    }
}

public enum GameState{
    StartScreen,
    Battle,
    BattleEnd,
    SelectEnemy,
}