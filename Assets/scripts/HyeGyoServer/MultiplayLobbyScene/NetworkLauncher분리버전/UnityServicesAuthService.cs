using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

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
        RefreshReferences();
    }


    private void RefreshReferences()
    {
        modeGate =
            GetComponent<NetworkModeGate>();

        statusHub =
            GetComponent<NetworkStatusHub>();
    }


    private async void Start()
    {
        RefreshReferences();

        if (!ValidateReferences())
            return;

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


    public async Task EnsureReadyAsync()
    {
        // 호출할 때마다 다시 참조 확인
        RefreshReferences();

        if (!ValidateReferences())
        {
            throw new InvalidOperationException(
                "UnityServicesAuthService의 필수 컴포넌트가 없습니다."
            );
        }

        if (!modeGate.NetworkEnabled)
        {
            throw new InvalidOperationException(
                "네트워크 기능이 OFF 상태입니다."
            );
        }

        if (IsInitialized)
            return;

        initializationTask ??=
            InitializeAsync();

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


    private bool ValidateReferences()
    {
        if (modeGate == null)
        {
            Debug.LogError(
                "[UnityServicesAuthService] " +
                "같은 GameObject에서 NetworkModeGate를 찾지 못했습니다.",
                gameObject
            );

            return false;
        }

        if (statusHub == null)
        {
            Debug.LogError(
                "[UnityServicesAuthService] " +
                "같은 GameObject에서 NetworkStatusHub를 찾지 못했습니다.",
                gameObject
            );

            return false;
        }

        return true;
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

        Debug.Log(
            "[UnityServicesAuthService] UGS/Auth 준비 완료"
        );
    }
}