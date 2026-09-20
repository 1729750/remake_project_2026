using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class GameManager_Multi : NetworkBehaviour
{
    public static GameManager_Multi Instance { get; private set; }

    // =========================================================
    // Multi Game State
    // =========================================================

    private MultiMatchState _currentState =
        MultiMatchState.WaitingForPlayers;

    // =========================================================
    // Network
    // =========================================================

    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    // =========================================================
    // 기존 GameManager Inspector 구조 대응
    // =========================================================

    [Header("Managers")]

    [SerializeField]
    private BattleManager_Multi battleManager;

    /*
     * 아직 각각의 Multi 스크립트를 만들지 않았으므로
     * 임시로 GameObject 참조만 유지한다.
     *
     * 이후:
     *
     * GameObject rewardManager
     *      → RewardManager_Multi
     *
     * GameObject mapManager
     *      → MapManager_Multi
     *
     * GameObject gameEndManager
     *      → GameEndManager_Multi
     *
     * 로 하나씩 교체한다.
     */

    [SerializeField]
    private GameObject rewardManager;

    [SerializeField]
    private GameObject mapManager;

    [SerializeField]
    private GameObject gameEndManager;

    [SerializeField]
    private FadeIn fadeIn;

    [SerializeField]
    private PlayerDeckPanel playerDeckPanel;

    // =========================================================
    // Single의 Enemy Data 자리
    //
    // Single:
    // CharacterData firstEnemyData
    // enemyCandidates
    // bossCandidates
    //
    // Multi:
    // 상대는 AI CharacterData가 아니라 Remote ClientId
    // =========================================================

    public ulong LocalClientId
    {
        get
        {
            if (NetworkManager.Singleton == null)
                return ulong.MaxValue;

            return NetworkManager.Singleton.LocalClientId;
        }
    }

    public ulong OpponentClientId
    {
        get
        {
            if (gameNetworkState == null ||
                !gameNetworkState.PlayersAssigned)
            {
                return ulong.MaxValue;
            }

            ulong localId = LocalClientId;

            if (localId ==
                gameNetworkState.Player0ClientId.Value)
            {
                return gameNetworkState.Player1ClientId.Value;
            }

            if (localId ==
                gameNetworkState.Player1ClientId.Value)
            {
                return gameNetworkState.Player0ClientId.Value;
            }

            return ulong.MaxValue;
        }
    }

    // =========================================================
    // 기존 Effect Cache 그대로 유지
    // =========================================================

    private const string EffectPricesResourcePath =
        "Data/EffectPrices";

    private static Dictionary<EffectType, EffectPriceInfo>
        _effectPriceCache;

    private const string EffectSummariesResourcePath =
        "Data/EffectSummaries";

    private static Dictionary<EffectType, string>
        _effectSummaryCache;

    // =========================================================
    // 내부 상태
    // =========================================================

    private bool _gameStarted;
    private bool _resultHandled;

    public bool IsGameStarted => _gameStarted;

    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildEffectPriceCache();
        BuildEffectSummaryCache();
    }

    // =========================================================
    // Single의 Start() 대응
    //
    // NetworkBehaviour이므로 Multi에서는
    // 실제 게임 시작 초기화를 OnNetworkSpawn에서 한다.
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[GameManager_Multi] " +
                "GameNetworkState가 연결되지 않았습니다."
            );

            return;
        }

        gameNetworkState.StateChanged +=
            HandleNetworkStateChanged;

        _currentState =
            gameNetworkState.MatchState.Value;

        GameStart();

        fadeIn?.Play();

        RefreshFromNetworkState();

        Debug.Log(
            "[GameManager_Multi] Network Spawn\n" +
            $"IsServer: {IsServer}\n" +
            $"LocalClientId: {LocalClientId}"
        );
    }

    public override void OnNetworkDespawn()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged -=
                HandleNetworkStateChanged;
        }
    }

    // =========================================================
    // Single GameManager.StartBattle() 대응
    //
    // Single:
    // CharacterData enemyData를 받아 AI 전투 시작
    //
    // Multi:
    // Player0 / Player1이 이미 NetworkState에 있으므로
    // 별도의 enemyData가 필요 없다.
    // =========================================================

    public void StartBattle()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[GameManager_Multi] " +
                "전투 시작 결정은 Server만 가능합니다."
            );

            return;
        }

        TryStartBattleServer();
    }

    private void TryStartBattleServer()
    {
        if (!IsServer)
            return;

        if (_gameStarted)
            return;

        if (gameNetworkState == null ||
            !gameNetworkState.PlayersAssigned)
        {
            Debug.Log(
                "[GameManager_Multi] " +
                "Player0 / Player1 확정 대기 중"
            );

            return;
        }

        if (battleManager == null)
        {
            Debug.LogError(
                "[GameManager_Multi] " +
                "BattleManager_Multi가 연결되지 않았습니다."
            );

            return;
        }

        _gameStarted = true;

        SetGameStateServer(
            MultiMatchState.BattleStarting
        );

        Debug.Log(
            "[GameManager_Multi] StartBattle\n" +
            $"Player0: {gameNetworkState.Player0ClientId.Value}\n" +
            $"Player1: {gameNetworkState.Player1ClientId.Value}"
        );

        battleManager.InitializeBattleServer();
    }

    // =========================================================
    // Single EndBattle() 대응
    // =========================================================

    public void EndBattle()
    {
        if (!IsServer)
            return;

        SetGameStateServer(
            MultiMatchState.BattleFinished
        );
    }

    // =========================================================
    // Single GameOver() 대응
    //
    // 주의:
    // 승패 '판정'은 여기서 하지 않는다.
    //
    // BattleManager_Multi가 Server에서 WinnerClientId를 정하고,
    // 이 함수는 Local 화면 표현용이다.
    // =========================================================

    public void GameOver()
    {
        SoundManager.Instance?.Play(
            BgmName.GameLoseBGM
        );

        if (gameEndManager != null)
        {
            gameEndManager.SetActive(true);
        }

        Debug.Log(
            "[GameManager_Multi] Local Result = LOSE"
        );
    }

    // =========================================================
    // Single GameWin() 대응
    // =========================================================

    public void GameWin()
    {
        SoundManager.Instance?.Play(
            BgmName.GameWinBGM
        );

        if (gameEndManager != null)
        {
            gameEndManager.SetActive(true);
        }

        Debug.Log(
            "[GameManager_Multi] Local Result = WIN"
        );
    }

    // =========================================================
    // Single GameStart() 대응
    // =========================================================

    public void GameStart()
    {
        _gameStarted = false;
        _resultHandled = false;

        /*
         * BattleManager_Multi는 기존 BattleManager.Init 구조를
         * 그대로 가지고 있으므로 호출한다.
         *
         * 단, BattleManager_Multi가 NetworkSpawn 전이라면
         * Network 관련 초기화 순서 문제가 있을 수 있으므로
         * IsSpawned 확인.
         */

        if (battleManager != null &&
            battleManager.IsSpawned)
        {
            battleManager.Init();
        }

        /*
         * Single:
         *
         * mapManager.Init();
         * PlayerManager.Instance.Init();
         * TransitionManager.Instance?.Init();
         *
         * 이 세 부분은 각 _Multi 버전 코드를 받은 뒤
         * 동일 위치에 다시 연결한다.
         */

        Debug.Log(
            "[GameManager_Multi] GameStart"
        );
    }

    // =========================================================
    // Single AddCard() 대응
    // =========================================================

    public void AddCard(
        CardDefinition card)
    {
        /*
         * PlayerManager_Multi가 아직 없기 때문에
         * 임의로 Single PlayerManager를 사용하지 않는다.
         */

        Debug.LogWarning(
            "[GameManager_Multi] " +
            "AddCard는 PlayerManager_Multi 구현 후 연결합니다."
        );
    }

    // =========================================================
    // Single DiscardCard() 대응
    // =========================================================

    public void DiscardCard(
        int index)
    {
        Debug.LogWarning(
            "[GameManager_Multi] " +
            "DiscardCard는 PlayerManager_Multi 구현 후 연결합니다."
        );
    }

    // =========================================================
    // Single EnhanceCard() 대응
    // =========================================================

    public void EnhanceCard(
        int index,
        CardUpgrade upgrade)
    {
        Debug.LogWarning(
            "[GameManager_Multi] " +
            "EnhanceCard는 PlayerManager_Multi 구현 후 연결합니다."
        );
    }

    // =========================================================
    // Single ShowEnemySelection() 대응
    //
    // 이 부분이 가장 중요한 차이.
    //
    // Single:
    // AI Enemy 후보 3개 생성/선택
    //
    // Multi:
    // Enemy = 상대 ClientId
    //
    // 따라서 Map 선택 / Random Enemy 생성 없음.
    // =========================================================

    public void ShowEnemySelection()
    {
        if (gameNetworkState == null ||
            !gameNetworkState.PlayersAssigned)
        {
            Debug.LogWarning(
                "[GameManager_Multi] " +
                "아직 상대 Client가 확정되지 않았습니다."
            );

            return;
        }

        ulong opponentId =
            OpponentClientId;

        Debug.Log(
            "[GameManager_Multi] " +
            "Multi에서는 Enemy Selection을 사용하지 않습니다.\n" +
            $"LocalClientId: {LocalClientId}\n" +
            $"OpponentClientId: {opponentId}"
        );
    }

    // =========================================================
    // Single의 enemy 생성 함수 대신
    // Network 상대 찾기
    // =========================================================

    public ulong GetOpponentClientId(
        ulong clientId)
    {
        if (gameNetworkState == null ||
            !gameNetworkState.PlayersAssigned)
        {
            return ulong.MaxValue;
        }

        if (clientId ==
            gameNetworkState.Player0ClientId.Value)
        {
            return gameNetworkState.Player1ClientId.Value;
        }

        if (clientId ==
            gameNetworkState.Player1ClientId.Value)
        {
            return gameNetworkState.Player0ClientId.Value;
        }

        return ulong.MaxValue;
    }

    // =========================================================
    // Single GetGameState() 대응
    // =========================================================

    public MultiMatchState GetGameState()
    {
        return _currentState;
    }

    // =========================================================
    // Server에서만 Network State 변경
    // =========================================================

    public void SetGameStateServer(
        MultiMatchState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[GameManager_Multi] " +
                "GameState 변경은 Server만 가능합니다."
            );

            return;
        }

        if (gameNetworkState == null)
            return;

        gameNetworkState.SetMatchStateServer(
            newState
        );
    }

    // =========================================================
    // Network State → Local Game State
    // =========================================================

    private void HandleNetworkStateChanged()
    {
        RefreshFromNetworkState();

        if (IsServer)
        {
            TryStartBattleServer();
        }
    }

    private void RefreshFromNetworkState()
    {
        if (gameNetworkState == null ||
            !gameNetworkState.IsSpawned)
        {
            return;
        }

        MultiMatchState previousState =
            _currentState;

        MultiMatchState newState =
            gameNetworkState.MatchState.Value;

        _currentState = newState;

        if (previousState != newState)
        {
            Debug.Log(
                "[GameManager_Multi] State Changed\n" +
                $"{previousState} -> {newState}"
            );

            LoadAppropriateManager();
        }

        if (newState ==
            MultiMatchState.BattleFinished)
        {
            HandleBattleFinishedLocal();
        }
    }

    // =========================================================
    // Network 승패 → Local Win/Lose 표현
    // =========================================================

    private void HandleBattleFinishedLocal()
    {
        if (_resultHandled)
            return;

        ulong winnerId =
            gameNetworkState.WinnerClientId.Value;

        if (winnerId ==
            GameNetworkState.UnassignedClientId)
        {
            return;
        }

        _resultHandled = true;

        if (winnerId ==
            LocalClientId)
        {
            GameWin();
        }
        else
        {
            GameOver();
        }
    }

    // =========================================================
    // Single LoadAppropriateManager() 대응
    //
    // 주의:
    // BattleManager_Multi가 붙은 NetworkObject 자체를
    // SetActive(false) 하면 안 된다.
    //
    // 그래서 아직 Battle Root는 건드리지 않는다.
    // =========================================================

    private void LoadAppropriateManager()
    {
        bool battleActive =
            _currentState ==
                MultiMatchState.BattleStarting ||
            _currentState ==
                MultiMatchState.Battle;

        bool gameEndActive =
            _currentState ==
            MultiMatchState.BattleFinished;

        /*
         * BattleManager_Multi가 NetworkObject인 경우
         * battleManager.gameObject.SetActive(false)
         * 하지 않는다.
         *
         * 이후 BattleViewRoot를 별도로 받아
         * Visual만 ON/OFF하도록 변경한다.
         */

        if (mapManager != null)
        {
            mapManager.SetActive(false);
        }

        if (rewardManager != null)
        {
            rewardManager.SetActive(false);
        }

        if (gameEndManager != null)
        {
            gameEndManager.SetActive(
                gameEndActive
            );
        }

        Debug.Log(
            "[GameManager_Multi] Local View State\n" +
            $"Battle: {battleActive}\n" +
            $"GameEnd: {gameEndActive}"
        );
    }

    // =========================================================
    // Single SummonDeck() 대응
    // =========================================================

    public static DeckDisplay SummonDeck(
        Func<CardDefinition, bool> filter = null)
    {
        /*
         * Single에서는:
         *
         * PlayerManager.Instance.GetDeck()
         *
         * 을 사용했지만,
         *
         * Multi에서는 비공개 Deck을 상대에게 보내면 안 되므로
         * PlayerManager_Multi / Owner 전용 데이터가 만들어진 뒤
         * 구현한다.
         */

        Debug.LogWarning(
            "[GameManager_Multi] " +
            "SummonDeck은 PlayerManager_Multi 구현 후 연결합니다."
        );

        return null;
    }

    // =========================================================
    // Effect Price / Summary
    //
    // 이 부분은 Network 판정과 상관없는 공용 데이터이므로
    // Single 구조를 그대로 사용한다.
    // =========================================================

    private void BuildEffectPriceCache()
    {
        _effectPriceCache =
            new Dictionary<
                EffectType,
                EffectPriceInfo
            >();

        TextAsset json =
            Resources.Load<TextAsset>(
                EffectPricesResourcePath
            );

        if (json != null)
        {
            EffectPriceTable table =
                JsonUtility.FromJson<
                    EffectPriceTable
                >(json.text);

            if (table?.prices != null)
            {
                foreach (
                    EffectPriceJsonEntry entry
                    in table.prices)
                {
                    if (Enum.TryParse(
                        entry.effectType,
                        true,
                        out EffectType type))
                    {
                        _effectPriceCache[type] =
                            new EffectPriceInfo
                            {
                                price =
                                    entry.price,

                                magnitudeMin =
                                    entry.magnitudeMin,

                                magnitudeMax =
                                    entry.magnitudeMax,

                                magnitudeUnit =
                                    entry.magnitudeUnit
                            };
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[GameManager_Multi] " +
                            "알 수 없는 EffectType: " +
                            entry.effectType
                        );
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning(
                "[GameManager_Multi] " +
                $"Resources/{EffectPricesResourcePath}.json을 찾지 못했습니다."
            );
        }
    }

    [Serializable]
    private class EffectPriceJsonEntry
    {
        public string effectType;
        public float price;

        public int magnitudeMin;
        public int magnitudeMax;
        public int magnitudeUnit;
    }

    [Serializable]
    private class EffectPriceTable
    {
        public List<
            EffectPriceJsonEntry
        > prices;
    }

    private void BuildEffectSummaryCache()
    {
        _effectSummaryCache =
            new Dictionary<
                EffectType,
                string
            >();

        TextAsset json =
            Resources.Load<TextAsset>(
                EffectSummariesResourcePath
            );

        if (json != null)
        {
            EffectSummaryTable table =
                JsonUtility.FromJson<
                    EffectSummaryTable
                >(json.text);

            if (table?.summaries != null)
            {
                foreach (
                    EffectSummaryJsonEntry entry
                    in table.summaries)
                {
                    if (Enum.TryParse(
                        entry.effectType,
                        true,
                        out EffectType type))
                    {
                        _effectSummaryCache[type] =
                            entry.summary;
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[GameManager_Multi] " +
                            "알 수 없는 EffectType: " +
                            entry.effectType
                        );
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning(
                "[GameManager_Multi] " +
                $"Resources/{EffectSummariesResourcePath}.json을 찾지 못했습니다."
            );
        }
    }

    [Serializable]
    private class EffectSummaryJsonEntry
    {
        public string effectType;
        public string summary;
    }

    [Serializable]
    private class EffectSummaryTable
    {
        public List<
            EffectSummaryJsonEntry
        > summaries;
    }

    public static int GetEffectPrice(
        EffectType effectType)
    {
        return Mathf.FloorToInt(
            GetEffectPriceInfo(
                effectType
            ).price
        );
    }

    public static EffectPriceInfo
        GetEffectPriceInfo(
            EffectType effectType)
    {
        if (_effectPriceCache == null)
        {
            Instance?.BuildEffectPriceCache();
        }

        _effectPriceCache.TryGetValue(
            effectType,
            out EffectPriceInfo info
        );

        return info;
    }

    public static string GetEffectSummary(
        EffectType effectType)
    {
        if (_effectSummaryCache == null)
        {
            Instance?.BuildEffectSummaryCache();
        }

        _effectSummaryCache.TryGetValue(
            effectType,
            out string summary
        );

        return summary;
    }
}