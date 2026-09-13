using TMPro;
using UnityEngine;

public sealed class NetworkLobbyUIHost : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private NetworkLauncher networkLauncher;

    [Header("Host UI")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_InputField codeOutput;

    private bool hostStartRequested;


    private void Awake()
    {
        // 닉네임은 영어만 입력
        if (nicknameInput != null)
        {
            nicknameInput.onValidateInput +=
                ValidateNicknameCharacter;
        }

        // Host의 Code 칸은 입력하는 곳이 아니라
        // 자동 생성된 Join Code를 보여주는 곳
        if (codeOutput != null)
        {
            codeOutput.readOnly = true;
            codeOutput.text = string.Empty;
        }
    }


    private void OnEnable()
    {
        if (networkLauncher == null)
        {
            Debug.LogError(
                "[NetworkLobbyUIHost] NetworkLauncher가 연결되지 않았습니다."
            );

            return;
        }

        // 방 생성 완료 이벤트 먼저 구독
        networkLauncher.SessionCreated +=
            HandleSessionCreated;

        // HostPanel이 켜지는 순간 서버 생성
        StartHostAutomatically();
    }


    private void OnDisable()
    {
        if (networkLauncher != null)
        {
            networkLauncher.SessionCreated -=
                HandleSessionCreated;
        }
    }


    private void StartHostAutomatically()
    {
        // OnEnable이 중복 호출되어
        // 방을 여러 번 만드는 것 방지
        if (hostStartRequested)
            return;

        // 이미 방에 들어가 있다면 생성하지 않음
        if (networkLauncher.IsInSession)
            return;

        // 이미 네트워크 작업 중이면 생성하지 않음
        if (networkLauncher.IsBusy)
            return;

        hostStartRequested = true;

        if (codeOutput != null)
        {
            codeOutput.text = "Creating...";
        }

        // 네트워크가 OFF라면 먼저 ON
        if (!networkLauncher.NetworkEnabled)
        {
            networkLauncher.SetNetworkEnabled(true);
        }

        // Relay Session + NGO Host 생성
        networkLauncher.CreateSession();
    }


    private void HandleSessionCreated(string joinCode)
    {
        if (codeOutput != null)
        {
            codeOutput.text =
                joinCode.ToUpperInvariant();
        }

        Debug.Log(
            $"[Host] 방 생성 완료 | Join Code: {joinCode}"
        );
    }


    private char ValidateNicknameCharacter(
        string text,
        int charIndex,
        char addedChar)
    {
        bool isEnglish =
            (addedChar >= 'A' &&
             addedChar <= 'Z') ||
            (addedChar >= 'a' &&
             addedChar <= 'z');

        return isEnglish
            ? addedChar
            : '\0';
    }
}