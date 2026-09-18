using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Multiplayer Session의 생성 / 참가 / 나가기만 담당한다.
///
/// UnityServices/Auth 초기화:
///     UnityServicesAuthService 담당
///
/// Session / Relay:
///     이 클래스 담당
///
/// WithRelayNetwork()를 사용하므로
/// Session 생성 / 참가 과정에서 Relay 네트워크가 함께 구성된다.
/// </summary>
[RequireComponent(typeof(NetworkModeGate))]
[RequireComponent(typeof(NetworkStatusHub))]
[RequireComponent(typeof(UnityServicesAuthService))]
public sealed class NetworkSessionService : MonoBehaviour
{
    [Header("Session")]
    [Min(2)]
    [SerializeField]
    private int maxPlayers = 2;

    [SerializeField]
    private string sessionName = "HyeGyo Match";


    private NetworkModeGate modeGate;
    private NetworkStatusHub statusHub;
    private UnityServicesAuthService services;

    private ISession currentSession;


    // =========================================================
    // Public State
    // =========================================================

    public bool IsBusy { get; private set; }

    public bool IsInSession =>
        currentSession != null;

    public int MaxPlayers =>
        maxPlayers;

    public string JoinCode =>
        currentSession != null
            ? currentSession.Code
            : string.Empty;

    public ISession CurrentSession =>
        currentSession;


    // =========================================================
    // Events
    // =========================================================

    public event Action<string> SessionCreated;

    public event Action SessionJoined;

