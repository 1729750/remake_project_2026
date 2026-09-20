using Unity.Netcode;
using UnityEngine;

public sealed class MultiGameManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [SerializeField]
    private MultiBattleManager multiBattleManager;

    private bool gameStarted;

    public override void OnNetworkSpawn()
    {
        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[MultiGameManager] GameNetworkState가 없습니다."
            );
            return;
        }

        gameNetworkState.StateChanged +=
            HandleNetworkStateChanged;

        TryStartGameServer();
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
        if (IsServer)
        {
            TryStartGameServer();
        }
    }

    private void TryStartGameServer()
    {
        if (!IsServer)
            return;

        if (gameStarted)
            return;

        if (!gameNetworkState.PlayersAssigned)
            return;

        if (multiBattleManager == null)
        {
            Debug.LogError(
                "[MultiGameManager] MultiBattleManager가 없습니다."
            );
            return;
        }

        gameStarted = true;

        Debug.Log(
            "[MultiGameManager] 두 플레이어 준비 완료 → Battle 시작"
        );

        gameNetworkState.SetMatchStateServer(
            MultiMatchState.BattleStarting
        );

        multiBattleManager.InitializeBattleServer();
    }
}