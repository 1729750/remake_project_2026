using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class BattleManager_Multi : NetworkBehaviour
{
    public static BattleManager_Multi Instance { get; private set; }

    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [Header("Battle View")]
    [SerializeField]
    private GameObject battleViewRoot;

    [SerializeField]
    private CharacterManager_Multi playerCharacterManager;

    [SerializeField]
    private CharacterManager_Multi enemyCharacterManager;

    [SerializeField]
    private TextMeshPro turnText;

    [SerializeField]
    private float turnDuration = 1f;

    [SerializeField]
    private float startDelay = 3f;

    [SerializeField]
    private List<EffectEmoji> effectEmojis =
        new List<EffectEmoji>();

    private TurnTimerOverlay _turnTimerOverlay;

    private float _startElapsed;
    private float _turnElapsed;
    private int _currentTurnNumber;

    private static Dictionary<EffectType, Sprite>
        _emojiCache;

    private int _startTickCount;
    private bool _nextTurnEndIsFirst = true;
    private bool _battleStarted;

    public BattleState CurrentState
    {
        get;
        private set;
    }

    public bool BattleStarted =>
        _battleStarted;

    public GameNetworkState NetworkState =>
        gameNetworkState;


    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // BattleManager 자체는 살아있고,
        // 실제 Battle 화면만 처음에는 숨긴다.
        SetBattleViewActive(false);
    }


    public override void OnNetworkSpawn()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged +=
                HandleNetworkStateChanged;
        }

        Init();

        TryBindLocalViews();

        // 현재 네트워크 상태에 맞춰
        // Battle 화면 표시 여부 동기화.
        RefreshBattleViewVisibility();
    }


    public override void OnNetworkDespawn()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged -=
                HandleNetworkStateChanged;
        }
    }


    public void Init()
    {
        playerCharacterManager?.Init();
        enemyCharacterManager?.Init();

        if (_turnTimerOverlay != null)
        {
            Destroy(
                _turnTimerOverlay.gameObject
            );
        }

        if (turnText == null)
            return;

        GameObject overlayGO =
            new GameObject(
                "TurnTimerOverlay_Multi"
            );

        overlayGO.transform.SetParent(
            turnText.transform.parent
        );

        overlayGO.transform.localPosition =
            Vector3.zero;

        overlayGO.transform.localScale =
            Vector3.one;

        overlayGO.AddComponent<MeshFilter>();
        overlayGO.AddComponent<MeshRenderer>();

        _turnTimerOverlay =
            overlayGO.AddComponent<
                TurnTimerOverlay>();
    }


    private void TryBindLocalViews()
    {
        if (gameNetworkState == null ||
            !gameNetworkState.PlayersAssigned ||
            NetworkManager.Singleton == null)
        {
            return;
        }

        ulong localId =
            NetworkManager.Singleton
                .LocalClientId;

        ulong opponentId =
            gameNetworkState
                .GetOpponentClientId(
                    localId
                );

        if (opponentId ==
            GameNetworkState
                .UnassignedClientId)
        {
            return;
        }

        // 각 PC에서
        // Player = 나
        // Enemy = 상대
        playerCharacterManager
            ?.BindClientId(
                localId,
                true
            );

        enemyCharacterManager
            ?.BindClientId(
                opponentId,
                false
            );
    }


    public void InitializeBattleServer()
    {
        if (!IsServer ||
            _battleStarted)
        {
            return;
        }

        if (gameNetworkState == null ||
            !gameNetworkState
                .PlayersAssigned)
        {
            return;
        }

        TryBindLocalViews();

        if (MatchDeckStore.Instance != null)
        {
            if (!TryInitializeFinalDecksServer())
            {
                Debug.LogError(
                    "[BattleManager_Multi] " +
                    "FinalDeck 초기화 실패로 전투 시작을 중단합니다."
                );

                return;
            }
        }
        else
        {
            // MultiPlayMode Scene을 직접 실행한 개발 테스트에서만
            // Inspector Start Deck을 fallback으로 사용한다.
            playerCharacterManager
                ?.InitializeConfiguredCharacterServer();

            enemyCharacterManager
                ?.InitializeConfiguredCharacterServer();
        }

        _battleStarted = true;

        _startElapsed = 0f;
        _turnElapsed = 0f;
        _currentTurnNumber = 0;
        _startTickCount = 0;
        _nextTurnEndIsFirst = true;

        _turnTimerOverlay
            ?.SetFill(0f);

        SetState(
            BattleState.BattleStarting
        );

        gameNetworkState
            .SetCurrentTurnServer(0);

        gameNetworkState
            .SetMatchStateServer(
                MultiMatchState.BattleStarting
            );

        SoundManager.Instance
            ?.StopBgm();

        UpdateStartCountdownText();
    }


    private bool TryInitializeFinalDecksServer()
    {
        MatchDeckStore store =
            MatchDeckStore.Instance;

        if (store == null ||
            playerCharacterManager == null ||
            enemyCharacterManager == null)
        {
            return false;
        }

        ulong playerClientId =
            playerCharacterManager.RepresentedClientId;

        ulong enemyClientId =
            enemyCharacterManager.RepresentedClientId;

        if (!store.TryCreateDeck(
                playerClientId,
                out CardDefinition[] playerDeck))
        {
            Debug.LogError(
                "[BattleManager_Multi] " +
                $"Player FinalDeck을 찾지 못했습니다: {playerClientId}"
            );

            return false;
        }

        if (!store.TryCreateDeck(
                enemyClientId,
                out CardDefinition[] enemyDeck))
        {
            foreach (CardDefinition card in playerDeck)
            {
                if (card != null)
                    Destroy(card);
            }

            Debug.LogError(
                "[BattleManager_Multi] " +
                $"Enemy FinalDeck을 찾지 못했습니다: {enemyClientId}"
            );

            return false;
        }

        playerCharacterManager.CharacterInit(
            playerDeck,
            playerCharacterManager.getmaxHealth()
        );

        enemyCharacterManager.CharacterInit(
            enemyDeck,
            enemyCharacterManager.getmaxHealth()
        );

        Debug.Log(
            "[BattleManager_Multi] " +
            $"FinalDeck 전투 연결 완료 | " +
            $"Player({playerClientId}): {playerDeck.Length} | " +
            $"Enemy({enemyClientId}): {enemyDeck.Length}"
        );

        return true;
    }


    public void SetBattleViewActive(
        bool active)
    {
        // 현재 MultiPlayMode 씬에서는 battleViewRoot가
        // BattleManager 자신을 가리키고 있다. 이 오브젝트를 끄면
        // NetworkObject/NetworkBehaviour까지 함께 비활성화되어
        // 전투가 시작되지 않으므로 루트 자체는 끄지 않는다.
        if (battleViewRoot == gameObject)
        {
            Debug.LogWarning(
                "[BattleManager_Multi] " +
                "battleViewRoot가 BattleManager 자신을 가리킵니다. " +
                "전투 루트 비활성화를 건너뜁니다."
            );

            return;
        }

        if (battleViewRoot != null)
        {
            battleViewRoot.SetActive(active);
        }

        Debug.Log(
            "[BattleManager_Multi] " +
            $"Battle View Active: {active}"
        );
    }


    private void RefreshBattleViewVisibility()
    {
        if (gameNetworkState == null)
        {
            SetBattleViewActive(false);
            return;
        }

        MultiMatchState state =
            gameNetworkState
                .MatchState
                .Value;

        bool shouldShowBattle =
            state ==
                MultiMatchState
                    .BattleStarting ||
            state ==
                MultiMatchState
                    .Battle ||
            state ==
                MultiMatchState
                    .BattleFinished;

        SetBattleViewActive(
            shouldShowBattle
        );
    }


    private void UpdateStartCountdownText()
    {
        if (turnText == null)
            return;

        int remaining =
            Mathf.CeilToInt(
                Mathf.Max(
                    startDelay -
                    _startElapsed,
                    0f
                )
            );

        turnText.text =
            remaining.ToString();
    }


    public static List<CardDefinition>
        UnpackCardCollection(
            CardCollection collection)
    {
        var result =
            new List<CardDefinition>();

        if (collection == null)
        {
            return result;
        }

        foreach (
            CardDefinition def
            in collection.GetCards())
        {
            if (def != null)
            {
                result.Add(
                    Instantiate(def)
                );
            }
        }

        return result;
    }


    public void Tick(
        float deltaTime)
    {
        if (!IsServer ||
            !_battleStarted)
        {
            return;
        }

        if (CurrentState ==
            BattleState.BattleStarting)
        {
            _startElapsed +=
                deltaTime;

            float startRatio =
                startDelay > 0f
                    ? Mathf.Clamp01(
                        _startElapsed /
                        startDelay
                    )
                    : 1f;

            _turnTimerOverlay
                ?.SetFill(
                    startRatio
                );

            int maxTicks =
                Mathf.Max(
                    0,
                    Mathf.FloorToInt(
                        startDelay
                    )
                );

            int desiredTicks =
                Mathf.Min(
                    Mathf.FloorToInt(
                        _startElapsed
                    ) + 1,
                    maxTicks
                );

            while (
                _startTickCount <
                desiredTicks)
            {
                _startTickCount++;

                PlayAlternatingTurnEnd();
            }

            if (_startElapsed >=
                startDelay)
            {
                OnBattleStarted();
            }
            else
            {
                UpdateStartCountdownText();
            }

            return;
        }

        if (CurrentState ==
            BattleState.Turn)
        {
            _turnElapsed +=
                deltaTime;

            float turnRatio =
                turnDuration > 0f
                    ? Mathf.Clamp01(
                        _turnElapsed /
                        turnDuration
                    )
                    : 1f;

            _turnTimerOverlay
                ?.SetFill(
                    turnRatio
                );

            if (_turnElapsed >=
                turnDuration)
            {
                OnTurnEnded();
            }
        }
    }


    private void SetState(
        BattleState state)
    {
        CurrentState =
            state;
    }


    private void OnBattleStarted()
    {
        if (!IsServer ||
            CurrentState !=
                BattleState
                    .BattleStarting)
        {
            return;
        }

        gameNetworkState
            .SetMatchStateServer(
                MultiMatchState.Battle
            );

        SoundManager.Instance
            ?.Play(
                EffectSound
                    .BattleStart
            );

        SoundManager.Instance
            ?.Play(
                BgmName.BattleBGM
            );

        StartNextTurnServer();
    }


    private void StartNextTurnServer()
    {
        if (!IsServer ||
            !_battleStarted ||
            CurrentState ==
                BattleState
                    .BattleFinish)
        {
            return;
        }

        _currentTurnNumber++;
        _turnElapsed = 0f;

        gameNetworkState
            .SetCurrentTurnServer(
                _currentTurnNumber
            );

        if (turnText != null)
        {
            turnText.text =
                _currentTurnNumber
                    .ToString();
        }

        _turnTimerOverlay
            ?.SetFill(0f);

        OnTurnStarted();
    }


    private void OnTurnStarted()
    {
        if (!IsServer)
            return;

        SetState(
            BattleState.TurnStart
        );

        playerCharacterManager
            ?.OnTurnStart();

        if (CurrentState ==
            BattleState.BattleFinish)
        {
            return;
        }

        enemyCharacterManager
            ?.OnTurnStart();

        if (CurrentState ==
            BattleState.BattleFinish)
        {
            return;
        }

        SetState(
            BattleState.Turn
        );
    }


    private void OnTurnEnded()
    {
        if (!IsServer ||
            CurrentState !=
                BattleState.Turn)
        {
            return;
        }

        SetState(
            BattleState.TurnEnd
        );

        PlayAlternatingTurnEnd();

        playerCharacterManager
            ?.OnTurnEnd();

        if (CurrentState ==
            BattleState.BattleFinish)
        {
            return;
        }

        enemyCharacterManager
            ?.OnTurnEnd();

        if (CurrentState ==
            BattleState.BattleFinish)
        {
            return;
        }

        StartNextTurnServer();
    }


    private void PlayAlternatingTurnEnd()
    {
        SoundManager.Instance
            ?.Play(
                _nextTurnEndIsFirst
                    ? EffectSound
                        .TurnEnd1
                    : EffectSound
                        .TurnEnd2
            );

        _nextTurnEndIsFirst =
            !_nextTurnEndIsFirst;
    }


    public void RequestSelectCard(
        int index)
    {
        if (NetworkManager.Singleton ==
            null)
        {
            return;
        }

        if (IsServer)
        {
            ExecuteSelectCardServer(
                NetworkManager
                    .Singleton
                    .LocalClientId,
                index
            );

            return;
        }

        RequestSelectCardServerRpc(
            index
        );
    }


    [ServerRpc(
        RequireOwnership = false)]
    private void RequestSelectCardServerRpc(
        int index,
        ServerRpcParams rpcParams =
            default)
    {
        ExecuteSelectCardServer(
            rpcParams
                .Receive
                .SenderClientId,
            index
        );
    }


    private void ExecuteSelectCardServer(
        ulong senderClientId,
        int index)
    {
        if (!IsServer ||
            CurrentState !=
                BattleState.Turn)
        {
            return;
        }

        CharacterManager_Multi character =
            GetCharacterByClientId(
                senderClientId
            );

        character?.SelectCardServer(
            index
        );
    }


    public void ApplyDamageServer(
        ulong targetClientId,
        int damage)
    {
        if (!IsServer ||
            !_battleStarted ||
            damage <= 0)
        {
            return;
        }

        CharacterManager_Multi target =
            GetCharacterByClientId(
                targetClientId
            );

        target?.Attacked(
            damage
        );
    }


    public void NotifyDefeat(
        CharacterManager_Multi loser)
    {
        if (!IsServer ||
            loser == null ||
            CurrentState ==
                BattleState
                    .BattleFinish)
        {
            return;
        }

        SetState(
            BattleState.BattleFinish
        );

        _nextTurnEndIsFirst =
            true;

        playerCharacterManager
            ?.ClearHandAndQueue();

        enemyCharacterManager
            ?.ClearHandAndQueue();

        SoundManager.Instance
            ?.StopBgm();

        ulong loserClientId =
            loser.RepresentedClientId;

        ulong winnerClientId =
            gameNetworkState
                .GetOpponentClientId(
                    loserClientId
                );

        gameNetworkState
            .SetWinnerServer(
                winnerClientId
            );

        gameNetworkState
            .SetMatchStateServer(
                MultiMatchState
                    .BattleFinished
            );

        _battleStarted = false;
    }


    public CharacterManager_Multi GetOpponent(
        CharacterManager_Multi user)
    {
        return user ==
            playerCharacterManager
                ? enemyCharacterManager
                : playerCharacterManager;
    }


    public CharacterManager_Multi
        GetCharacterByClientId(
            ulong clientId)
    {
        if (playerCharacterManager != null &&
            playerCharacterManager
                .RepresentedClientId ==
            clientId)
        {
            return
                playerCharacterManager;
        }

        if (enemyCharacterManager != null &&
            enemyCharacterManager
                .RepresentedClientId ==
            clientId)
        {
            return
                enemyCharacterManager;
        }

        return null;
    }


    public ulong GetOpponentClientId(
        ulong clientId)
    {
        return gameNetworkState != null
            ? gameNetworkState
                .GetOpponentClientId(
                    clientId
                )
            : GameNetworkState
                .UnassignedClientId;
    }


    public static Sprite GetEmoji(
        EffectType effectType)
    {
        if (_emojiCache == null)
        {
            _emojiCache =
                new Dictionary<
                    EffectType,
                    Sprite>();

            if (Instance == null)
            {
                return null;
            }

            foreach (
                EffectEmoji entry
                in Instance.effectEmojis)
            {
                _emojiCache[
                    entry.effectType
                ] =
                    entry.sprite;
            }
        }

        _emojiCache.TryGetValue(
            effectType,
            out Sprite sprite
        );

        return sprite;
    }


    private void HandleNetworkStateChanged()
    {
        TryBindLocalViews();

        RefreshBattleViewVisibility();

        if (!IsServer &&
            turnText != null &&
            gameNetworkState != null &&
            gameNetworkState
                .MatchState
                .Value ==
            MultiMatchState.Battle)
        {
            turnText.text =
                gameNetworkState
                    .CurrentTurnNumber
                    .Value
                    .ToString();
        }
    }


    private void Update()
    {
        if (!IsServer ||
            !_battleStarted)
        {
            return;
        }

        Tick(
            Time.deltaTime
        );
    }
}