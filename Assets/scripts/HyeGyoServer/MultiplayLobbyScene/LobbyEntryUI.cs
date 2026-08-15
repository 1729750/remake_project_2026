using TMPro;
using UnityEngine;

/// <summary>
/// 멀티플레이 로비 입력 처리.
///
/// 담당 역할:
/// 1. 입력값 검증
/// 2. 검증된 값을 HostGameManager에 전달
/// </summary>
public sealed class LobbyEntryUI : MonoBehaviour
{
    private const int MaxNicknameLength = 12;

    // =========================================================
    // Inspector
    // =========================================================

    [Header("References")]
    [SerializeField]
    private LobbyEntryFormUI formUI;

    [SerializeField]
    private HostGameManager hostGameManager;

    [Header("Status")]
    [SerializeField]
    private TMP_Text inputStatusText;

    // =========================================================
    // Confirm
    // =========================================================

    public void Confirm()
    {
        if (!CanRequest())
            return;

        if (!TryReadNickname(
                out string nickname))
        {
            return;
        }

        // -----------------------------------------------------
        // Host
        // -----------------------------------------------------

        if (formUI.SelectedMode ==
            LobbyEntryFormUI.EntryMode.Host)
        {
            SetStatus(
                "방 생성을 요청합니다..."
            );

            hostGameManager.RequestCreateRoom(
                nickname
            );

            return;
        }

        // -----------------------------------------------------
        // Client
        // -----------------------------------------------------

        if (formUI.SelectedMode ==
            LobbyEntryFormUI.EntryMode.Client)
        {
            if (!TryReadJoinCode(
                    out string joinCode))
            {
                return;
            }

            SetStatus(
                "방 참가를 요청합니다..."
            );

            hostGameManager.RequestJoinRoom(
                nickname,
                joinCode
            );

            return;
        }

        SetStatus(
            "Host 또는 Client를 먼저 선택하세요."
        );
    }

    // =========================================================
    // Request Validation
    // =========================================================

    private bool CanRequest()
    {
        if (formUI == null)
        {
            Debug.LogError(
                "[LobbyEntryUI] LobbyEntryFormUI가 없습니다."
            );

            return false;
        }

        if (hostGameManager == null)
        {
            SetStatus(
                "HostGameManager가 연결되어 있지 않습니다."
            );

            return false;
        }

        if (hostGameManager.IsNetworkBusy)
        {
            SetStatus(
                "현재 네트워크 작업을 처리 중입니다."
            );

            return false;
        }

        if (hostGameManager.IsInSession)
        {
            SetStatus(
                "이미 참가 중인 방이 있습니다."
            );

            return false;
        }

        return true;
    }

    // =========================================================
    // Input Validation
    // =========================================================

    private bool TryReadNickname(
        out string nickname)
    {
        nickname = formUI.GetNickname();

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
                $"닉네임은 최대 {MaxNicknameLength}자까지 가능합니다."
            );

            return false;
        }

        return true;
    }

    private bool TryReadJoinCode(
        out string joinCode)
    {
        joinCode = formUI.GetJoinCode();

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
    // Status
    // =========================================================

    private void SetStatus(
        string message)
    {
        if (inputStatusText != null)
        {
            inputStatusText.text =
                message;
        }

        Debug.Log(
            $"[LobbyEntryUI] {message}"
        );
    }
}