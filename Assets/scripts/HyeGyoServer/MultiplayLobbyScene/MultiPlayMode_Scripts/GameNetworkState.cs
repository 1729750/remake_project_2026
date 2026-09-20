using System;
using Unity.Netcode;
using UnityEngine;

public enum MultiMatchState : byte
{
    WaitingForPlayers,
    BattleStarting,
    Battle,
    BattleFinished
}

public sealed class GameNetworkState : NetworkBehaviour
{
    public const ulong UnassignedClientId = ulong.MaxValue;

    // =========================
    // Player Mapping
    // =========================

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

    // =========================
    // Battle State
    // =========================

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

    public NetworkVariable<MultiMatchState> MatchState = new(
        MultiMatchState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> WinnerClientId = new(
        UnassignedClientId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action StateChanged;

    public bool PlayersAssigned =>
        Player0ClientId.Value != UnassignedClientId &&
        Player1ClientId.Value != UnassignedClientId;

    public override void OnNetworkSpawn()
    {
        Player0ClientId.OnValueChanged += HandleULongChanged;
        Player1ClientId.OnValueChanged += HandleULongChanged;

        Player0Health.OnValueChanged += HandleIntChanged;
        Player1Health.OnValueChanged += HandleIntChanged;

        MatchState.OnValueChanged += HandleMatchStateChanged;
        WinnerClientId.OnValueChanged += HandleULongChanged;

        StateChanged?.Invoke();

        Debug.Log(
            "[GameNetworkState] Spawn\n" +
            $"IsServer: {IsServer}\n" +
            $"LocalClientId: {NetworkManager.Singleton.LocalClientId}"
        );
    }

    public override void OnNetworkDespawn()
    {
        Player0ClientId.OnValueChanged -= HandleULongChanged;
        Player1ClientId.OnValueChanged -= HandleULongChanged;

        Player0Health.OnValueChanged -= HandleIntChanged;
        Player1Health.OnValueChanged -= HandleIntChanged;

        MatchState.OnValueChanged -= HandleMatchStateChanged;
        WinnerClientId.OnValueChanged -= HandleULongChanged;
    }

    public void InitializePlayersServer(
        ulong player0ClientId,
        ulong player1ClientId,
        int initialHealth)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[GameNetworkState] Server만 Player를 초기화할 수 있습니다."
            );
            return;
        }

        if (PlayersAssigned)
        {
            Debug.LogWarning(
                "[GameNetworkState] Player0/Player1은 이미 확정됐습니다."
            );
            return;
        }

        Player0ClientId.Value = player0ClientId;
        Player1ClientId.Value = player1ClientId;

        Player0Health.Value = initialHealth;
        Player1Health.Value = initialHealth;

        WinnerClientId.Value = UnassignedClientId;
        MatchState.Value = MultiMatchState.WaitingForPlayers;

        Debug.Log(
            "[GameNetworkState] Player Mapping 완료\n" +
            $"Player0: {player0ClientId}\n" +
            $"Player1: {player1ClientId}\n" +
            $"HP: {initialHealth}"
        );
    }

    public void SetHealthServer(
        ulong clientId,
        int health)
    {
        if (!IsServer)
            return;

        health = Mathf.Max(0, health);

        if (clientId == Player0ClientId.Value)
        {
            Player0Health.Value = health;
            return;
        }

        if (clientId == Player1ClientId.Value)
        {
            Player1Health.Value = health;
            return;
        }

        Debug.LogWarning(
            $"[GameNetworkState] 등록되지 않은 ClientId: {clientId}"
        );
    }

    public void SetMatchStateServer(
        MultiMatchState state)
    {
        if (!IsServer)
            return;

        MatchState.Value = state;
    }

    public void SetWinnerServer(
        ulong clientId)
    {
        if (!IsServer)
            return;

        WinnerClientId.Value = clientId;
    }

    private void HandleULongChanged(
        ulong previousValue,
        ulong newValue)
    {
        StateChanged?.Invoke();
    }

    private void HandleIntChanged(
        int previousValue,
        int newValue)
    {
        StateChanged?.Invoke();
    }

    private void HandleMatchStateChanged(
        MultiMatchState previousValue,
        MultiMatchState newValue)
    {
        StateChanged?.Invoke();
    }
}