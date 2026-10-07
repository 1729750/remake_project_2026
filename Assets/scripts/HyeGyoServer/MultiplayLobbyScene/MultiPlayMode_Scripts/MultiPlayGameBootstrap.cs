using Unity.Netcode;
using UnityEngine;

/// <summary>
/// MultiPlayMode 진입 후 서버의 플레이어 할당과
/// 테스트용 즉시 전투 시작을 담당한다.
///
/// 플레이어 접속/씬 로드 타이밍이 서로 다를 수 있으므로
/// OnNetworkSpawn에서 한 번만 검사하지 않고 Update에서 재시도한다.
/// </summary>
public sealed class MultiPlayGameBootstrap : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [SerializeField]
    private BattleManager_Multi battleManager;

    [Header("Initial State")]
    [SerializeField]
    private int initialHealth = 100;

    [Header("Temporary Vertical Slice")]
    [Tooltip("준비 10회를 아직 사용하지 않는 연결/전투 검증용 옵션입니다.")]
    [SerializeField]
    private bool startBattleImmediately = true;

    private bool playersInitialized;
    private bool battleStartRequested;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        TryInitializePlayersServer();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        TryInitializePlayersServer();

        if (!startBattleImmediately ||
            battleStartRequested ||
            !playersInitialized)
        {
            return;
        }

        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager_Multi>();
        }

        // BattleManager의 NetworkObject가 먼저 Spawn된 뒤에 시작한다.
        if (battleManager == null ||
            !battleManager.IsSpawned)
        {
            return;
        }

        if (battleManager.BattleStarted)
        {
            battleStartRequested = true;
            return;
        }

        battleStartRequested = true;

        Debug.Log(
            "[MultiPlayGameBootstrap] " +
            "플레이어 할당 완료 → 테스트 전투 초기화"
        );

        battleManager.InitializeBattleServer();
    }

    private void TryInitializePlayersServer()
    {
        if (playersInitialized ||
            gameNetworkState == null ||
            NetworkManager.Singleton == null)
        {
            return;
        }

        var clients =
            NetworkManager.Singleton.ConnectedClientsIds;

        if (clients.Count != 2)
        {
            return;
        }

        ulong player0 = clients[0];
        ulong player1 = clients[1];

        // ClientId 순서를 고정해 양쪽 화면의 Player0/Player1 의미를 맞춘다.
        if (player1 < player0)
        {
            (player0, player1) =
                (player1, player0);
        }

        gameNetworkState.InitializePlayersServer(
            player0,
            player1,
            initialHealth
        );

        playersInitialized =
            gameNetworkState.PlayersAssigned;

        Debug.Log(
            "[MultiPlayGameBootstrap] " +
            $"Player0={player0}, Player1={player1} 등록 완료"
        );
    }
}
