using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class PlayerPreparationRegistry
    : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private NetworkConnectionMonitor connectionMonitor;

    private readonly Dictionary<
        ulong,
        PlayerPreparationData
    > players = new();


    public int Count => players.Count;

    public IEnumerable<PlayerPreparationData>
        Players => players.Values;


    public event Action<ulong> PlayerRegistered;

    public event Action<ulong> PlayerRemoved;


    private void Awake()
    {
        EnsureReferences();
    }


    private void OnEnable()
    {
        Debug.Log("[PlayerPreparationRegistry] OnEnable 실행");

        EnsureReferences();

        if (connectionMonitor == null)
        {
            Debug.LogError(
                "[PlayerPreparationRegistry] " +
                "NetworkConnectionMonitor가 없습니다."
            );

            return;
        }

        connectionMonitor.ClientConnected +=
            HandleClientConnected;

        connectionMonitor.ClientDisconnected +=
            HandleClientDisconnected;

        RegisterAlreadyConnectedPlayers();
    }


    private void OnDisable()
    {
        if (connectionMonitor == null)
            return;

        connectionMonitor.ClientConnected -=
            HandleClientConnected;

        connectionMonitor.ClientDisconnected -=
            HandleClientDisconnected;
    }


    private void EnsureReferences()
    {
        if (connectionMonitor != null)
            return;

        connectionMonitor =
            FindFirstObjectByType<
                NetworkConnectionMonitor>();
    }


    public bool TryGetPlayer(
        ulong clientId,
        out PlayerPreparationData data)
    {
        return players.TryGetValue(
            clientId,
            out data
        );
    }


    public bool ContainsPlayer(
        ulong clientId)
    {
        return players.ContainsKey(
            clientId
        );
    }


    private void HandleClientConnected(
        ulong clientId)
    {
        if (!IsServer())
            return;

        RegisterPlayer(clientId);
    }


    private void HandleClientDisconnected(
        ulong clientId)
    {
        if (!IsServer())
            return;

        if (!players.Remove(clientId))
            return;

        Debug.Log(
            "[PlayerPreparationRegistry] " +
            $"준비 데이터 제거 | " +
            $"ClientId: {clientId} | " +
            $"Count: {players.Count}"
        );

        PlayerRemoved?.Invoke(clientId);
    }


    private void RegisterPlayer(
        ulong clientId)
    {
        if (players.ContainsKey(clientId))
            return;

        players.Add(
            clientId,
            new PlayerPreparationData(
                clientId
            )
        );

        Debug.Log(
            "[PlayerPreparationRegistry] " +
            $"준비 데이터 생성 | " +
            $"ClientId: {clientId} | " +
            $"Count: {players.Count}"
        );

        PlayerRegistered?.Invoke(clientId);
    }


    private void RegisterAlreadyConnectedPlayers()
    {
        if (!IsServer())
            return;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList)
        {
            RegisterPlayer(
                client.ClientId
            );
        }
    }


    private bool IsServer()
    {
        return
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer;
    }
}