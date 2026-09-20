using Unity.Netcode;
using UnityEngine;

public sealed class MultiPlayModeEntryDebug
    : MonoBehaviour
{
    [Header("Temporary Disable")]

    [SerializeField]
    private GameObject mapManagerRoot;


    private void Awake()
    {
        if (mapManagerRoot != null)
        {
            mapManagerRoot.SetActive(false);

            Debug.Log(
                "[MultiPlayMode] " +
                "기존 Single MapManager 비활성화"
            );
        }
    }


    private void Start()
    {
        NetworkManager networkManager =
            NetworkManager.Singleton;


        if (networkManager == null)
        {
            Debug.LogError(
                "[MultiPlayMode] " +
                "NetworkManager 없음"
            );

            return;
        }


        Debug.Log(
            "[MultiPlayMode] 입장 성공 | " +
            $"LocalClientId: " +
            $"{networkManager.LocalClientId} | " +
            $"IsHost: {networkManager.IsHost} | " +
            $"IsServer: {networkManager.IsServer} | " +
            $"IsClient: {networkManager.IsClient} | " +
            $"Connected: " +
            $"{networkManager.IsConnectedClient}"
        );


        if (!networkManager.IsServer)
        {
            return;
        }


        Debug.Log(
            "[MultiPlayMode] Server 기준 접속자 수: " +
            $"{networkManager.ConnectedClientsIds.Count}"
        );


        foreach (
            ulong clientId
            in networkManager.ConnectedClientsIds)
        {
            Debug.Log(
                "[MultiPlayMode] 접속 ClientId: " +
                clientId
            );
        }
    }
}