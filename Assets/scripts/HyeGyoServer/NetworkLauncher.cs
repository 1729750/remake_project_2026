using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkLauncher : MonoBehaviour
{
    private NetworkManager networkManager;

    private string statusMessage = "네트워크 연결 대기";

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void OnDisable()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    /// <summary>
    /// 방장으로 네트워크를 시작합니다.
    /// Host는 Server와 Client 역할을 동시에 수행합니다.
    /// </summary>
    public void StartHost()
    {
        if (!CanStartNetwork())
            return;

        statusMessage = "Host 시작 중...";

        bool started = networkManager.StartHost();

        if (started)
        {
            statusMessage = "Host 시작 완료";
            Debug.Log("NetworkManager Host 시작 완료");
        }
        else
        {
            statusMessage = "Host 시작 실패";
            Debug.LogError("NetworkManager.StartHost() 실패");
        }
    }

    /// <summary>
    /// 다른 Host에 Client로 접속합니다.
    /// </summary>
    public void StartClient()
    {
        if (!CanStartNetwork())
            return;

        statusMessage = "Client 접속 시도 중...";

        bool started = networkManager.StartClient();

        if (started)
        {
            Debug.Log("NetworkManager Client 시작");
        }
        else
        {
            statusMessage = "Client 시작 실패";
            Debug.LogError("NetworkManager.StartClient() 실패");
        }
    }

    /// <summary>
    /// 전용 서버로 시작합니다.
    /// 현재 2인 테스트에서는 필수 기능이 아닙니다.
    /// </summary>
    public void StartServer()
    {
        if (!CanStartNetwork())
            return;

        statusMessage = "Server 시작 중...";

        bool started = networkManager.StartServer();

        if (started)
        {
            statusMessage = "Server 시작 완료";
            Debug.Log("NetworkManager Server 시작 완료");
        }
        else
        {
            statusMessage = "Server 시작 실패";
            Debug.LogError("NetworkManager.StartServer() 실패");
        }
    }

    /// <summary>
    /// 현재 네트워크 연결을 종료합니다.
    /// </summary>
    public void ShutdownNetwork()
    {
        if (networkManager == null)
            return;

        if (!networkManager.IsListening)
        {
            statusMessage = "실행 중인 네트워크가 없습니다.";
            return;
        }

        networkManager.Shutdown();
        statusMessage = "네트워크 연결 종료";

        Debug.Log("NetworkManager 종료");
    }

    private bool CanStartNetwork()
    {
        if (networkManager == null)
        {
            statusMessage = "NetworkManager가 없습니다.";
            Debug.LogError(statusMessage);
            return false;
        }

        if (networkManager.IsListening)
        {
            statusMessage = "이미 네트워크가 실행 중입니다.";
            Debug.LogWarning(statusMessage);
            return false;
        }

        return true;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            int connectedCount =
                networkManager.ConnectedClientsList.Count;

            statusMessage =
                $"플레이어 접속\n" +
                $"Client ID: {clientId}\n" +
                $"접속 인원: {connectedCount}";
        }
        else if (clientId == networkManager.LocalClientId)
        {
            statusMessage =
                $"Client 접속 완료\n" +
                $"Client ID: {clientId}";
        }

        Debug.Log($"Client 연결: {clientId}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client 연결 종료: {clientId}");

        if (networkManager == null || !networkManager.IsListening)
        {
            statusMessage = "네트워크 연결 종료";
            return;
        }

        if (networkManager.IsServer)
        {
            int connectedCount =
                networkManager.ConnectedClientsList.Count;

            statusMessage =
                $"플레이어 연결 종료\n" +
                $"Client ID: {clientId}\n" +
                $"현재 인원: {connectedCount}";
        }
        else
        {
            statusMessage = "서버와 연결이 종료되었습니다.";
        }
    }

    private string GetNetworkRole()
    {
        if (networkManager == null)
            return "NetworkManager 없음";

        if (networkManager.IsHost)
            return "Host";

        if (networkManager.IsServer)
            return "Server";

        if (networkManager.IsClient)
            return "Client";

        return "연결 대기";
    }

    /*
     * 임시 네트워크 테스트 UI입니다.
     * 정상 동작 확인 후 Canvas UI로 교체합니다.
     */
    private void OnGUI()
    {
        const float width = 420f;
        const float height = 390f;

        float x = (Screen.width - width) * 0.5f;
        float y = (Screen.height - height) * 0.5f;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fixedHeight = 52f
        };

        GUILayout.BeginArea(
            new Rect(x, y, width, height),
            GUI.skin.box
        );

        GUILayout.Label("NetworkLauncher", titleStyle);
        GUILayout.Space(15f);

        GUILayout.Label(
            $"현재 상태: {GetNetworkRole()}",
            labelStyle
        );

        GUILayout.Label(statusMessage, labelStyle);
        GUILayout.Space(20f);

        bool canStart =
            networkManager != null &&
            !networkManager.IsListening;

        GUI.enabled = canStart;

        if (GUILayout.Button("Host 시작", buttonStyle))
        {
            StartHost();
        }

        GUILayout.Space(10f);

        if (GUILayout.Button("Client 접속", buttonStyle))
        {
            StartClient();
        }

        GUILayout.Space(10f);

        if (GUILayout.Button("Dedicated Server 시작", buttonStyle))
        {
            StartServer();
        }

        GUI.enabled =
            networkManager != null &&
            networkManager.IsListening;

        GUILayout.Space(20f);

        if (GUILayout.Button("연결 종료", buttonStyle))
        {
            ShutdownNetwork();
        }

        GUI.enabled = true;

        GUILayout.EndArea();
    }
}