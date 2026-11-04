using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MultiplayLobby_Oracle 씬의 "방 참가" 패널. (MultiLobbyScene의 NetworkLobbyUIClient 자리)
/// 참가 버튼 → JoinSession(), 뒤로 버튼 → BackButton() 은 씬의 Button에 연결돼 있다.
/// </summary>
public sealed class OracleLobbyClientPanel : MonoBehaviour
{
    private const string DefaultNickname = "도전자";

    [Header("Panel")]
    [SerializeField]
    private GameObject selectionPanel;

    [Header("Input")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_InputField codeInput;

    [SerializeField]
    private TMP_Text statusText;

    private OracleServerClient client;
    private bool joining;
    private bool loadingGame;

    private void Awake()
    {
        if (nicknameInput != null)
            nicknameInput.characterLimit = 12;

        if (codeInput != null)
        {
            codeInput.characterLimit = 6;
            codeInput.onValidateInput += ValidateCodeCharacter;
        }
    }

    private void OnEnable()
    {
        client = OracleServerClient.GetOrCreate();
        client.MessageReceived += HandleMessage;
        client.Disconnected += HandleDisconnected;
        SetStatus(string.Empty);
    }

    private void OnDisable()
    {
        if (client == null) return;
        client.MessageReceived -= HandleMessage;
        client.Disconnected -= HandleDisconnected;

        if (!loadingGame && client.InRoom)
            client.Send(ClientMessage.LeaveRoom());
    }

    // =========================================================
    // Buttons
    // =========================================================

    public async void JoinSession()
    {
        if (joining || client.InRoom) return;

        string code = codeInput != null ? codeInput.text.Trim().ToUpperInvariant() : string.Empty;
        if (code.Length == 0)
        {
            SetStatus("방 코드를 입력하세요.");
            return;
        }

        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        if (nickname.Length == 0) nickname = DefaultNickname;

        joining = true;
        SetStatus("서버 연결 중...");

        bool connected = await client.ConnectAsync();
        if (!isActiveAndEnabled)
        {
            joining = false;
            return;
        }

        if (!connected)
        {
            joining = false;
            SetStatus("서버에 연결할 수 없습니다.");
            return;
        }

        SetStatus("방 참가 중...");
        client.Send(ClientMessage.Hello(nickname));
        client.Send(ClientMessage.JoinRoom(code));
    }

    public void BackButton()
    {
        if (selectionPanel != null)
            selectionPanel.SetActive(true);

        gameObject.SetActive(false);
    }

    // =========================================================
    // Server Messages
    // =========================================================

    private void HandleMessage(ServerMessage message)
    {
        switch (message.type)
        {
            case "room_joined":
                joining = false;
                SetStatus("입장했습니다. 곧 게임이 시작됩니다.");
                break;

            case "room_ready":
                loadingGame = true;
                SceneManager.LoadScene(OracleScenes.Game);
                break;

            case "error":
                joining = false;
                SetStatus(message.message);
                break;
        }
    }

    private void HandleDisconnected()
    {
        joining = false;
        SetStatus("서버와 연결이 끊어졌습니다.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            statusText.text = message;
        }

        if (!string.IsNullOrEmpty(message))
            Debug.Log($"[OracleLobbyClientPanel] {message}");
    }

    private static char ValidateCodeCharacter(string text, int charIndex, char addedChar)
    {
        bool isEnglish = (addedChar >= 'A' && addedChar <= 'Z') || (addedChar >= 'a' && addedChar <= 'z');
        bool isNumber = addedChar >= '0' && addedChar <= '9';
        return isEnglish || isNumber ? char.ToUpperInvariant(addedChar) : '\0';
    }
}
