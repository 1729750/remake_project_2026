using Unity.Netcode;
using UnityEngine;

/// <summary>
/// MultiPlayMode 진입 후 실제 접속 ClientId 두 개를
/// GameNetworkState의 Player0/Player1에 할당한다.
/// 전투 시작은 카드 준비 10회가 끝난 뒤 MatchServerController가 담당한다.
/// </summary>
public sealed class MultiPlayGameBootstrap : NetworkBehaviour
{
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [SerializeField]
    private int initialHealth = 100;

    private bool playersInitialized;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            TryInitializePlayersServer();
    }

    private void Update()
    {
        if (IsServer && !playersInitialized)
            TryInitializePlayersServer();
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
            return;

        ulong player0 = clients[0];
        ulong player1 = clients[1];

        if (player1 < player0)
            (player0, player1) = (player1, player0);

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
