using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;


public sealed class NetworkLobbyUIHost : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private NetworkLauncher networkLauncher;

    [Header("Connection")]
    [SerializeField]
    private NetworkConnectionMonitor connectionMonitor;


    [Header("Lobby State")]
    [SerializeField]
    private HostGameManager hostGameManager;

    [SerializeField]
    private MatchServerController matchServerController;


    [Header("Flow")]
    [SerializeField]
    private bool autoStartWhenReady = true;


    [Header("Host UI")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_Text codeOutput;

    [SerializeField]
    private TMP_Text currentPeopleText;


    [Header("Waiting Dots")]
    [SerializeField]
    private GameObject waitingDot1;

    [SerializeField]
    private GameObject waitingDot2;

    [SerializeField]
    private GameObject waitingDot3;


    private bool hostStartRequested;
    private bool gameStartRequested;

    private Coroutine waitingDotsCoroutine;


    public string Nickname =>
        nicknameInput != null
            ? nicknameInput.text.Trim()
            : string.Empty;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (nicknameInput != null)
        {
            nicknameInput.contentType =
                TMP_InputField.ContentType.Standard;
        }


        if (codeOutput != null)
        {
            codeOutput.text =
                string.Empty;
        }


        if (currentPeopleText != null)
        {
            currentPeopleText.text =
                string.Empty;
        }


        SetWaitingDots(
            false,
            false,
            false
        );
    }


    private void OnEnable()
    {
        Debug.Log(
            "[NetworkLobbyUIHost] OnEnable 실행"
        );


        if (networkLauncher == null)
        {
            Debug.LogError(
                "[NetworkLobbyUIHost] " +
                "NetworkLauncher가 연결되지 않았습니다."
            );

            return;
        }


        networkLauncher.SessionCreated +=
            HandleSessionCreated;


        if (hostGameManager != null)
        {
            hostGameManager.LobbyPlayersChanged +=
                HandleLobbyPlayersChanged;


            Debug.Log(
                "[NetworkLobbyUIHost] " +
                "HostGameManager.LobbyPlayersChanged 구독 완료 | " +
                $"InstanceId: {hostGameManager.GetInstanceID()}"
            );
        }
        else
        {
            Debug.LogError(
                "[NetworkLobbyUIHost] " +
                "HostGameManager가 연결되지 않았습니다."
            );
        }

        if (connectionMonitor != null)
{
    connectionMonitor.PlayerCountChanged +=
        HandlePlayerCountChanged;
}
else
{
    Debug.LogError(
        "[NetworkLobbyUIHost] " +
        "NetworkConnectionMonitor가 연결되지 않았습니다."
    );
}


        RefreshCurrentPeople();

        StartHostAutomatically();
    }


    private void OnDisable()
    {

            Debug.LogWarning(
        "[NetworkLobbyUIHost] OnDisable 실행 | " +
        $"InstanceId: {GetInstanceID()}"
    );


        if (networkLauncher != null)
        {
            networkLauncher.SessionCreated -=
                HandleSessionCreated;
        }


        if (hostGameManager != null)
        {
            hostGameManager.LobbyPlayersChanged -=
                HandleLobbyPlayersChanged;
        }
            if (connectionMonitor != null)
        {
            connectionMonitor.PlayerCountChanged -=
                HandlePlayerCountChanged;
        }


        StopWaitingAnimation();
    }


    // =========================================================
    // Host Start
    // =========================================================

    private void StartHostAutomatically()
    {
        if (hostStartRequested)
        {
            return;
        }


        if (networkLauncher.IsInSession)
        {
            return;
        }


        if (networkLauncher.IsBusy)
        {
            return;
        }


        hostStartRequested = true;


        if (codeOutput != null)
        {
            codeOutput.text =
                string.Empty;
        }


        if (currentPeopleText != null)
        {
            currentPeopleText.text =
                string.Empty;
        }


        StartWaitingAnimation();


        if (!networkLauncher.NetworkEnabled)
        {
            networkLauncher.SetNetworkEnabled(
                true
            );
        }


        networkLauncher.CreateSession();
    }


    private void HandleSessionCreated(
        string joinCode)
    {
        StopWaitingAnimation();


        if (codeOutput != null)
        {
            codeOutput.text =
                joinCode.ToUpperInvariant();
        }


        RefreshCurrentPeople();


        Debug.Log(
            "[NetworkLobbyUIHost] " +
            $"방 생성 완료 | Join Code: {joinCode}"
        );
    }


    // =========================================================
    // Lobby Player Event
    // =========================================================

    private void HandleLobbyPlayersChanged()
    {
        if (hostGameManager == null)
        {
            Debug.LogError(
                "[NetworkLobbyUIHost] " +
                "LobbyPlayersChanged 수신했지만 " +
                "HostGameManager가 null입니다."
            );

            return;
        }


        Debug.Log(
            "[NetworkLobbyUIHost] " +
            "LobbyPlayersChanged 이벤트 수신 | " +
            $"Count: {hostGameManager.ConnectedPlayerCount} | " +
            $"InstanceId: {hostGameManager.GetInstanceID()}"
        );


        RefreshCurrentPeople();


        if (!autoStartWhenReady)
        {
            Debug.Log(
                "[NetworkLobbyUIHost] " +
                "AutoStartWhenReady OFF"
            );

            return;
        }


        if (gameStartRequested)
        {
            return;
        }


        if (!hostGameManager.IsRoomReady)
        {
            Debug.Log(
                "[NetworkLobbyUIHost] " +
                "아직 Room Ready 아님 | " +
                $"Count: {hostGameManager.ConnectedPlayerCount}"
            );

            return;
        }


        Debug.Log(
            "[NetworkLobbyUIHost] " +
            "2/2 확인 → TryStartGame 호출"
        );


        TryStartGame();
    }

