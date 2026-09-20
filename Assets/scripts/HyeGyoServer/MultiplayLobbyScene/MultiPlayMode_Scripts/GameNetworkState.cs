using System;
using Unity.Netcode;
using UnityEngine;

public sealed class GameNetworkState : NetworkBehaviour
{
    public const ulong UnassignedClientId = ulong.MaxValue;

    public NetworkVariable<ulong> Player0ClientId = new(
        UnassignedClientId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> Player1ClientId = new(
        UnassignedClientId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Player0Health = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Player1Health = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action StateChanged;

    public bool PlayersAssigned =>
        Player0ClientId.Value != UnassignedClientId &&
        Player1ClientId.Value != UnassignedClientId;

    public override void OnNetworkSpawn()
    {
        Player0ClientId.OnValueChanged += HandleClientIdChanged;
        Player1ClientId.OnValueChanged += HandleClientIdChanged;

        Player0Health.OnValueChanged += HandleHealthChanged;
        Player1Health.OnValueChanged += HandleHealthChanged;

        // Client는 이 시점에 초기 NetworkVariable 값을 이미 읽을 수 있다.
        StateChanged?.Invoke();

        Debug.Log(
            $"[GameNetworkState] OnNetworkSpawn\n" +
            $"IsServer: {IsServer}\n" +
            $"LocalClientId: {NetworkManager.Singleton.LocalClientId}\n" +
            $"Player0: {Player0ClientId.Value}\n" +
            $"Player1: {Player1ClientId.Value}"
        );
    }

    public override void OnNetworkDespawn()
    {
        Player0ClientId.OnValueChanged -= HandleClientIdChanged;
        Player1ClientId.OnValueChanged -= HandleClientIdChanged;

        Player0Health.OnValueChanged -= HandleHealthChanged;
        Player1Health.OnValueChanged -= HandleHealthChanged;
    }

    public void InitializePlayersServer(
        ulong player0ClientId,
        ulong player1ClientId,
        int initialHealth)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[GameNetworkState] InitializePlayersServer는 Server만 호출할 수 있습니다."
            );
            return;
        }

        if (PlayersAssigned)
        {
            Debug.LogWarning(
                "[GameNetworkState] Player0 / Player1은 이미 확정되어 있습니다."
            );
            return;
        }

        Player0ClientId.Value = player0ClientId;
        Player1ClientId.Value = player1ClientId;

        Player0Health.Value = initialHealth;
        Player1Health.Value = initialHealth;

        Debug.Log(
            $"[GameNetworkState] 플레이어 확정\n" +
            $"Player0ClientId: {player0ClientId}\n" +
            $"Player1ClientId: {player1ClientId}\n" +
            $"InitialHealth: {initialHealth}"
        );
    }

    public void SetHealthServer(ulong clientId, int newHealth)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[GameNetworkState] HP 변경은 Server만 가능합니다."
            );
            return;
        }

        newHealth = Mathf.Max(0, newHealth);

        if (clientId == Player0ClientId.Value)
        {
            Player0Health.Value = newHealth;
            return;
        }

        if (clientId == Player1ClientId.Value)
        {
            Player1Health.Value = newHealth;
            return;
        }

        Debug.LogWarning(
            $"[GameNetworkState] 등록되지 않은 ClientId입니다: {clientId}"
        );
    }

    private void HandleClientIdChanged(ulong previousValue, ulong newValue)
    {
        StateChanged?.Invoke();
    }

    private void HandleHealthChanged(int previousValue, int newValue)
    {
        StateChanged?.Invoke();
    }
}