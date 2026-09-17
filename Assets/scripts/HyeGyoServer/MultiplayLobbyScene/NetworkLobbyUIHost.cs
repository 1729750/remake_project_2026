using System.Collections;
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

    // Host 방 코드는 입력하는 곳이 아니라 표시만 하는 Text
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

    private Coroutine waitingDotsCoroutine;


    public string Nickname =>
        nicknameInput != null
            ? nicknameInput.text.Trim()
            : string.Empty;


    private void Awake()
    {
        // 닉네임
        // 한글 IME 입력을 위해 onValidateInput은 사용하지 않는다.
        if (nicknameInput != null)
        {
            nicknameInput.contentType =
                TMP_InputField.ContentType.Standard;
        }

        // 처음에는 방 코드 비우기
        if (codeOutput != null)
        {
            codeOutput.text = string.Empty;
        }

        // 서버 생성 전에는 현재 인원 표시 안 함
        if (currentPeopleText != null)
        {
            currentPeopleText.text = string.Empty;
        }

        // 도트 전부 OFF
        SetWaitingDots(
            false,
            false,
            false
        );
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

        // 방 생성 완료 이벤트 구독
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

        // Panel이 꺼지면 대기 애니메이션도 중지
        StopWaitingAnimation();
    }


    private void StartHostAutomatically()
    {
        // OnEnable 중복 호출 방지
        if (hostStartRequested)
        {
            return;
        }

        // 이미 방에 들어가 있다면 생성하지 않음
        if (networkLauncher.IsInSession)
        {
            return;
        }

        // 이미 네트워크 작업 중이면 생성하지 않음
        if (networkLauncher.IsBusy)
        {
            return;
        }

        hostStartRequested = true;


        // 서버 생성 중에는 방 코드 비우기
        if (codeOutput != null)
        {
            codeOutput.text = string.Empty;
        }

        // 서버 생성 중에는 인원 표시 비우기
        if (currentPeopleText != null)
        {
            currentPeopleText.text = string.Empty;
        }

        // 대기 도트 시작
        StartWaitingAnimation();


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
        // 서버 생성 완료
        StopWaitingAnimation();

        // Join Code 표시
        if (codeOutput != null)
        {
            codeOutput.text =
                joinCode.ToUpperInvariant();
        }

        // Host 자신이 있으므로 1 / 2
        if (currentPeopleText != null)
        {
            currentPeopleText.text = "1/2";
        }

        Debug.Log(
            $"[Host] 방 생성 완료 | Join Code: {joinCode}"
        );
    }


    // ==================================================
    // Waiting Dot Animation
    // ==================================================

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

            waitingDotsCoroutine = null;
        }

        // 애니메이션 종료 시 전부 OFF
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
            // 처음에는 전부 OFF
            SetWaitingDots(
                false,
                false,
                false
            );

            yield return new WaitForSeconds(1f);


            // ●
            SetWaitingDots(
                true,
                false,
                false
            );

            yield return new WaitForSeconds(1f);


            // ● ●
            SetWaitingDots(
                true,
                true,
                false
            );

            yield return new WaitForSeconds(1f);


            // ● ● ●
            SetWaitingDots(
                true,
                true,
                true
            );

            yield return new WaitForSeconds(1f);

            // 이후 while 처음으로 돌아가면서
            // 다시 전부 OFF
        }
    }


    private void SetWaitingDots(
        bool dot1,
        bool dot2,
        bool dot3)
    {
        if (waitingDot1 != null)
        {
            waitingDot1.SetActive(dot1);
        }

        if (waitingDot2 != null)
        {
            waitingDot2.SetActive(dot2);
        }

        if (waitingDot3 != null)
        {
            waitingDot3.SetActive(dot3);
        }
    }


    // ==================================================
    // Nickname
    // ==================================================

    public bool TryGetNickname(
        out string nickname)
    {
        nickname = Nickname;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogWarning(
                "[Host UI] 닉네임을 입력하세요."
            );

            return false;
        }

        if (!IsValidNickname(nickname))
        {
            Debug.LogWarning(
                "[Host UI] 닉네임은 한글과 영어만 사용할 수 있습니다."
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
}