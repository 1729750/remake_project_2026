using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Unity Gaming Services 초기화 + 익명 로그인만 담당한다.
/// Session 생성/참가/Relay 연결은 담당하지 않는다.
/// </summary>
[RequireComponent(typeof(NetworkModeGate))]
[RequireComponent(typeof(NetworkStatusHub))]
public sealed class UnityServicesAuthService : MonoBehaviour
{
    private NetworkModeGate modeGate;
    private NetworkStatusHub statusHub;
    private Task initializationTask;

    public bool IsInitialized { get; private set; }

    public string PlayerId =>
        IsInitialized &&
        AuthenticationService.Instance.IsSignedIn
            ? AuthenticationService.Instance.PlayerId
            : string.Empty;

    private void Awake()
    {
        modeGate = GetComponent<NetworkModeGate>();
        statusHub = GetComponent<NetworkStatusHub>();
    }

    private async void Start()
    {
        if (!modeGate.InitializeServicesOnStart)
        {
            statusHub.SetStatus(
                modeGate.NetworkEnabled
                    ? "네트워크 ON\n방 생성 또는 참가 대기"
                    : "네트워크 OFF"
            );

            return;
        }

        try
        {
            await EnsureReadyAsync();
        }
        catch (Exception exception)
        {
            statusHub.SetStatus(
                $"서비스 초기화 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// UGS/Auth가 준비되어 있지 않다면 한 번만 초기화한다.
    /// 여러 곳에서 동시에 호출해도 같은 Task를 기다린다.
    /// </summary>
    public async Task EnsureReadyAsync()
    {
        if (!modeGate.NetworkEnabled)
        {
            throw new InvalidOperationException(
                "네트워크 기능이 OFF 상태입니다."
            );
        }

        if (IsInitialized)
            return;

        initializationTask ??= InitializeAsync();

        try
        {
            await initializationTask;
        }
        catch
        {
            initializationTask = null;
            throw;
        }
    }

    private async Task InitializeAsync()
    {
        statusHub.SetStatus(
            "Unity Services 초기화 중..."
        );

        if (UnityServices.State !=
            ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            statusHub.SetStatus(
                "익명 로그인 중..."
            );

            await AuthenticationService.Instance
                .SignInAnonymouslyAsync();
        }

        IsInitialized = true;

        statusHub.SetStatus(
            $"로그인 완료\n" +
            $"Player ID: {AuthenticationService.Instance.PlayerId}"
        );
    }
}
