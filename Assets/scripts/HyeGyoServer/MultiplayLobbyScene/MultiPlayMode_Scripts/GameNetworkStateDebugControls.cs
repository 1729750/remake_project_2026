using Unity.Netcode;
using UnityEngine;

public sealed class GameNetworkStateDebugControls : MonoBehaviour
{
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [SerializeField]
    private int damageAmount = 10;

    [ContextMenu("Debug/Damage Player0")]
    private void DamagePlayer0()
    {
        Damage(
            gameNetworkState.Player0ClientId.Value,
            gameNetworkState.Player0Health.Value
        );
    }

    [ContextMenu("Debug/Damage Player1")]
    private void DamagePlayer1()
    {
        Damage(
            gameNetworkState.Player1ClientId.Value,
            gameNetworkState.Player1Health.Value
        );
    }

    private void Damage(
        ulong clientId,
        int currentHealth)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning(
                "[GameNetworkStateDebugControls] Host/Server에서만 테스트할 수 있습니다."
            );
            return;
        }

        if (gameNetworkState == null ||
            !gameNetworkState.PlayersAssigned)
        {
            return;
        }

        int newHealth =
            Mathf.Max(0, currentHealth - damageAmount);

        gameNetworkState.SetHealthServer(
            clientId,
            newHealth
        );
    }
}