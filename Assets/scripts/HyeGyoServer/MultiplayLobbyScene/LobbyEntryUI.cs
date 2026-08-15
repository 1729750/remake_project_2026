using TMPro;
using UnityEngine;

/// <summary>
/// 멀티플레이 로비 입장 UI 전용.
///
/// 담당 역할:
/// 1. Host / Client 선택
/// 2. 닉네임 입력
/// 3. 참가 코드 입력
/// 4. 입력값 검증
/// 5. 검증된 값을 HostGameManager에 전달
///
/// 네트워크 연결이나 RPC는 담당하지 않는다.
/// </summary>
public sealed class LobbyEntryUI : MonoBehaviour
{
    private const int MaxNicknameLength = 12;

    private enum EntryMode
    {
        None,
        Host,
        Client
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("Lobby Manager")]
    [SerializeField]
    private HostGameManager hostGameManager;

    [Header("Panels")]
    [SerializeField]
    private GameObject modeSelectionPanel;

    [SerializeField]
    private GameObject connectionInputPanel;

    [Header("Input Fields")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_InputField joinCodeInput;

    [Header("Texts")]
    [SerializeField]
    private TMP_Text modeTitleText;

    [SerializeField]
    private TMP_Text inputStatusText;

    // =========================================================
    // Local State
    // =========================================================

    private EntryMode selectedMode =
        EntryMode.None;

    // =========================================================
    // Unity Life Cycle
    // =========================================================

    private void Awake()
    {
        ShowModeSelection();
        ClearInputs();
    }

    // =========================================================
    // Host / Client Selection
    // =========================================================

    /// <summary>
    /// Host 버튼에서 호출.
    /// </summary>
    public void SelectHost()
    {
        if (IsLobbyBusy())
            return;

        selectedMode =
            EntryMode.Host;

        ShowConnectionInput();

        if (modeTitleText != null)
        {
            modeTitleText.text =
                "Host 방 만들기";
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.text =
                string.Empty;

            joinCodeInput.gameObject
                .SetActive(false);
        }

        SetStatus(
            "닉네임을 입력하세요."
        );
    }

    /// <summary>
    /// Client 버튼에서 호출.
    /// </summary>
    public void SelectClient()
    {
        if (IsLobbyBusy())
            return;

        selectedMode =
            EntryMode.Client;

        ShowConnectionInput();

        if (modeTitleText != null)
        {
            modeTitleText.text =
                "Client 방 참가";
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.text =
                string.Empty;

            joinCodeInput.gameObject
                .SetActive(true);
        }

        SetStatus(
            "닉네임과 참가 코드를 입력하세요."
        );
    }

    // =========================================================
    // Confirm
    // =========================================================

    /// <summary>
    /// 닉네임 / 참가 코드 입력 화면의
    /// 확인 버튼에서 호출.
    /// </summary>
    public void Confirm()
    {
        if (hostGameManager == null)
        {
            SetStatus(
                "HostGameManager가 연결되어 있지 않습니다."
            );

            Debug.LogError(
                "[LobbyEntryUI] HostGameManager가 없습니다."
            );

            return;
        }

        if (hostGameManager.IsNetworkBusy)
        {
            SetStatus(
                "현재 네트워크 작업을 처리 중입니다."
            );

            return;
        }

        if (hostGameManager.IsInSession)
        {
            SetStatus(
                "이미 참가 중인 방이 있습니다."
            );

            return;
        }

        if (selectedMode ==
            EntryMode.None)
        {
            SetStatus(
                "Host 또는 Client를 먼저 선택하세요."
            );

            return;
        }

        if (!TryReadNickname(
                out string nickname))
        {
            return;
        }

        // -----------------------------------------------------
        // Host
        // -----------------------------------------------------

        if (selectedMode ==
            EntryMode.Host)
        {
            SetStatus(
                "방 생성을 요청합니다..."
            );

            hostGameManager
                .RequestCreateRoom(
                    nickname
                );

            return;
        }

        // -----------------------------------------------------
        // Client
        // -----------------------------------------------------

        if (!TryReadJoinCode(
                out string joinCode))
        {
            return;
        }

        SetStatus(
            "방 참가를 요청합니다..."
        );

        hostGameManager
            .RequestJoinRoom(
                nickname,
                joinCode
            );
    }

    // =========================================================
    // Back
    // =========================================================

    /// <summary>
    /// 뒤로가기 버튼에서 호출.
    /// </summary>
    public void Back()
    {
        if (IsLobbyBusy())
            return;

        if (hostGameManager != null &&
            hostGameManager.IsInSession)
        {
            SetStatus(
                "먼저 현재 방에서 나가주세요."
            );

            return;
        }

        selectedMode =
            EntryMode.None;

        ClearInputs();
        ShowModeSelection();

        SetStatus(
            string.Empty
        );
    }

    // =========================================================
    // Validation
    // =========================================================

    private bool TryReadNickname(
        out string nickname)
    {
        nickname =
            nicknameInput != null
                ? nicknameInput.text.Trim()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(
                nickname))
        {
            SetStatus(
                "닉네임을 입력하세요."
            );

            return false;
        }

        if (nickname.Length >
            MaxNicknameLength)
        {
            SetStatus(
                $"닉네임은 최대 " +
                $"{MaxNicknameLength}자까지 가능합니다."
            );

            return false;
        }

        return true;
    }

    private bool TryReadJoinCode(
        out string joinCode)
    {
        joinCode =
            joinCodeInput != null
                ? joinCodeInput.text
                    .Trim()
                    .ToUpperInvariant()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(
                joinCode))
        {
            SetStatus(
                "참가 코드를 입력하세요."
            );

            return false;
        }

        return true;
    }

    // =========================================================
    // UI
    // =========================================================

    private void ShowModeSelection()
    {
        if (modeSelectionPanel != null)
        {
            modeSelectionPanel
                .SetActive(true);
        }

        if (connectionInputPanel != null)
        {
            connectionInputPanel
                .SetActive(false);
        }
    }

    private void ShowConnectionInput()
    {
        if (modeSelectionPanel != null)
        {
            modeSelectionPanel
                .SetActive(false);
        }

        if (connectionInputPanel != null)
        {
            connectionInputPanel
                .SetActive(true);
        }
    }

    private void ClearInputs()
    {
        if (nicknameInput != null)
        {
            nicknameInput.text =
                string.Empty;
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.text =
                string.Empty;
        }
    }

    private bool IsLobbyBusy()
    {
        if (hostGameManager == null)
            return false;

        return hostGameManager.IsNetworkBusy;
    }

    private void SetStatus(
        string message)
    {
        if (inputStatusText != null)
        {
            inputStatusText.text =
                message;
        }

        if (!string.IsNullOrEmpty(message))
        {
            Debug.Log(
                $"[LobbyEntryUI] {message}"
            );
        }
    }
}