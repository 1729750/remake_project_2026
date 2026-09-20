using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class BattleManager_Multi : NetworkBehaviour
{
    public static BattleManager_Multi Instance { get; private set; }

    // =========================================================
    // Network
    // =========================================================

    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    // =========================================================
    // 기존 BattleManager와 동일한 Inspector 구조
    // =========================================================

    [Header("Battle View")]

    [SerializeField]
    private CharacterManager playerCharacterManager;

    [SerializeField]
    private CharacterManager enemyCharacterManager;

    [SerializeField]
    private TextMeshPro turnText;

    [SerializeField]
    private float turnDuration = 1f;

    [SerializeField]
    private float startDelay = 3f;

    [SerializeField]
    private List<EffectEmoji> effectEmojis =
        new List<EffectEmoji>();

    // =========================================================
    // 기존 BattleManager와 동일한 Runtime 필드
    // =========================================================

    private TurnManager _turnManager;
    private TurnTimerOverlay _turnTimerOverlay;

    private float _startElapsed;

    private static Dictionary<EffectType, Sprite> _emojiCache;

    private int _startTickCount;

    private bool _nextTurnEndIsFirst = true;

    // Multi 전용
    private bool _battleStarted;

    // =========================================================
    // State
    // =========================================================

    public BattleState CurrentState { get; private set; }

    public bool BattleStarted => _battleStarted;

    public GameNetworkState NetworkState => gameNetworkState;

    // Local 화면의 Player / Enemy ClientId
    public ulong LocalPlayerClientId
    {
        get
        {
            if (NetworkManager.Singleton == null)
                return ulong.MaxValue;

            return NetworkManager.Singleton.LocalClientId;
        }
    }

    public ulong EnemyClientId
    {
        get
        {
            if (gameNetworkState == null ||
                !gameNetworkState.PlayersAssigned)
            {
                return ulong.MaxValue;
            }

            ulong localId = LocalPlayerClientId;

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
    }

    // =========================================================
    // Network Spawn
    // =========================================================

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            "[BattleManager_Multi] OnNetworkSpawn\n" +
            $"IsServer: {IsServer}\n" +
            $"IsClient: {IsClient}\n" +
            $"LocalClientId: {NetworkManager.Singleton.LocalClientId}"
        );
    }

    // =========================================================
    // Single의 Init()와 같은 위치
    // =========================================================

    public void Init()
    {
        /*
         * 중요:
         *
         * Single에서는 여기서
         *
         * playerCharacterManager.Init();
         * enemyCharacterManager.Init();
         *
         * 을 실행했지만,
         *
         * CharacterManager 내부가 아직
         * Single 판정 + View가 섞여있는지 확인하지 않았기 때문에
         * Multi에서는 지금 당장 호출하지 않는다.
         *
         * CharacterManager_Multi를 만들 때 다시 연결한다.
         */

        if (_turnTimerOverlay != null)
        {
            Destroy(_turnTimerOverlay.gameObject);
        }

        if (turnText != null)
        {
            var overlayGO =
                new GameObject("TurnTimerOverlay_Multi");

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
                overlayGO.AddComponent<TurnTimerOverlay>();

            /*
             * TurnManager는 실제 판정을 Server가 담당한다.
             *
             * 따라서 Server에서만 생성한다.
             */
            if (IsServer)
            {
                _turnManager =
                    new TurnManager(
                        turnDuration,
                        _turnTimerOverlay,
                        turnText
                    );

                _turnManager.OnTurnStarted +=
                    OnTurnStarted;

                _turnManager.OnTurnEnded +=
                    OnTurnEnded;
            }
        }

        Debug.Log(
            "[BattleManager_Multi] Init 완료"
        );
    }

    // =========================================================
    // 기존 GameManager_Multi가 호출하는 진입점
    // =========================================================

    public void InitializeBattleServer()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[BattleManager_Multi] " +
                "Server만 Battle을 시작할 수 있습니다."
            );

            return;
        }

        StartBattleServer();
    }

    // =========================================================
    // Single의 StartBattle() 위치
    // =========================================================

    private void StartBattleServer()
    {
        if (!IsServer)
            return;

        if (_battleStarted)
            return;

        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[BattleManager_Multi] " +
                "GameNetworkState가 없습니다."
            );

            return;
        }

        if (!gameNetworkState.PlayersAssigned)
        {
            Debug.LogWarning(
                "[BattleManager_Multi] " +
                "Player0 / Player1 확정 전입니다."
            );

            return;
        }

        _battleStarted = true;

        if (_turnManager == null)
        {
            Init();
        }

        _turnManager?.Reset();

        _startElapsed = 0f;
        _startTickCount = 0;

        _turnTimerOverlay?.SetFill(0f);

        SetState(
            BattleState.BattleStarting
        );

        gameNetworkState.SetMatchStateServer(
            MultiMatchState.BattleStarting
        );

        Debug.Log(
            "[BattleManager_Multi] StartBattle\n" +
            $"Player0: {gameNetworkState.Player0ClientId.Value}\n" +
            $"Player1: {gameNetworkState.Player1ClientId.Value}\n" +
            $"Player0 HP: {gameNetworkState.Player0Health.Value}\n" +
            $"Player1 HP: {gameNetworkState.Player1Health.Value}"
        );
    }

    // =========================================================
    // Single의 UpdateStartCountdownText()
    // =========================================================

    private void UpdateStartCountdownText()
    {
        if (turnText == null)
            return;

        int remaining =
            Mathf.CeilToInt(
                Mathf.Max(
                    startDelay - _startElapsed,
                    0f
                )
            );

        turnText.text =
            $"{remaining}";
    }

    // =========================================================
    // Single의 UnpackCardCollection()
    // =========================================================

    public static List<CardDefinition>
        UnpackCardCollection(
            CardCollection collection)
    {
        var result =
            new List<CardDefinition>();

        if (collection == null)
            return result;

        foreach (var def in collection.GetCards())
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

    // =========================================================
    // Single의 Tick()
    //
    // 차이:
    // 실제 Turn 진행은 Server만 한다.
    // =========================================================

    public void Tick(float deltaTime)
    {
        if (!IsServer)
            return;

        if (!_battleStarted)
            return;

        if (CurrentState ==
            BattleState.BattleStarting)
        {
            _startElapsed += deltaTime;

            _turnTimerOverlay?.SetFill(
                Mathf.Clamp01(
                    _startElapsed /
                    startDelay
                )
            );

            int desiredTicks =
                Mathf.Min(
                    Mathf.FloorToInt(
                        _startElapsed
                    ) + 1,
                    Mathf.FloorToInt(
                        startDelay
                    )
                );

            while (_startTickCount <
                   desiredTicks)
            {
                _startTickCount++;

                PlayAlternatingTurnEnd();
            }

            if (_startElapsed >= startDelay)
            {
                OnBattleStarted();
            }
            else
            {
                UpdateStartCountdownText();
            }
        }
        else if (
            CurrentState ==
            BattleState.Turn)
        {
            _turnManager?.Tick(
                deltaTime
            );
        }
    }

    // =========================================================
    // Single의 SetState()
    // =========================================================

    private void SetState(
        BattleState state)
    {
        CurrentState = state;
    }

    // =========================================================
    // Single의 OnBattleStarted()
    // =========================================================

    private void OnBattleStarted()
    {
        if (!IsServer)
            return;

        gameNetworkState.SetMatchStateServer(
            MultiMatchState.Battle
        );

        SoundManager.Instance?.Play(
            EffectSound.BattleStart
        );

        SoundManager.Instance?.Play(
            BgmName.BattleBGM
        );

        _turnManager?.StartTurn();

        Debug.Log(
            "[BattleManager_Multi] Battle Started"
        );
    }

    // =========================================================
    // Single의 OnTurnStarted()
    // =========================================================

    private void OnTurnStarted()
    {
        if (!IsServer)
            return;

        SetState(
            BattleState.TurnStart
        );

        Debug.Log(
            $"[BattleManager_Multi] " +
            $"Turn {_turnManager.GetCurrentTurn()} Started"
        );

        /*
         * Single:
         *
         * playerCharacterManager.OnTurnStart();
         * enemyCharacterManager.OnTurnStart();
         *
         * ↓
         *
         * Multi에서는 CharacterManager_Multi 작업 후
         * Server 판정용 코드로 교체한다.
         *
         * 지금은 호출하지 않는다.
         */

        SetState(
            BattleState.Turn
        );
    }

    // =========================================================
    // Single의 OnTurnEnded()
    // =========================================================

    private void OnTurnEnded()
    {
        if (!IsServer)
            return;

        SetState(
            BattleState.TurnEnd
        );

        Debug.Log(
            $"[BattleManager_Multi] " +
            $"Turn {_turnManager.GetCurrentTurn()} Ended"
        );

        PlayAlternatingTurnEnd();

        /*
         * Single:
         *
         * playerCharacterManager.OnTurnEnd();
         * enemyCharacterManager.OnTurnEnd();
         *
         * 이것도 CharacterManager_Multi에서
         * Network 구조로 교체 예정.
         */

        if (CurrentState !=
            BattleState.BattleFinish)
        {
            _turnManager?.StartTurn();
        }
    }

    // =========================================================
    // Single의 PlayAlternatingTurnEnd()
    // =========================================================

    private void PlayAlternatingTurnEnd()
    {
        /*
         * 현재는 Host에서만 실행된다.
         *
         * 나중에는
         * Network Event / RPC를 이용해
         * Host + Client 양쪽에서 같은 효과음을 재생하도록 변경한다.
         */

        SoundManager.Instance?.Play(
            _nextTurnEndIsFirst
                ? EffectSound.TurnEnd1
                : EffectSound.TurnEnd2
        );

        _nextTurnEndIsFirst =
            !_nextTurnEndIsFirst;
    }

    // =========================================================
    // Multi 실제 Damage
    // =========================================================

    public void ApplyDamageServer(
        ulong targetClientId,
        int damage)
    {
        if (!IsServer)
            return;

        if (!_battleStarted)
            return;

        if (damage <= 0)
            return;

        int currentHealth;

        if (targetClientId ==
            gameNetworkState.Player0ClientId.Value)
        {
            currentHealth =
                gameNetworkState.Player0Health.Value;
        }
        else if (
            targetClientId ==
            gameNetworkState.Player1ClientId.Value)
        {
            currentHealth =
                gameNetworkState.Player1Health.Value;
        }
        else
        {
            Debug.LogWarning(
                "[BattleManager_Multi] " +
                $"잘못된 ClientId: {targetClientId}"
            );

            return;
        }

        int nextHealth =
            Mathf.Max(
                0,
                currentHealth - damage
            );

        gameNetworkState.SetHealthServer(
            targetClientId,
            nextHealth
        );

        Debug.Log(
            "[BattleManager_Multi] Damage\n" +
            $"Target: {targetClientId}\n" +
            $"Damage: {damage}\n" +
            $"HP: {currentHealth} -> {nextHealth}"
        );

        if (nextHealth <= 0)
        {
            FinishBattleServer(
                targetClientId
            );
        }
    }

    // =========================================================
    // Single의 NotifyDefeat() 대응
    // =========================================================

    private void FinishBattleServer(
        ulong loserClientId)
    {
        if (!IsServer)
            return;

        if (CurrentState ==
            BattleState.BattleFinish)
        {
            return;
        }

        SetState(
            BattleState.BattleFinish
        );

        _nextTurnEndIsFirst = true;

        ulong winnerClientId;

        if (loserClientId ==
            gameNetworkState.Player0ClientId.Value)
        {
            winnerClientId =
                gameNetworkState.Player1ClientId.Value;
        }
        else
        {
            winnerClientId =
                gameNetworkState.Player0ClientId.Value;
        }

        gameNetworkState.SetWinnerServer(
            winnerClientId
        );

        gameNetworkState.SetMatchStateServer(
            MultiMatchState.BattleFinished
        );

        _battleStarted = false;

        SoundManager.Instance?.StopBgm();

        Debug.Log(
            "[BattleManager_Multi] Battle Finished\n" +
            $"Winner: {winnerClientId}\n" +
            $"Loser: {loserClientId}"
        );
    }

    // =========================================================
    // Single의 GetOpponent()와 같은 View 함수
    // =========================================================

    public CharacterManager GetOpponent(
        CharacterManager user)
    {
        return user ==
               playerCharacterManager
            ? enemyCharacterManager
            : playerCharacterManager;
    }

    // =========================================================
    // Network 기준 상대 찾기
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
    // Single의 GetEmoji()
    // =========================================================

    public static Sprite GetEmoji(
        EffectType effectType)
    {
        if (_emojiCache == null)
        {
            _emojiCache =
                new Dictionary<
                    EffectType,
                    Sprite
                >();

            if (Instance == null)
                return null;

            foreach (
                var entry in
                Instance.effectEmojis)
            {
                _emojiCache[
                    entry.effectType
                ] = entry.sprite;
            }
        }

        _emojiCache.TryGetValue(
            effectType,
            out var sprite
        );

        return sprite;
    }

    // =========================================================
    // Single의 Update()
    //
    // Single:
    // GameManager.Instance.GetGameState()
    //
    // Multi:
    // Server + _battleStarted를 기준으로 한다.
    // =========================================================

    private void Update()
    {
        if (!IsServer)
            return;

        if (!_battleStarted)
            return;

        Tick(
            Time.deltaTime
        );
    }
}