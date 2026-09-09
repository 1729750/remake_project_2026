using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NGO의 Client 연결 / 연결 종료 이벤트 감지만 담당한다.
/// Session 생성/참가 로직과 게임 로직은 담당하지 않는다.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(NetworkStatusHub))]
[RequireComponent(typeof(NetworkSessionService))]
public sealed class NetworkConnectionMonitor : MonoBehaviour
{
    private NetworkManager networkManager;
    private NetworkStatusHub statusHub;
    private NetworkSessionService sessionService;

    public int ConnectedPlayerCount =>
        networkManager != null &&
        networkManager.IsListening
            ? networkManager.ConnectedClientsList.Count
            : 0;

    public event Action<ulong> ClientConnected;
    public event Action<ulong> ClientDisconnected;
    public event Action<int> PlayerCountChanged;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        statusHub = GetComponent<NetworkStatusHub>();
        sessionService = GetComponent<NetworkSessionService>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback +=
            HandleClientConnected;

        networkManager.OnClientDisconnectCallback +=
            HandleClientDisconnected;
    }

    private void Unsubscribe()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -=
            HandleClientConnected;

        networkManager.OnClientDisconnectCallback -=
            HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"NGO Client 연결 완료: {clientId}");
        ClientConnected?.Invoke(clientId);

        if (networkManager.IsServer)
        {
            int playerCount =
                networkManager.ConnectedClientsList.Count;

            PlayerCountChanged?.Invoke(playerCount);

            // Host 자신의 최초 연결(1명)에서는
            // 방 생성 완료/JoinCode 메시지를 덮어쓰지 않는다.
            if (playerCount > 1)
            {
                statusHub.SetStatus(
                    $"플레이어 접속\n" +
                    $"접속 인원: {playerCount}/{sessionService.MaxPlayers}"
                );
            }

            return;
        }

        if (clientId == networkManager.LocalClientId)
        {
            statusHub.SetStatus(
                $"Relay 네트워크 접속 완료\n" +
                $"Client ID: {clientId}"
            );
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"NGO Client 연결 종료: {clientId}");
        ClientDisconnected?.Invoke(clientId);

        if (networkManager != null && networkManager.IsServer)
        {
            int playerCount =
                networkManager.ConnectedClientsList.Count;

            PlayerCountChanged?.Invoke(playerCount);

            statusHub.SetStatus(
                $"플레이어 연결 종료\n" +
                $"현재 인원: {playerCount}/{sessionService.MaxPlayers}"
            );

            return;
        }

        statusHub.SetStatus("Host와의 연결이 종료되었습니다.");
    }
}
