using Unity.Netcode;
using UnityEngine;

public sealed class MultiPlayGameBootstrap : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [Header("Initial Game State")]
    [SerializeField]
    private int initialHealth = 100;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        InitializePlayers();
    }

    private void InitializePlayers()
    {
        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[MultiPlayGameBootstrap] GameNetworkState가 연결되지 않았습니다."
            );
            return;
        }

        var connectedClientIds =
            NetworkManager.Singleton.ConnectedClientsIds;

        if (connectedClientIds.Count != 2)
        {
            Debug.LogError(
                $"[MultiPlayGameBootstrap] 게임 시작 시점의 실제 NGO 인원이 2명이 아닙니다.\n" +
                $"ConnectedClients: {connectedClientIds.Count}"
            );
            return;
        }

        ulong firstClientId = connectedClientIds[0];
        ulong secondClientId = connectedClientIds[1];

        // 연결 리스트 순서에 의존하지 않도록
        // ClientId가 작은 쪽을 Player0으로 고정한다.
        ulong player0ClientId = firstClientId;
        ulong player1ClientId = secondClientId;

        if (player1ClientId < player0ClientId)
        {
            (player0ClientId, player1ClientId) =
                (player1ClientId, player0ClientId);
        }

        gameNetworkState.InitializePlayersServer(
            player0ClientId,
            player1ClientId,
            initialHealth
        );

        Debug.Log(
            $"[MultiPlayGameBootstrap] 게임 플레이어 배치 완료\n" +
            $"Player0: {player0ClientId}\n" +
            $"Player1: {player1ClientId}\n" +
            $"ConnectedClients: {connectedClientIds.Count}"
        );
    }
}