using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 이름. 빌드 설정(Scenes In Build)에 둘 다 들어 있어야 한다.
/// </summary>
public static class OracleScenes
{
    public const string Lobby = "MultiplayLobby_Oracle";
    public const string Game = "MultiPlayMode_Oracle";
}

/// <summary>
/// MultiplayLobby_Oracle 씬의 "방 만들기" 패널. (MultiLobbyScene의 NetworkLobbyUIHost 자리)
/// 패널이 켜지면 바로 오라클 서버에 방을 만들고 코드를 보여준다. 상대가 들어오면 게임 씬으로 넘어간다.
/// </summary>
public sealed class OracleLobbyHostPanel : MonoBehaviour
{
    private const string DefaultNickname = "방장";

    [Header("Flow")]
    [SerializeField]
    private LobbyEntryFormUI lobbyForm;

    [Header("Host UI")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_Text codeOutput;

    [SerializeField]
    private TMP_Text currentPeopleText;

    [SerializeField]
    private Button copyButton;

    [Tooltip("Button 컴포넌트가 없으면 실행 중에 붙인다")]
    [SerializeField]
    private GameObject backButton;

    [SerializeField]
    private TMP_Text statusText;

    [Header("Waiting Dots")]
    [SerializeField]
    private GameObject waitingDot1;

    [SerializeField]
    private GameObject waitingDot2;

    [SerializeField]
    private GameObject waitingDot3;

    private OracleServerClient client;
    private Coroutine waitingDots;
    private bool creating;
    private bool loadingGame;

    private void Awake()
    {
        if (copyButton != null)
            copyButton.onClick.AddListener(CopyCode);

        if (backButton != null)
        {
            Button button = backButton.GetComponent<Button>();
            if (button == null) button = backButton.AddComponent<Button>();
            button.onClick.AddListener(Back);
        }

        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = 12;
            nicknameInput.onEndEdit.AddListener(_ => SendNickname());
        }
    }

    private void OnEnable()
    {
        client = OracleServerClient.GetOrCreate();
        client.MessageReceived += HandleMessage;
        client.Disconnected += HandleDisconnected;

        Refresh();

        if (!client.InRoom)
            CreateRoom();
    }

    private void OnDisable()
    {
        StopWaitingDots();

        if (client == null) return;
        client.MessageReceived -= HandleMessage;
        client.Disconnected -= HandleDisconnected;

        // 뒤로 나가면 방도 닫는다 (게임 씬으로 넘어갈 때는 유지)
        if (!loadingGame && client.InRoom)
            client.Send(ClientMessage.LeaveRoom());
    }

    // =========================================================
    // Actions
    // =========================================================

    private async void CreateRoom()
    {
        if (creating) return;
        creating = true;
        SetStatus("서버 연결 중...");

        bool connected = await client.ConnectAsync();

        // 기다리는 사이 패널이 꺼졌으면 그만둔다
        if (!isActiveAndEnabled)
        {
            creating = false;
            return;
        }

        if (!connected)
        {
            creating = false;
            SetStatus($"서버에 연결할 수 없습니다. 뒤로 갔다가 다시 시도하세요.\n{client.LastErrorSummary}");
            return;
        }

        SetStatus("방 만드는 중...");
        client.Send(ClientMessage.Hello(CurrentNickname()));
        client.Send(ClientMessage.CreateRoom());
    }

    private void SendNickname()
    {
        if (client != null && client.IsConnected)
            client.Send(ClientMessage.Hello(CurrentNickname()));
    }

    private string CurrentNickname()
    {
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        return nickname.Length > 0 ? nickname : DefaultNickname;
    }

    private void CopyCode()
    {
        if (client == null || !client.InRoom) return;
        GUIUtility.systemCopyBuffer = client.RoomCode;
        SetStatus("방 코드를 복사했습니다.");
    }

    private void Back()
    {
        // OnDisable에서 방을 나간다
        if (lobbyForm != null) lobbyForm.Back();
        else gameObject.SetActive(false);
    }

    // =========================================================
    // Server Messages
    // =========================================================

    private void HandleMessage(ServerMessage message)
    {
        switch (message.type)
        {
            case "room_joined":
                creating = false;
                SetStatus($"방 코드 {client.RoomCode} 를 상대에게 알려주세요.");
                Refresh();
                break;

            case "player_joined":
                // 상대가 들어오기 직전에 바꾼 닉네임도 반영되게 한 번 더 보낸다
                SendNickname();
                Refresh();
                break;

            case "player_left":
                SetStatus("상대가 나갔습니다.");
                Refresh();
                break;

            case "room_ready":
                loadingGame = true;
                SetStatus("상대 입장! 게임을 시작합니다.");
                Refresh();
                SceneManager.LoadScene(OracleScenes.Game);
                break;

            case "error":
                creating = false;
                SetStatus(message.message);
                break;
        }
    }

    private void HandleDisconnected()
    {
        creating = false;
        SetStatus($"서버와 연결이 끊어졌습니다.\n{client.LastErrorSummary}");
        Refresh();
    }

    // =========================================================
    // UI
    // =========================================================

    private void Refresh()
    {
        bool inRoom = client != null && client.InRoom;
        int count = inRoom ? client.RoomPlayers.Count : 0;

        if (codeOutput != null)
            codeOutput.text = inRoom ? client.RoomCode : string.Empty;

        if (currentPeopleText != null)
            currentPeopleText.text = inRoom ? $"{count}/2" : string.Empty;

        if (inRoom && count < 2)
            StartWaitingDots();
        else
            StopWaitingDots();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            statusText.text = message;
        }

        Debug.Log($"[OracleLobbyHostPanel] {message}");
    }

    private void StartWaitingDots()
    {
        if (waitingDots == null)
            waitingDots = StartCoroutine(WaitingDotsRoutine());
    }

    private void StopWaitingDots()
    {
        if (waitingDots != null)
        {
            StopCoroutine(waitingDots);
            waitingDots = null;
        }

        SetWaitingDots(0);
    }

    private IEnumerator WaitingDotsRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            for (int count = 0; count <= 3; count++)
            {
                SetWaitingDots(count);
                yield return wait;
            }
        }
    }

    private void SetWaitingDots(int count)
    {
        if (waitingDot1 != null) waitingDot1.SetActive(count >= 1);
        if (waitingDot2 != null) waitingDot2.SetActive(count >= 2);
        if (waitingDot3 != null) waitingDot3.SetActive(count >= 3);
    }
}
