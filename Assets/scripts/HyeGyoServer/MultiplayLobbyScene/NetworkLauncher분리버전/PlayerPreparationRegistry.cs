using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class PlayerPreparationRegistry : MonoBehaviour
{
    [SerializeField] private NetworkConnectionMonitor connectionMonitor;
    private NetworkManager session;
    private bool subscribed;
    private readonly Dictionary<ulong, PlayerPreparationData> players = new();
    public int Count => players.Count;
    public IEnumerable<PlayerPreparationData> Players => players.Values;
    public event Action<ulong> PlayerRegistered;
    public event Action<ulong> PlayerRemoved;

    public void BindSession(NetworkManager runningSession)
    {
        if (session == runningSession && subscribed) return;
        Detach();
        session = runningSession;
        if (isActiveAndEnabled) Attach();
    }

    private void OnEnable() => Attach();
    private void OnDisable() => Detach();

    private void Attach()
    {
        if (subscribed) return;
        if (session != null)
        {
            session.OnClientConnectedCallback += HandleClientConnected;
            session.OnClientDisconnectCallback += HandleClientDisconnected;
        }
        else if (connectionMonitor != null)
        {
            connectionMonitor.ClientConnected += HandleClientConnected;
            connectionMonitor.ClientDisconnected += HandleClientDisconnected;
        }
        else return; // Gameplay explicitly binds the running session before preparation.
        subscribed = true;
        NetworkManager network = session != null ? session : NetworkManager.Singleton;
        if (network != null && network.IsServer)
            foreach (ulong clientId in network.ConnectedClientsIds) RegisterPlayer(clientId);
    }

    private void Detach()
    {
        if (!subscribed) return;
        if (session != null)
        {
            session.OnClientConnectedCallback -= HandleClientConnected;
            session.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
        else if (connectionMonitor != null)
        {
            connectionMonitor.ClientConnected -= HandleClientConnected;
            connectionMonitor.ClientDisconnected -= HandleClientDisconnected;
        }
        subscribed = false;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (IsServer()) RegisterPlayer(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (IsServer()) RemovePlayer(clientId);
    }

    private void RegisterPlayer(ulong clientId)
    {
        if (players.ContainsKey(clientId) || players.Count >= 2) return;
        players.Add(clientId, new PlayerPreparationData(clientId));
        PlayerRegistered?.Invoke(clientId);
    }

    private void RemovePlayer(ulong clientId)
    {
        if (!players.TryGetValue(clientId, out PlayerPreparationData removed)) return;
        foreach (CardDefinition card in removed.FinalDeck)
            if (card != null) Destroy(card);
        players.Remove(clientId);
        PlayerRemoved?.Invoke(clientId);
    }

    private void OnDestroy()
    {
        Detach();
        foreach (ulong clientId in new List<ulong>(players.Keys)) RemovePlayer(clientId);
    }

    public bool TryGetPlayer(ulong clientId, out PlayerPreparationData data) => players.TryGetValue(clientId, out data);
    public bool ContainsPlayer(ulong clientId) => players.ContainsKey(clientId);
    private bool IsServer() => (session != null ? session : NetworkManager.Singleton)?.IsServer == true;
}
