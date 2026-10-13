using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server가 실제 접속 플레이어 2명의
/// 매치 준비 데이터를 보관한다.
///
/// 이 데이터 자체는 Network 동기화하지 않는다.
/// 필요한 결과만 RPC / NetworkVariable로 전달한다.
/// </summary>
public sealed class PlayerPreparationRegistry : MonoBehaviour
{
    [SerializeField]
    private NetworkConnectionMonitor connectionMonitor;

    private readonly Dictionary<
        ulong,
        PlayerPreparationData
    > players = new();

    public int Count => players.Count;

    public IEnumerable<PlayerPreparationData> Players =>
        players.Values;

    public event Action<ulong> PlayerRegistered;
    public event Action<ulong> PlayerRemoved;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
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

        RemovePlayer(clientId);
    }

    private void RegisterAlreadyConnectedPlayers()
    {
        if (!IsServer())
            return;

        if (NetworkManager.Singleton == null)
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

    private void RegisterPlayer(
        ulong clientId)
    {
        if (players.ContainsKey(clientId))
            return;

        // 1:1 게임이므로 2명 이상 등록 금지
        if (players.Count >= 2)
        {
            Debug.LogWarning(
                "[PlayerPreparationRegistry] " +
                $"3번째 플레이어 등록 거부 | {clientId}"
            );

            return;
        }

        players.Add(
            clientId,
            new PlayerPreparationData(
                clientId
            )
        );

        Debug.Log(
            "[PlayerPreparationRegistry] " +
            $"실제 플레이어 등록 | " +
            $"ClientId: {clientId} | " +
            $"Count: {players.Count}/2"
        );

        PlayerRegistered?.Invoke(clientId);
    }

    private void RemovePlayer(
        ulong clientId)
    {
        if (!players.Remove(clientId))
            return;

        Debug.Log(
            "[PlayerPreparationRegistry] " +
            $"플레이어 제거 | " +
            $"ClientId: {clientId} | " +
            $"Count: {players.Count}/2"
        );

        PlayerRemoved?.Invoke(clientId);
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

    private bool IsServer()
    {
        return
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer;
    }
}