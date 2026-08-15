using TMPro;
using UnityEngine;

/// <summary>
/// 멀티플레이 로비 입력 화면 전용.
///
/// 담당 역할:
/// 1. Host / Client 선택
/// 2. 닉네임 입력 UI 관리
/// 3. Join Code 입력 UI 관리
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

    public void SelectHost()
    {
        SelectedMode = EntryMode.Host;

        modeSelectionPanel.SetActive(false);
        hostPanel.SetActive(true);
        clientPanel.SetActive(false);

        ClearHostInput();
    }

    public void SelectClient()
    {
        SelectedMode = EntryMode.Client;

        modeSelectionPanel.SetActive(false);
        hostPanel.SetActive(false);
        clientPanel.SetActive(true);

        ClearClientInput();
    }

    public void Back()
    {
        SelectedMode = EntryMode.None;

        ShowModeSelection();
    }

    // =========================================================
    // Input
    // =========================================================

    public string GetNickname()
    {
        TMP_InputField input =
            SelectedMode == EntryMode.Host
                ? hostNicknameInput
                : clientNicknameInput;

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

    private void ShowModeSelection()
    {
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(true);

        if (hostPanel != null)
            hostPanel.SetActive(false);

        if (clientPanel != null)
            clientPanel.SetActive(false);
    }

    private void ClearHostInput()
    {
        if (hostNicknameInput != null)
            hostNicknameInput.text = string.Empty;
    }

    private void ClearClientInput()
    {
        if (clientNicknameInput != null)
            clientNicknameInput.text = string.Empty;

        if (joinCodeInput != null)
            joinCodeInput.text = string.Empty;
    }
}