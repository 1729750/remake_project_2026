using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Multiplayer Session의 생성 / 참가 / 나가기만 담당한다.
/// WithRelayNetwork()를 사용하므로 Session 작업 과정에서
/// Relay + NGO Host/Client 연결이 함께 처리된다.
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

    public bool IsBusy { get; private set; }
    public bool IsInSession => currentSession != null;
    public int MaxPlayers => maxPlayers;

    public string JoinCode =>
        currentSession != null
            ? currentSession.Code
            : string.Empty;

    public ISession CurrentSession => currentSession;

    public event Action<string> SessionCreated;
    public event Action SessionJoined;
    public event Action SessionLeft;

    private void Awake()
    {
        modeGate = GetComponent<NetworkModeGate>();
        statusHub = GetComponent<NetworkStatusHub>();
        services = GetComponent<UnityServicesAuthService>();
    }

public async Task<bool> CreateSessionAsync()
{
    if (!CanBeginSessionOperation())
        return false;

    IsBusy = true;

    ISession createdSession;

    try
    {
        await services.EnsureReadyAsync();

        statusHub.SetStatus("Relay 세션 생성 중...");

        var options = new SessionOptions
        {
            MaxPlayers = maxPlayers,
            Name = sessionName
        }.WithRelayNetwork();

        createdSession =
            await MultiplayerService.Instance
                .CreateSessionAsync(options);
    }
    catch (Exception exception)
    {
        statusHub.SetStatus(
            $"방 생성 실패\n{exception.Message}"
        );

        Debug.LogException(exception);
        return false;
    }
    finally
    {
        IsBusy = false;
    }

    // 네트워크 작업 성공이 확정된 뒤 상태 반영
    currentSession = createdSession;

    statusHub.SetStatus(
        $"방 생성 완료\n참가 코드: {currentSession.Code}"
    );

    RaiseSessionCreated(currentSession.Code);

    return true;
}
    public async Task<bool> JoinSessionAsync(string joinCode)
    {
        if (!CanBeginSessionOperation())
            return false;

        string normalizedCode =
            joinCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            statusHub.SetStatus("참가 코드를 입력하세요.");
            return false;
        }

        IsBusy = true;

        try
        {
            await services.EnsureReadyAsync();

            statusHub.SetStatus("Relay 세션 참가 중...");

            currentSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(normalizedCode);

            statusHub.SetStatus(
                $"방 참가 완료\n" +
                $"참가 코드: {currentSession.Code}"
            );

            SessionJoined?.Invoke();

            Debug.Log(
                $"Relay 세션 참가 완료 | " +
                $"Session ID: {currentSession.Id} | " +
                $"Join Code: {currentSession.Code}"
            );

            return true;
        }
        catch (Exception exception)
        {
            currentSession = null;

            statusHub.SetStatus(
                $"방 참가 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> LeaveSessionAsync()
    {
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
            statusHub.SetStatus("세션에서 나가는 중...");

            await currentSession.LeaveAsync();
            currentSession = null;

            statusHub.SetStatus("세션에서 나왔습니다.");
            SessionLeft?.Invoke();

            return true;
        }
        catch (Exception exception)
        {
            statusHub.SetStatus(
                $"세션 나가기 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBeginSessionOperation()
    {
        if (!modeGate.NetworkEnabled)
        {
            statusHub.SetStatus("네트워크 기능이 OFF 상태입니다.");
            return false;
        }

        if (IsBusy)
        {
            statusHub.SetStatus("현재 다른 네트워크 작업을 처리 중입니다.");
            return false;
        }

        if (currentSession != null)
        {
            statusHub.SetStatus("이미 참가 중인 세션이 있습니다.");
            return false;
        }

        return true;
    }

    private void RaiseSessionCreated(string joinCode)
{
    if (SessionCreated == null)
        return;

    foreach (Action<string> handler
             in SessionCreated.GetInvocationList())
    {
        try
        {
            handler(joinCode);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
}
