using Unity.Netcode;
using UnityEngine;

public sealed class GameManager_Multi : NetworkBehaviour
{
    public static GameManager_Multi Instance { get; private set; }

    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [Header("Managers")]
    [SerializeField]
    private BattleManager_Multi battleManager;

    private bool _gameStarted;

    public bool IsGameStarted => _gameStarted;

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
        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[GameManager_Multi] GameNetworkState가 연결되지 않았습니다."
            );
            return;
        }

        gameNetworkState.StateChanged += HandleNetworkStateChanged;

        Debug.Log(
            "[GameManager_Multi] Network Spawn\n" +
            $"IsServer: {IsServer}\n" +
            $"IsClient: {IsClient}\n" +
            $"LocalClientId: {NetworkManager.Singleton.LocalClientId}"
        );

        if (IsServer)
        {
            TryStartGameServer();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged -=
                HandleNetworkStateChanged;
        }
    }

    private void HandleNetworkStateChanged()
    {
        if (!IsServer)
            return;

        TryStartGameServer();
    }

    private void TryStartGameServer()
    {
        if (!IsServer)
            return;

        if (_gameStarted)
            return;

        if (!gameNetworkState.PlayersAssigned)
        {
            Debug.Log(
                "[GameManager_Multi] Player0 / Player1 확정 대기 중"
            );

            return;
        }

        if (battleManager == null)
        {
            Debug.LogError(
                "[GameManager_Multi] BattleManager_Multi가 연결되지 않았습니다."
            );

            return;
        }

        _gameStarted = true;

        Debug.Log(
            "[GameManager_Multi] 게임 시작 조건 완료\n" +
            $"Player0: {gameNetworkState.Player0ClientId.Value}\n" +
            $"Player1: {gameNetworkState.Player1ClientId.Value}"
        );

        gameNetworkState.SetMatchStateServer(
            MultiMatchState.BattleStarting
        );

        battleManager.InitializeBattleServer();
    }
}