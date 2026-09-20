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

    public NetworkVariable<ulong> Player0ClientId = new(
        UnassignedClientId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<ulong> Player1ClientId = new(
        UnassignedClientId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player0MaxHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player1MaxHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player0Health = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player1Health = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player0Defense = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player1Defense = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player0Cost = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player1Cost = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> Player0Guard = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> Player1Guard = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentTurnNumber = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<MultiMatchState> MatchState = new(
        MultiMatchState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<ulong> WinnerClientId = new(
        UnassignedClientId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public event Action StateChanged;

    public bool PlayersAssigned =>
        Player0ClientId.Value != UnassignedClientId &&
        Player1ClientId.Value != UnassignedClientId;

    public override void OnNetworkSpawn()
    {
        Player0ClientId.OnValueChanged += HandleULongChanged;
        Player1ClientId.OnValueChanged += HandleULongChanged;

        Player0MaxHealth.OnValueChanged += HandleIntChanged;
        Player1MaxHealth.OnValueChanged += HandleIntChanged;
        Player0Health.OnValueChanged += HandleIntChanged;
        Player1Health.OnValueChanged += HandleIntChanged;
        Player0Defense.OnValueChanged += HandleIntChanged;
        Player1Defense.OnValueChanged += HandleIntChanged;
        Player0Cost.OnValueChanged += HandleIntChanged;
        Player1Cost.OnValueChanged += HandleIntChanged;
        CurrentTurnNumber.OnValueChanged += HandleIntChanged;

        Player0Guard.OnValueChanged += HandleBoolChanged;
        Player1Guard.OnValueChanged += HandleBoolChanged;

        MatchState.OnValueChanged += HandleMatchStateChanged;
        WinnerClientId.OnValueChanged += HandleULongChanged;

        StateChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        Player0ClientId.OnValueChanged -= HandleULongChanged;
        Player1ClientId.OnValueChanged -= HandleULongChanged;

        Player0MaxHealth.OnValueChanged -= HandleIntChanged;
        Player1MaxHealth.OnValueChanged -= HandleIntChanged;
        Player0Health.OnValueChanged -= HandleIntChanged;
        Player1Health.OnValueChanged -= HandleIntChanged;
        Player0Defense.OnValueChanged -= HandleIntChanged;
        Player1Defense.OnValueChanged -= HandleIntChanged;
        Player0Cost.OnValueChanged -= HandleIntChanged;
        Player1Cost.OnValueChanged -= HandleIntChanged;
        CurrentTurnNumber.OnValueChanged -= HandleIntChanged;

        Player0Guard.OnValueChanged -= HandleBoolChanged;
        Player1Guard.OnValueChanged -= HandleBoolChanged;

        MatchState.OnValueChanged -= HandleMatchStateChanged;
        WinnerClientId.OnValueChanged -= HandleULongChanged;
    }

    public void InitializePlayersServer(
        ulong player0ClientId,
        ulong player1ClientId,
        int initialHealth)
    {
        if (!IsServer)
            return;

        if (PlayersAssigned)
            return;

        Player0ClientId.Value = player0ClientId;
        Player1ClientId.Value = player1ClientId;

        Player0MaxHealth.Value = initialHealth;
        Player1MaxHealth.Value = initialHealth;
        Player0Health.Value = initialHealth;
        Player1Health.Value = initialHealth;

        Player0Defense.Value = 0;
        Player1Defense.Value = 0;
        Player0Cost.Value = 0;
        Player1Cost.Value = 0;
        Player0Guard.Value = false;
        Player1Guard.Value = false;

        CurrentTurnNumber.Value = 0;
        WinnerClientId.Value = UnassignedClientId;
        MatchState.Value = MultiMatchState.WaitingForPlayers;
    }

    public bool IsRegisteredClient(ulong clientId)
    {
        return clientId == Player0ClientId.Value ||
               clientId == Player1ClientId.Value;
    }

    public ulong GetOpponentClientId(ulong clientId)
    {
        if (clientId == Player0ClientId.Value)
            return Player1ClientId.Value;

        if (clientId == Player1ClientId.Value)
            return Player0ClientId.Value;

        return UnassignedClientId;
    }

    public bool TryGetCharacterPublicState(
        ulong clientId,
        out int maxHealth,
        out int health,
        out int defense,
        out int cost,
        out bool guard)
    {
        if (clientId == Player0ClientId.Value)
        {
            maxHealth = Player0MaxHealth.Value;
            health = Player0Health.Value;
            defense = Player0Defense.Value;
            cost = Player0Cost.Value;
            guard = Player0Guard.Value;
            return true;
        }

        if (clientId == Player1ClientId.Value)
        {
            maxHealth = Player1MaxHealth.Value;
            health = Player1Health.Value;
            defense = Player1Defense.Value;
            cost = Player1Cost.Value;
            guard = Player1Guard.Value;
            return true;
        }

        maxHealth = 0;
        health = 0;
        defense = 0;
        cost = 0;
        guard = false;
        return false;
    }

    public void SetCharacterPublicStateServer(
        ulong clientId,
        int maxHealth,
        int health,
        int defense,
        int cost,
        bool guard)
    {
        if (!IsServer)
            return;

        maxHealth = Mathf.Max(1, maxHealth);
        health = Mathf.Max(0, health);
        defense = Mathf.Max(0, defense);
        cost = Mathf.Max(0, cost);

        if (clientId == Player0ClientId.Value)
        {
            Player0MaxHealth.Value = maxHealth;
            Player0Health.Value = health;
            Player0Defense.Value = defense;
            Player0Cost.Value = cost;
            Player0Guard.Value = guard;
            return;
        }

        if (clientId == Player1ClientId.Value)
        {
            Player1MaxHealth.Value = maxHealth;
            Player1Health.Value = health;
            Player1Defense.Value = defense;
            Player1Cost.Value = cost;
            Player1Guard.Value = guard;
        }
    }

    public void SetHealthServer(ulong clientId, int value)
    {
        if (!IsServer)
            return;

        value = Mathf.Max(0, value);

        if (clientId == Player0ClientId.Value)
            Player0Health.Value = value;
        else if (clientId == Player1ClientId.Value)
            Player1Health.Value = value;
    }

    public void SetDefenseServer(ulong clientId, int value)
    {
        if (!IsServer)
            return;

        value = Mathf.Max(0, value);

        if (clientId == Player0ClientId.Value)
            Player0Defense.Value = value;
        else if (clientId == Player1ClientId.Value)
            Player1Defense.Value = value;
    }

    public void SetCostServer(ulong clientId, int value)
    {
        if (!IsServer)
            return;

        value = Mathf.Max(0, value);

        if (clientId == Player0ClientId.Value)
            Player0Cost.Value = value;
        else if (clientId == Player1ClientId.Value)
            Player1Cost.Value = value;
    }

    public void SetGuardServer(ulong clientId, bool value)
    {
        if (!IsServer)
            return;

        if (clientId == Player0ClientId.Value)
            Player0Guard.Value = value;
        else if (clientId == Player1ClientId.Value)
            Player1Guard.Value = value;
    }

    public void SetCurrentTurnServer(int turnNumber)
    {
        if (!IsServer)
            return;

        CurrentTurnNumber.Value = Mathf.Max(0, turnNumber);
    }

    public void SetMatchStateServer(MultiMatchState state)
    {
        if (!IsServer)
            return;

        MatchState.Value = state;
    }

    public void SetWinnerServer(ulong clientId)
    {
        if (!IsServer)
            return;

        WinnerClientId.Value = clientId;
    }

    private void HandleULongChanged(ulong previousValue, ulong newValue)
    {
        StateChanged?.Invoke();
    }

    private void HandleIntChanged(int previousValue, int newValue)
    {
        StateChanged?.Invoke();
    }

    private void HandleBoolChanged(bool previousValue, bool newValue)
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
