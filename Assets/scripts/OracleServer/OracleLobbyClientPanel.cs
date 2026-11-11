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
    private string sentCode = string.Empty;

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

        // 한글 입력기 전각 모드(５Ｍ５ＧＷＲ)나 공백이 섞여도 서버 코드 형식으로 맞춘다
        string code = codeInput != null ? NormalizeCode(codeInput.text) : string.Empty;
        if (codeInput != null) codeInput.text = code;
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
            SetStatus($"서버에 연결할 수 없습니다.\n{client.LastErrorSummary}");
            return;
        }

        sentCode = code;
        SetStatus($"방 참가 중... ({code})");
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
                // 코드를 잘못 옮겨 적었는지 바로 알 수 있게 보낸 코드를 같이 보여준다
                SetStatus(sentCode.Length > 0 ? $"{message.message}\n(보낸 코드: {sentCode})" : message.message);
                break;
        }
    }

    private void HandleDisconnected()
    {
        joining = false;
        SetStatus($"서버와 연결이 끊어졌습니다.\n{client.LastErrorSummary}");
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
        char c = ToHalfWidth(addedChar);
        return IsCodeChar(c) ? char.ToUpperInvariant(c) : '\0';
    }

    /// <summary>전각 영문/숫자를 반각으로 바꾸고 영문/숫자만 남겨 대문자로.</summary>
    public static string NormalizeCode(string raw)
    {
        var builder = new System.Text.StringBuilder(raw.Length);
        foreach (char ch in raw)
        {
            char c = ToHalfWidth(ch);
            if (IsCodeChar(c)) builder.Append(char.ToUpperInvariant(c));
        }
        return builder.ToString();
    }

    // 전각 ！(U+FF01) ~ ～(U+FF5E) → 반각
    private static char ToHalfWidth(char c) => c >= '！' && c <= '～' ? (char)(c - 0xFEE0) : c;

    private static bool IsCodeChar(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
}
