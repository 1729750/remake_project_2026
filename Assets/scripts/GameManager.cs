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
    // 임시: 보상 카드 로딩 로직이 생기기 전까지 인스펙터에서 직접 지정
    [SerializeField] private CardDefinition[] rewardCards;
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
        EndBattle();
        //ShowEnemySelection();
    }

    public void StartBattle(CharacterData enemyData)
    {
        SetGameState(GameState.Battle);
        inputManager.Unload();
        inputManager.Load("Battle", new Dictionary<string, Action>
        {
            ["PlayCard1"] = () => PlayCard(0),
            ["PlayCard2"] = () => PlayCard(1),
            ["PlayCard3"] = () => PlayCard(2),
            ["PlayCard4"] = () => PlayCard(3),
            ["ReDraw"]    = () => PlayCard(CharacterManager.RedrawAction),
            ["Defense"]   = () => PlayCard(CharacterManager.DefenseAction),
        });
        battleManager.StartBattle(PlayerManager.Instance.GetCharacterData(), enemyData);
    }

    // Battle 맵의 PlayCard1..4/ReDraw/Defense가 공유하는 가드: 내 턴일 때만 카드를 낼 수 있다.
    private void PlayCard(int index)
    {
        if (battleManager.CurrentState == BattleState.Turn)
            PlayerManager.Instance.GetCharacterManager().SelectCard(index);
    }

    public void EndBattle()
    {
        SetGameState(GameState.BattleEnd);
        inputManager.Unload();
        ShowRewardDisplay();
    }

    // 카드 획득/삭제/강화 중 무엇을 할지 고르는 첫 화면.
    public void ShowRewardDisplay()
    {
        rewardManager.ShowRewardDisplay();
        inputManager.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => rewardManager.MoveRewardDisplaySelection(-1),
            ["Right"]  = () => rewardManager.MoveRewardDisplaySelection(1),
            ["Select"] = rewardManager.ConfirmRewardSelection,
        });
    }

    // RewardDisplay 왼쪽 패널: 기존 카드 획득 로직.
    public void RewardCard()
    {
        inputManager.Unload();
        rewardManager.ClearRewardDisplay();
        inputManager.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => rewardManager.MoveRewardCardSelection(-1),
            ["Right"]  = () => rewardManager.MoveRewardCardSelection(1),
            ["Select"] = ConfirmRewardCard,
        });
        rewardManager.ShowRewardCard(rewardCards);
    }

    public void ConfirmRewardCard()
    {
        CardDefinition selected = rewardManager.ConfirmRewardCard();
        if (selected != null)
            PlayerManager.Instance.AddCard(selected);

        ConfirmReward();
    }

    // RewardDisplay 가운데 패널: 화면 크기 DeckDisplay를 띄워 버릴 카드를 고른다.
    public void RewardCardDelete()
    {
        inputManager.Unload();
        rewardManager.ClearRewardDisplay();

        DeckDisplay deckDisplay = Instantiate(Resources.Load<GameObject>("Prefabs/DeckDisplay")).GetComponent<DeckDisplay>();
        Camera cam = Camera.main;
        float screenHeight = 2f * cam.orthographicSize;
        float screenWidth = screenHeight * cam.aspect;
        deckDisplay.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, deckDisplay.transform.position.z);
        deckDisplay.SetSize(new Vector2(screenWidth, screenHeight));
        deckDisplay.SetDeck(PlayerManager.Instance.GetDeck(), 0.1f);

        inputManager.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => deckDisplay.MoveSelectionHorizontal(-1),
            ["Right"]  = () => deckDisplay.MoveSelectionHorizontal(1),
            ["Up"]     = () => deckDisplay.MoveSelectionVertical(-1),
            ["Down"]   = () => deckDisplay.MoveSelectionVertical(1),
            ["Select"] = () =>
            {
                DiscardCard(deckDisplay.GetSelectedIndex());
                Destroy(deckDisplay.gameObject);
            },
        });
    }

    private void DiscardCard(int index)
    {
        PlayerManager.Instance.DiscardCard(index);
        ConfirmReward();
    }

    // RewardDisplay 오른쪽 패널: 카드 강화. 아직 로직이 정해지지 않아 메뉴만 닫고 돌아간다.
    public void RewardCardEnhance()
    {
        rewardManager.ClearRewardDisplay();
        ConfirmReward();
    }

    // 보상 화면(카드 획득/삭제/강화 중 무엇이든)을 완전히 닫는 공통 지점.
    // reward 쪽에서 마지막으로 남아있던 input context를 여기서 pop한다.
    public void ConfirmReward()
    {
        inputManager.Unload();
        ShowEnemySelection();
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
}

public enum GameState{
    StartScreen,
    Battle,
    BattleEnd,
    SelectEnemy,
}