using System;
using UnityEngine;

/// <summary>
/// UI가 바라보는 얇은 Facade.
/// 실제 기능은 각 전용 컴포넌트에게 위임한다.
/// </summary>
[RequireComponent(typeof(NetworkModeGate))]
[RequireComponent(typeof(NetworkStatusHub))]
[RequireComponent(typeof(UnityServicesAuthService))]
[RequireComponent(typeof(NetworkSessionService))]
[RequireComponent(typeof(NetworkConnectionMonitor))]
public sealed class NetworkLauncher : MonoBehaviour
{
    private NetworkModeGate modeGate;
    private NetworkStatusHub statusHub;
    private UnityServicesAuthService services;
    private NetworkSessionService sessionService;

    public bool NetworkEnabled =>
        modeGate != null && modeGate.NetworkEnabled;

    public bool IsInitialized =>
        services != null && services.IsInitialized;

    public bool IsBusy =>
        sessionService != null && sessionService.IsBusy;

    public bool IsInSession =>
        sessionService != null && sessionService.IsInSession;

    public string JoinCode =>
        sessionService != null
            ? sessionService.JoinCode
            : string.Empty;

    public string StatusMessage =>
        statusHub != null
            ? statusHub.StatusMessage
            : string.Empty;

    public event Action<string> StatusChanged
    {
        add
        {
            EnsureReferences();
            statusHub.StatusChanged += value;
        }
        remove
        {
            EnsureReferences();
            statusHub.StatusChanged -= value;
        }
    }

    public event Action<string> SessionCreated
    {
        add
        {
            EnsureReferences();
            sessionService.SessionCreated += value;
        }
        remove
        {
            EnsureReferences();
            sessionService.SessionCreated -= value;
        }
    }

    public event Action SessionJoined
    {
        add
        {
            EnsureReferences();
            sessionService.SessionJoined += value;
        }
        remove
        {
            EnsureReferences();
            sessionService.SessionJoined -= value;
        }
    }

    public event Action SessionLeft
    {
        add
        {
            EnsureReferences();
            sessionService.SessionLeft += value;
        }
        remove
        {
            EnsureReferences();
            sessionService.SessionLeft -= value;
        }
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        modeGate ??= GetComponent<NetworkModeGate>();
        statusHub ??= GetComponent<NetworkStatusHub>();
        services ??= GetComponent<UnityServicesAuthService>();
        sessionService ??= GetComponent<NetworkSessionService>();
    }

    /// <summary>
    /// UI Toggle(bool)에서 바로 연결 가능.
    /// ON은 네트워크 사용 허용만 하며 방 생성/참가를 자동 실행하지 않는다.
    /// OFF 시 세션에 들어가 있다면 먼저 Leave한다.
    /// </summary>
    public async void SetNetworkEnabled(bool enabled)
    {
        if (enabled)
        {
            modeGate.SetNetworkEnabled(true);

            statusHub.SetStatus(
                "네트워크 ON\n방 생성 또는 참가 대기"
            );

            return;
        }

        if (sessionService.IsBusy)
        {
            statusHub.SetStatus(
                "네트워크 작업이 끝난 뒤 OFF로 변경하세요."
            );

            return;
        }

        if (sessionService.IsInSession)
        {
            bool left =
                await sessionService.LeaveSessionAsync();

            if (!left)
                return;
        }

        modeGate.SetNetworkEnabled(false);
        statusHub.SetStatus("네트워크 OFF");
    }

    /// <summary>
    /// 필요할 때 UGS/Auth만 미리 초기화한다.
    /// 방 생성/참가는 하지 않는다.
    /// </summary>
    public async void InitializeServices()
    {
        try
        {
            await services.EnsureReadyAsync();
        }
        catch (Exception exception)
        {
            statusHub.SetStatus(
                $"서비스 초기화 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
        }
    }

    /// <summary>Host 방 생성 Button용.</summary>
    public async void CreateSession()
    {
        await sessionService.CreateSessionAsync();
    }

    /// <summary>참가 코드로 Client 입장.</summary>
    public async void JoinSession(string joinCode)
    {
        await sessionService.JoinSessionAsync(joinCode);
    }

    /// <summary>현재 Session 나가기.</summary>
    public async void LeaveSession()
    {
        await sessionService.LeaveSessionAsync();
    }
}