private void RefreshCurrentPeople()
{
    if (currentPeopleText == null)
    {
        return;
    }


    if (connectionMonitor == null)
    {
        currentPeopleText.text =
            string.Empty;

        return;
    }


    int count =
        connectionMonitor.ConnectedPlayerCount;


    currentPeopleText.text =
        $"{count}/2";


    Debug.Log(
        "[NetworkLobbyUIHost] " +
        $"인원 UI 갱신 → {count}/2"
    );
}


    // =========================================================
    // Game Start
    // =========================================================

    private void TryStartGame()
    {
        Debug.Log(
            "[NetworkLobbyUIHost] " +
            "TryStartGame 호출됨"
        );


        if (matchServerController == null)
        {
            Debug.LogError(
                "[NetworkLobbyUIHost] " +
                "MatchServerController가 연결되지 않았습니다."
            );

            return;
        }


        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "[NetworkLobbyUIHost] " +
                "NetworkManager가 없습니다."
            );

            return;
        }


        Debug.Log(
            "[NetworkLobbyUIHost] " +
            $"IsHost: {NetworkManager.Singleton.IsHost} | " +
            $"IsServer: {NetworkManager.Singleton.IsServer} | " +
            $"Players: {hostGameManager.ConnectedPlayerCount}"
        );


        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning(
                "[NetworkLobbyUIHost] " +
                "Host가 아니므로 게임 시작을 요청하지 않습니다."
            );

            return;
        }


        gameStartRequested = true;


        bool success =
            matchServerController.TryStartMatch(
                NetworkManager.ServerClientId,
                out string rejectReason
            );


        if (!success)
        {
            gameStartRequested = false;


            Debug.LogWarning(
                "[NetworkLobbyUIHost] " +
                $"게임 시작 실패 | {rejectReason}"
            );

            return;
        }


        Debug.Log(
            "[NetworkLobbyUIHost] " +
            "게임 시작 요청 성공"
        );
    }


    // =========================================================
    // Waiting Dot Animation
    // =========================================================

    private void StartWaitingAnimation()
    {
        StopWaitingAnimation();


        waitingDotsCoroutine =
            StartCoroutine(
                WaitingDotsRoutine()
            );
    }


    private void StopWaitingAnimation()
    {
        if (waitingDotsCoroutine != null)
        {
            StopCoroutine(
                waitingDotsCoroutine
            );


            waitingDotsCoroutine =
                null;
        }


        SetWaitingDots(
            false,
            false,
            false
        );
    }


    private IEnumerator WaitingDotsRoutine()
    {
        while (true)
        {
            SetWaitingDots(
                false,
                false,
                false
            );

            yield return new WaitForSeconds(
                1f
            );


            SetWaitingDots(
                true,
                false,
                false
            );

            yield return new WaitForSeconds(
                1f
            );


            SetWaitingDots(
                true,
                true,
                false
            );

            yield return new WaitForSeconds(
                1f
            );


            SetWaitingDots(
                true,
                true,
                true
            );

            yield return new WaitForSeconds(
                1f
            );
        }
    }


    private void SetWaitingDots(
        bool dot1,
        bool dot2,
        bool dot3)
    {
        if (waitingDot1 != null)
        {
            waitingDot1.SetActive(
                dot1
            );
        }


        if (waitingDot2 != null)
        {
            waitingDot2.SetActive(
                dot2
            );
        }


        if (waitingDot3 != null)
        {
            waitingDot3.SetActive(
                dot3
            );
        }
    }


    // =========================================================
    // Nickname
    // =========================================================

    public bool TryGetNickname(
        out string nickname)
    {
        nickname =
            Nickname;


        if (string.IsNullOrWhiteSpace(
                nickname))
        {
            Debug.LogWarning(
                "[Host UI] " +
                "닉네임을 입력하세요."
            );

            return false;
        }


        if (!IsValidNickname(
                nickname))
        {
            Debug.LogWarning(
                "[Host UI] " +
                "닉네임은 한글과 영어만 사용할 수 있습니다."
            );

            return false;
        }


        return true;
    }


    private bool IsValidNickname(
        string nickname)
    {
        foreach (char c in nickname)
        {
            bool isEnglish =
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z');


            bool isHangul =
                (c >= '\uAC00' &&
                 c <= '\uD7A3') ||

                (c >= '\u3131' &&
                 c <= '\u318E') ||

                (c >= '\u1100' &&
                 c <= '\u11FF');


            if (!isEnglish &&
                !isHangul)
            {
                return false;
            }
        }


        return true;
    }

        private void HandlePlayerCountChanged(
        int playerCount)
    {
        Debug.Log(
            "[NetworkLobbyUIHost] " +
            $"실제 NGO 인원 변경 → {playerCount}/2"
        );


        if (currentPeopleText != null)
        {
            currentPeopleText.text =
                $"{playerCount}/2";
        }


        if (!autoStartWhenReady)
        {   
            return;
        }


        if (gameStartRequested)
        {
            return;
        }


        if (playerCount < 2)
        {   
            return;
        }


        Debug.Log(
            "[NetworkLobbyUIHost] " +
            "실제 NGO 2/2 확인 → TryStartGame"
        );


        TryStartGame();
    }
}