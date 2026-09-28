using Unity.Netcode;
using UnityEngine;

public sealed class MultiPlayGameBootstrap : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [Header("Initial State")]
    [SerializeField]
    private int initialHealth = 100;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        InitializePlayersServer();
    }

    private void InitializePlayersServer()
    {
        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[MultiPlayGameBootstrap] GameNetworkState가 없습니다."
            );
            return;
        }

        var clients =
            NetworkManager.Singleton.ConnectedClientsIds;

        if (clients.Count != 2)
        {
            Debug.LogError(
                "[MultiPlayGameBootstrap] " +
                $"현재 연결 수가 2명이 아닙니다: {clients.Count}"
            );
            return;
        }

        ulong player0 = clients[0];
        ulong player1 = clients[1];

        // 순서가 달라져도 항상 작은 ClientId = Player0
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
    }
}