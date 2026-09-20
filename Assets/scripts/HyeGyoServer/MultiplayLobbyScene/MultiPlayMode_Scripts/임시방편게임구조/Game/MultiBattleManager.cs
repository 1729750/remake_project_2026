using Unity.Netcode;
using UnityEngine;

public sealed class MultiBattleManager : NetworkBehaviour
{
    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    private bool battleStarted;

    public bool BattleStarted =>
        battleStarted;

    public void InitializeBattleServer()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[MultiBattleManager] Server만 전투를 시작할 수 있습니다."
            );
            return;
        }

        if (battleStarted)
            return;

        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[MultiBattleManager] GameNetworkState가 없습니다."
            );
            return;
        }

        if (!gameNetworkState.PlayersAssigned)
        {
            Debug.LogWarning(
                "[MultiBattleManager] Player Mapping이 끝나지 않았습니다."
            );
            return;
        }

        battleStarted = true;

        gameNetworkState.SetMatchStateServer(
            MultiMatchState.Battle
        );

        Debug.Log(
            "[MultiBattleManager] Battle 시작\n" +
            $"P0 HP: {gameNetworkState.Player0Health.Value}\n" +
            $"P1 HP: {gameNetworkState.Player1Health.Value}"
        );
    }

    public void ApplyDamageServer(
        ulong targetClientId,
        int damage)
    {
        if (!IsServer)
            return;

        if (!battleStarted)
            return;

        if (gameNetworkState.MatchState.Value !=
            MultiMatchState.Battle)
        {
            return;
        }

        if (damage <= 0)
            return;

        int currentHealth;

        if (targetClientId ==
            gameNetworkState.Player0ClientId.Value)
        {
            currentHealth =
                gameNetworkState.Player0Health.Value;
        }
        else if (targetClientId ==
                 gameNetworkState.Player1ClientId.Value)
        {
            currentHealth =
                gameNetworkState.Player1Health.Value;
        }
        else
        {
            Debug.LogWarning(
                $"[MultiBattleManager] " +
                $"알 수 없는 Target ClientId: {targetClientId}"
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
            "[MultiBattleManager] Damage\n" +
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

    private void FinishBattleServer(
        ulong loserClientId)
    {
        if (!IsServer)
            return;

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

        battleStarted = false;

        Debug.Log(
            "[MultiBattleManager] Battle Finished\n" +
            $"Winner: {winnerClientId}\n" +
            $"Loser: {loserClientId}"
        );
    }
}