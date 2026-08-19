using TMPro;
using UnityEngine;

/// <summary>
/// 멀티플레이 로비 입력 화면 전용.
///
/// 담당 역할:
/// 1. Host / Client 선택
/// 2. 선택한 모드에 맞는 Panel 표시
/// 3. 닉네임 입력 UI 관리
/// 4. Join Code 입력 UI 관리
///
/// 입력값 검증이나 네트워크 요청은 담당하지 않는다.
/// </summary>
public sealed class LobbyEntryFormUI : MonoBehaviour
{
    public enum EntryMode
    {
        None,
        Host,
        Client
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("Panels")]
    [SerializeField]
    private GameObject modeSelectionPanel;

    [SerializeField]
    private GameObject hostPanel;

    [SerializeField]
    private GameObject clientPanel;

    [Header("Host Input")]
    [SerializeField]
    private TMP_InputField hostNicknameInput;

    [Header("Client Input")]
    [SerializeField]
    private TMP_InputField clientNicknameInput;

    [SerializeField]
    private TMP_InputField joinCodeInput;

    // =========================================================
    // State
    // =========================================================

    public EntryMode SelectedMode { get; private set; }
        = EntryMode.None;

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        ShowModeSelection();
    }

    // =========================================================
    // Mode Selection
    // =========================================================

    /// <summary>
    /// Host 버튼에서 호출.
    ///
    /// ModeSelectPanel을 비활성화하고
    /// HostPanel을 활성화한다.
    /// </summary>
    public void SelectHost()
    {
        SelectedMode = EntryMode.Host;

        ShowSelectedPanel(
            showHost: true,
            showClient: false
        );

        ClearHostInput();

        Debug.Log(
            "[LobbyEntryFormUI] Host 모드 선택"
        );
    }

    /// <summary>
    /// Client 버튼에서 호출.
    ///
    /// ModeSelectPanel을 비활성화하고
    /// ClientPanel을 활성화한다.
    /// </summary>
    public void SelectClient()
    {
        SelectedMode = EntryMode.Client;

        ShowSelectedPanel(
            showHost: false,
            showClient: true
        );

        ClearClientInput();

        Debug.Log(
            "[LobbyEntryFormUI] Client 모드 선택"
        );
    }

    /// <summary>
    /// Host / Client Panel에서 뒤로가기.
    ///
    /// 다시 ModeSelectPanel을 표시한다.
    /// </summary>
    public void Back()
    {
        SelectedMode = EntryMode.None;

        ShowModeSelection();

        Debug.Log(
            "[LobbyEntryFormUI] 모드 선택 화면으로 돌아감"
        );
    }

    // =========================================================
    // Input
    // =========================================================

    public string GetNickname()
    {
        TMP_InputField input = null;

        if (SelectedMode == EntryMode.Host)
        {
            input = hostNicknameInput;
        }
        else if (SelectedMode == EntryMode.Client)
        {
            input = clientNicknameInput;
        }

        return input != null
            ? input.text.Trim()
            : string.Empty;
    }

    public string GetJoinCode()
    {
        return joinCodeInput != null
            ? joinCodeInput.text
                .Trim()
                .ToUpperInvariant()
            : string.Empty;
    }

    // =========================================================
    // UI
    // =========================================================

    /// <summary>
    /// 최초 진입 또는 Back 시 호출.
    ///
    /// ModeSelectPanel만 활성화한다.
    /// </summary>
    private void ShowModeSelection()
    {
        if (modeSelectionPanel != null)
        {
            modeSelectionPanel.SetActive(true);
        }

        if (hostPanel != null)
        {
            hostPanel.SetActive(false);
        }

        if (clientPanel != null)
        {
            clientPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Host 또는 Client 선택이 완료된 뒤
    /// ModeSelectPanel을 숨기고 선택한 Panel을 표시한다.
    /// </summary>
    private void ShowSelectedPanel(
        bool showHost,
        bool showClient)
    {
        if (modeSelectionPanel != null)
        {
            modeSelectionPanel.SetActive(false);
        }

        if (hostPanel != null)
        {
            hostPanel.SetActive(showHost);
        }

        if (clientPanel != null)
        {
            clientPanel.SetActive(showClient);
        }
    }

    // =========================================================
    // Clear Input
    // =========================================================

    private void ClearHostInput()
    {
        if (hostNicknameInput != null)
        {
            hostNicknameInput.text =
                string.Empty;
        }
    }

    private void ClearClientInput()
    {
        if (clientNicknameInput != null)
        {
            clientNicknameInput.text =
                string.Empty;
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.text =
                string.Empty;
        }
    }
}