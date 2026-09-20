using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class BattleManager_Multi : NetworkBehaviour
{
    public static BattleManager_Multi Instance { get; private set; }

    [Header("Network")]
    [SerializeField] private GameNetworkState gameNetworkState;

    [Header("Battle View")]
    [SerializeField] private CharacterManager_Multi playerCharacterManager;
    [SerializeField] private CharacterManager_Multi enemyCharacterManager;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private float startDelay = 3f;
    [SerializeField] private List<EffectEmoji> effectEmojis = new List<EffectEmoji>();

    private TurnTimerOverlay _turnTimerOverlay;
    private float _startElapsed;
    private float _turnElapsed;
    private int _currentTurnNumber;

    private static Dictionary<EffectType, Sprite> _emojiCache;

    private int _startTickCount;
    private bool _nextTurnEndIsFirst = true;
    private bool _battleStarted;

    public BattleState CurrentState { get; private set; }
    public bool BattleStarted => _battleStarted;
    public GameNetworkState NetworkState => gameNetworkState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (gameNetworkState != null)
            gameNetworkState.StateChanged += HandleNetworkStateChanged;

        Init();
        TryBindLocalViews();
    }

    public override void OnNetworkDespawn()
    {
        if (gameNetworkState != null)
            gameNetworkState.StateChanged -= HandleNetworkStateChanged;
    }

    public void Init()
    {
        playerCharacterManager?.Init();
        enemyCharacterManager?.Init();

        if (_turnTimerOverlay != null)
            Destroy(_turnTimerOverlay.gameObject);

        if (turnText == null)
            return;

        GameObject overlayGO =
            new GameObject("TurnTimerOverlay_Multi");

        overlayGO.transform.SetParent(turnText.transform.parent);
        overlayGO.transform.localPosition = Vector3.zero;
        overlayGO.transform.localScale = Vector3.one;

        overlayGO.AddComponent<MeshFilter>();
        overlayGO.AddComponent<MeshRenderer>();
        _turnTimerOverlay = overlayGO.AddComponent<TurnTimerOverlay>();
    }

    private void TryBindLocalViews()
    {
        if (gameNetworkState == null ||
            !gameNetworkState.PlayersAssigned ||
            NetworkManager.Singleton == null)
        {
            return;
        }

        ulong localId = NetworkManager.Singleton.LocalClientId;
        ulong opponentId = gameNetworkState.GetOpponentClientId(localId);

        if (opponentId == GameNetworkState.UnassignedClientId)
            return;

        // 각 PC에서 Player = 나, Enemy = 상대.
        playerCharacterManager?.BindClientId(localId, true);
        enemyCharacterManager?.BindClientId(opponentId, false);
    }

    public void InitializeBattleServer()
    {
        if (!IsServer || _battleStarted)
            return;

        if (gameNetworkState == null ||
            !gameNetworkState.PlayersAssigned)
        {
            return;
        }

        TryBindLocalViews();

        // 현재 단계에서는 Inspector Start Deck으로 실제 Server 캐릭터를 만든다.
        // FinalDeck 네트워크 연동 후 이 소스만 실제 플레이어 덱으로 교체한다.
        playerCharacterManager?.InitializeConfiguredCharacterServer();
        enemyCharacterManager?.InitializeConfiguredCharacterServer();

        _battleStarted = true;
        _startElapsed = 0f;
        _turnElapsed = 0f;
        _currentTurnNumber = 0;
        _startTickCount = 0;
        _nextTurnEndIsFirst = true;

        _turnTimerOverlay?.SetFill(0f);

        SetState(BattleState.BattleStarting);
        gameNetworkState.SetCurrentTurnServer(0);
        gameNetworkState.SetMatchStateServer(MultiMatchState.BattleStarting);

        SoundManager.Instance?.StopBgm();
        UpdateStartCountdownText();
    }

    private void UpdateStartCountdownText()
    {
        if (turnText == null)
            return;

        int remaining = Mathf.CeilToInt(
            Mathf.Max(startDelay - _startElapsed, 0f));

        turnText.text = remaining.ToString();
    }

    public static List<CardDefinition> UnpackCardCollection(
        CardCollection collection)
    {
        var result = new List<CardDefinition>();

        if (collection == null)
            return result;

        foreach (CardDefinition def in collection.GetCards())
        {
            if (def != null)
                result.Add(Instantiate(def));
        }

        return result;
    }

    public void Tick(float deltaTime)
    {
        if (!IsServer || !_battleStarted)
            return;

        if (CurrentState == BattleState.BattleStarting)
        {
            _startElapsed += deltaTime;

            float startRatio = startDelay > 0f
                ? Mathf.Clamp01(_startElapsed / startDelay)
                : 1f;

            _turnTimerOverlay?.SetFill(startRatio);

            int maxTicks = Mathf.Max(0, Mathf.FloorToInt(startDelay));
            int desiredTicks = Mathf.Min(
                Mathf.FloorToInt(_startElapsed) + 1,
                maxTicks);

            while (_startTickCount < desiredTicks)
            {
                _startTickCount++;
                PlayAlternatingTurnEnd();
            }

            if (_startElapsed >= startDelay)
                OnBattleStarted();
            else
                UpdateStartCountdownText();

            return;
        }

        if (CurrentState == BattleState.Turn)
        {
            _turnElapsed += deltaTime;

            float turnRatio = turnDuration > 0f
                ? Mathf.Clamp01(_turnElapsed / turnDuration)
                : 1f;

            _turnTimerOverlay?.SetFill(turnRatio);

            if (_turnElapsed >= turnDuration)
                OnTurnEnded();
        }
    }

    private void SetState(BattleState state)
    {
        CurrentState = state;
    }

    private void OnBattleStarted()
    {
        if (!IsServer || CurrentState != BattleState.BattleStarting)
            return;

        gameNetworkState.SetMatchStateServer(MultiMatchState.Battle);

        SoundManager.Instance?.Play(EffectSound.BattleStart);
        SoundManager.Instance?.Play(BgmName.BattleBGM);

        StartNextTurnServer();
    }

    private void StartNextTurnServer()
    {
        if (!IsServer || !_battleStarted ||
            CurrentState == BattleState.BattleFinish)
        {
            return;
        }

        _currentTurnNumber++;
        _turnElapsed = 0f;

        gameNetworkState.SetCurrentTurnServer(_currentTurnNumber);

        if (turnText != null)
            turnText.text = _currentTurnNumber.ToString();

        _turnTimerOverlay?.SetFill(0f);
        OnTurnStarted();
    }

    private void OnTurnStarted()
    {
        if (!IsServer)
            return;

        SetState(BattleState.TurnStart);

        playerCharacterManager?.OnTurnStart();
        if (CurrentState == BattleState.BattleFinish)
            return;

        enemyCharacterManager?.OnTurnStart();
        if (CurrentState == BattleState.BattleFinish)
            return;

        SetState(BattleState.Turn);
    }

    private void OnTurnEnded()
    {
        if (!IsServer || CurrentState != BattleState.Turn)
            return;

        SetState(BattleState.TurnEnd);
        PlayAlternatingTurnEnd();

        playerCharacterManager?.OnTurnEnd();
        if (CurrentState == BattleState.BattleFinish)
            return;

        enemyCharacterManager?.OnTurnEnd();
        if (CurrentState == BattleState.BattleFinish)
            return;

        StartNextTurnServer();
    }

    private void PlayAlternatingTurnEnd()
    {
        SoundManager.Instance?.Play(
            _nextTurnEndIsFirst
                ? EffectSound.TurnEnd1
                : EffectSound.TurnEnd2);

        _nextTurnEndIsFirst = !_nextTurnEndIsFirst;
    }

    public void RequestSelectCard(int index)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (IsServer)
        {
            ExecuteSelectCardServer(
                NetworkManager.Singleton.LocalClientId,
                index);
            return;
        }

        RequestSelectCardServerRpc(index);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSelectCardServerRpc(
        int index,
        ServerRpcParams rpcParams = default)
    {
        ExecuteSelectCardServer(
            rpcParams.Receive.SenderClientId,
            index);
    }

    private void ExecuteSelectCardServer(
        ulong senderClientId,
        int index)
    {
        if (!IsServer || CurrentState != BattleState.Turn)
            return;

        CharacterManager_Multi character =
            GetCharacterByClientId(senderClientId);

        character?.SelectCardServer(index);
    }

    public void ApplyDamageServer(
        ulong targetClientId,
        int damage)
    {
        if (!IsServer || !_battleStarted || damage <= 0)
            return;

        CharacterManager_Multi target =
            GetCharacterByClientId(targetClientId);

        target?.Attacked(damage);
    }

    public void NotifyDefeat(CharacterManager_Multi loser)
    {
        if (!IsServer ||
            loser == null ||
            CurrentState == BattleState.BattleFinish)
        {
            return;
        }

        SetState(BattleState.BattleFinish);
        _nextTurnEndIsFirst = true;

        playerCharacterManager?.ClearHandAndQueue();
        enemyCharacterManager?.ClearHandAndQueue();

        SoundManager.Instance?.StopBgm();

        ulong loserClientId = loser.RepresentedClientId;
        ulong winnerClientId =
            gameNetworkState.GetOpponentClientId(loserClientId);

        gameNetworkState.SetWinnerServer(winnerClientId);
        gameNetworkState.SetMatchStateServer(MultiMatchState.BattleFinished);

        _battleStarted = false;
    }

    public CharacterManager_Multi GetOpponent(
        CharacterManager_Multi user)
    {
        return user == playerCharacterManager
            ? enemyCharacterManager
            : playerCharacterManager;
    }

    public CharacterManager_Multi GetCharacterByClientId(
        ulong clientId)
    {
        if (playerCharacterManager != null &&
            playerCharacterManager.RepresentedClientId == clientId)
        {
            return playerCharacterManager;
        }

        if (enemyCharacterManager != null &&
            enemyCharacterManager.RepresentedClientId == clientId)
        {
            return enemyCharacterManager;
        }

        return null;
    }

    public ulong GetOpponentClientId(ulong clientId)
    {
        return gameNetworkState != null
            ? gameNetworkState.GetOpponentClientId(clientId)
            : GameNetworkState.UnassignedClientId;
    }

    public static Sprite GetEmoji(EffectType effectType)
    {
        if (_emojiCache == null)
        {
            _emojiCache = new Dictionary<EffectType, Sprite>();

            if (Instance == null)
                return null;

            foreach (EffectEmoji entry in Instance.effectEmojis)
                _emojiCache[entry.effectType] = entry.sprite;
        }

        _emojiCache.TryGetValue(effectType, out Sprite sprite);
        return sprite;
    }

    private void HandleNetworkStateChanged()
    {
        TryBindLocalViews();

        if (!IsServer &&
            turnText != null &&
            gameNetworkState != null &&
            gameNetworkState.MatchState.Value == MultiMatchState.Battle)
        {
            turnText.text =
                gameNetworkState.CurrentTurnNumber.Value.ToString();
        }
    }

    private void Update()
    {
        if (!IsServer || !_battleStarted)
            return;

        Tick(Time.deltaTime);
    }
}