    public event Action SessionLeft;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        EnsureReferences();
    }


    private void EnsureReferences()
    {
        modeGate ??=
            GetComponent<NetworkModeGate>();

        statusHub ??=
            GetComponent<NetworkStatusHub>();

        services ??=
            GetComponent<UnityServicesAuthService>();
    }


    // =========================================================
    // Create Session
    // =========================================================

    public async Task<bool> CreateSessionAsync()
    {
        if (!CanBeginSessionOperation())
        {
            return false;
        }


        IsBusy = true;


        try
        {
            // -------------------------------------------------
            // UGS / Authentication 준비
            // -------------------------------------------------

            await services.EnsureReadyAsync();


            Debug.Log(
                "[UGS CHECK] HOST | " +
                $"CloudProjectId: {Application.cloudProjectId} | " +
                $"PlayerId: {services.PlayerId}"
            );


            // -------------------------------------------------
            // Session 생성
            // -------------------------------------------------

            statusHub.SetStatus(
                "Relay 세션 생성 중..."
            );


            var options =
                new SessionOptions
                {
                    MaxPlayers = maxPlayers,
                    Name = sessionName
                }
                .WithRelayNetwork();


            ISession createdSession =
                await MultiplayerService.Instance
                    .CreateSessionAsync(
                        options
                    );


            if (createdSession == null)
            {
                statusHub.SetStatus(
                    "방 생성 실패\n" +
                    "생성된 Session이 null입니다."
                );

                Debug.LogError(
                    "[NetworkSessionService] " +
                    "CreateSessionAsync 결과가 null입니다."
                );

                return false;
            }


            // 성공한 경우에만 저장
            currentSession =
                createdSession;


            // -------------------------------------------------
            // Success
            // -------------------------------------------------

            statusHub.SetStatus(
                "방 생성 완료\n" +
                $"참가 코드: {currentSession.Code}"
            );


            Debug.Log(
                "[NetworkSessionService] " +
                "방 생성 완료 | " +
                $"Session ID: {currentSession.Id} | " +
                $"Join Code: {currentSession.Code} | " +
                $"CloudProjectId: {Application.cloudProjectId}"
            );


            RaiseSessionCreated(
                currentSession.Code
            );


            return true;
        }
        catch (Exception exception)
        {
            currentSession = null;


            statusHub.SetStatus(
                "방 생성 실패\n" +
                exception.Message
            );


            Debug.LogError(
                "[NetworkSessionService] " +
                "방 생성 실패"
            );

            Debug.LogException(
                exception
            );


            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }


    // =========================================================
    // Join Session
    // =========================================================

    public async Task<bool> JoinSessionAsync(
        string joinCode)
    {
        if (!CanBeginSessionOperation())
        {
            return false;
        }


        string normalizedCode =
            joinCode?
                .Trim()
                .ToUpperInvariant();


        if (string.IsNullOrWhiteSpace(
                normalizedCode))
        {
            statusHub.SetStatus(
                "참가 코드를 입력하세요."
            );

            return false;
        }


        IsBusy = true;


        try
        {
            // -------------------------------------------------
            // UGS / Authentication 준비
            // -------------------------------------------------

            await services.EnsureReadyAsync();


            Debug.Log(
                "[UGS CHECK] CLIENT | " +
                $"CloudProjectId: {Application.cloudProjectId} | " +
                $"PlayerId: {services.PlayerId} | " +
                $"Requested JoinCode: {normalizedCode}"
            );


            // -------------------------------------------------
            // Session 참가
            // -------------------------------------------------

            statusHub.SetStatus(
                "Relay 세션 참가 중..."
            );


            ISession joinedSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(
                        normalizedCode
                    );


            if (joinedSession == null)
            {
                statusHub.SetStatus(
                    "방 참가 실패\n" +
                    "참가한 Session이 null입니다."
                );

                Debug.LogError(
                    "[NetworkSessionService] " +
                    "JoinSessionByCodeAsync 결과가 null입니다."
                );

                return false;
            }


            // 성공한 경우에만 저장
            currentSession =
                joinedSession;


            // -------------------------------------------------
            // Success
            // -------------------------------------------------

            statusHub.SetStatus(
                "방 참가 완료\n" +
                $"참가 코드: {currentSession.Code}"
            );


            Debug.Log(
                "[NetworkSessionService] " +
                "Relay 세션 참가 완료 | " +
                $"Session ID: {currentSession.Id} | " +
                $"Join Code: {currentSession.Code} | " +
                $"Requested Code: {normalizedCode} | " +
                $"CloudProjectId: {Application.cloudProjectId}"
            );


            SessionJoined?.Invoke();


            return true;
        }
        catch (Exception exception)
        {
            currentSession = null;


            statusHub.SetStatus(
                "방 참가 실패\n" +
                exception.Message
            );


            Debug.LogError(
                "[NetworkSessionService] " +
                "방 참가 실패 | " +
                $"Requested JoinCode: {normalizedCode} | " +
                $"CloudProjectId: {Application.cloudProjectId} | " +
                $"Exception: {exception.GetType().Name} | " +
                $"Message: {exception.Message}"
            );


            Debug.LogException(
                exception
            );


            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }


    // =========================================================
    // Leave Session
    // =========================================================

    public async Task<bool> LeaveSessionAsync()
    {
        EnsureReferences();


        if (statusHub == null)
        {
            Debug.LogError(
                "[NetworkSessionService] " +
                "NetworkStatusHub를 찾을 수 없습니다.",
                this
            );

            return false;
        }


        if (IsBusy)
        {
            statusHub.SetStatus(
                "현재 다른 네트워크 작업을 처리 중입니다."
            );

            return false;
        }


        if (currentSession == null)
        {
            statusHub.SetStatus(
                "현재 참가 중인 세션이 없습니다."
            );

            return false;
        }


        IsBusy = true;


        try
        {
            string leavingSessionId =
                currentSession.Id;

            string leavingJoinCode =
                currentSession.Code;


            statusHub.SetStatus(
                "세션에서 나가는 중..."
            );


            await currentSession.LeaveAsync();


            currentSession = null;


            statusHub.SetStatus(
                "세션에서 나왔습니다."
            );


            Debug.Log(
                "[NetworkSessionService] " +
                "세션 나가기 완료 | " +
                $"Session ID: {leavingSessionId} | " +
                $"Join Code: {leavingJoinCode}"
            );


            SessionLeft?.Invoke();


            return true;
        }
        catch (Exception exception)
        {
            statusHub.SetStatus(
                "세션 나가기 실패\n" +
                exception.Message
            );


            Debug.LogException(
                exception
            );


            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }


    // =========================================================
    // Validation
    // =========================================================

    private bool CanBeginSessionOperation()
    {
        EnsureReferences();


        if (modeGate == null)
        {
            Debug.LogError(
                "[NetworkSessionService] " +
                "NetworkModeGate를 찾을 수 없습니다.",
                this
            );

            return false;
        }


        if (statusHub == null)
        {
            Debug.LogError(
                "[NetworkSessionService] " +
                "NetworkStatusHub를 찾을 수 없습니다.",
                this
            );

            return false;
        }


        if (services == null)
        {
            Debug.LogError(
                "[NetworkSessionService] " +
                "UnityServicesAuthService를 찾을 수 없습니다.",
                this
            );

            return false;
        }


        if (!modeGate.NetworkEnabled)
        {
            statusHub.SetStatus(
                "네트워크 기능이 OFF 상태입니다."
            );


            Debug.LogWarning(
                "[NetworkSessionService] " +
                "Session 요청 차단 | " +
                "NetworkEnabled: false"
            );


            return false;
        }


        if (IsBusy)
        {
            statusHub.SetStatus(
                "현재 다른 네트워크 작업을 처리 중입니다."
            );

            return false;
        }


        if (currentSession != null)
        {
            statusHub.SetStatus(
                "이미 참가 중인 세션이 있습니다."
            );


            Debug.LogWarning(
                "[NetworkSessionService] " +
                "이미 Session 참가 중 | " +
                $"Session ID: {currentSession.Id} | " +
                $"Join Code: {currentSession.Code}"
            );


            return false;
        }


        return true;
    }


    // =========================================================
    // Events
    // =========================================================

    private void RaiseSessionCreated(
        string joinCode)
    {
        if (SessionCreated == null)
        {
            return;
        }


        foreach (
            Action<string> handler
            in SessionCreated.GetInvocationList())
        {
            try
            {
                handler(
                    joinCode
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    exception
                );
            }
        }
    }
}