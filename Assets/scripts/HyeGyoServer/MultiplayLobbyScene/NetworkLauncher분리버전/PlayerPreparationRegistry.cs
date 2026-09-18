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


    [Header("Debug")]
    [Tooltip("실제 Client 없이 두 번째 플레이어를 임시 등록합니다.")]
    [SerializeField]
    private bool addDebugSecondPlayer;


    // 실제 NGO ClientId와 구분하기 위한 테스트 전용 ID
    public const ulong DebugClientId =
        ulong.MaxValue - 1;


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
        Debug.Log(
            "[PlayerPreparationRegistry] OnEnable 실행"
        );

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

        // 이미 접속해 있는 실제 NGO 플레이어 등록
        RegisterAlreadyConnectedPlayers();

        // 개발 테스트용 가짜 두 번째 플레이어
        TryRegisterDebugSecondPlayer();
    }


    private void OnDisable()
    {
        if (connectionMonitor != null)
        {
            connectionMonitor.ClientConnected -=
                HandleClientConnected;

            connectionMonitor.ClientDisconnected -=
                HandleClientDisconnected;
        }

        RemoveDebugSecondPlayer();
    }


    private void EnsureReferences()
    {
        if (connectionMonitor != null)
        {
            return;
        }

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
        {
            return;
        }

        RegisterPlayer(clientId);

            // 개발 중이라면 가짜 두 번째 플레이어도 등록
    TryRegisterDebugSecondPlayer();
    }


    private void HandleClientDisconnected(
        ulong clientId)
    {
        if (!IsServer())
        {
            return;
        }

        RemovePlayer(clientId);
    }


    private void RegisterPlayer(
        ulong clientId)
    {
        if (players.ContainsKey(clientId))
        {
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
            $"준비 데이터 생성 | " +
            $"ClientId: {clientId} | " +
            $"Count: {players.Count}"
        );

        PlayerRegistered?.Invoke(clientId);
    }


    private void RemovePlayer(
        ulong clientId)
    {
        if (!players.Remove(clientId))
        {
            return;
        }

        Debug.Log(
            "[PlayerPreparationRegistry] " +
            $"준비 데이터 제거 | " +
            $"ClientId: {clientId} | " +
            $"Count: {players.Count}"
        );

        PlayerRemoved?.Invoke(clientId);
    }


    private void RegisterAlreadyConnectedPlayers()
    {
        if (!IsServer())
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

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


    private void TryRegisterDebugSecondPlayer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        if (!addDebugSecondPlayer)
        {
            return;
        }

        if (!IsServer())
        {
            return;
        }

        RegisterPlayer(
            DebugClientId
        );

        Debug.Log(
            "[PlayerPreparationRegistry] " +
            "Debug 두 번째 플레이어 등록 완료"
        );

#endif
    }


    private void RemoveDebugSecondPlayer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        if (!players.ContainsKey(
                DebugClientId))
        {
            return;
        }

        RemovePlayer(
            DebugClientId
        );

#endif
    }


    private bool IsServer()
    {
        return
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer;
    }
}