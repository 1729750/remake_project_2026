using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;


[RequireComponent(typeof(NetworkModeGate))]
[RequireComponent(typeof(NetworkStatusHub))]
public sealed class UnityServicesAuthService : MonoBehaviour
{
    private const string EnvironmentName =
        "production";


    private NetworkModeGate modeGate;
    private NetworkStatusHub statusHub;

    private Task initializationTask;


    public bool IsInitialized { get; private set; }


    public string PlayerId =>
        IsInitialized &&
        AuthenticationService.Instance.IsSignedIn
            ? AuthenticationService.Instance.PlayerId
            : string.Empty;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        RefreshReferences();
    }


    private async void Start()
    {
        RefreshReferences();


        if (!ValidateReferences())
        {
            return;
        }


        // Play 시작 시 자동 초기화를 사용하지 않는 경우
        // 실제 방 생성 / 참가 시 EnsureReadyAsync()가 호출된다.
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
                "서비스 초기화 실패\n" +
                exception.Message
            );


            Debug.LogException(
                exception
            );
        }
    }


    // =========================================================
    // References
    // =========================================================

    private void RefreshReferences()
    {
        modeGate ??=
            GetComponent<NetworkModeGate>();

        statusHub ??=
            GetComponent<NetworkStatusHub>();
    }


    private bool ValidateReferences()
    {
        if (modeGate == null)
        {
            Debug.LogError(
                "[UnityServicesAuthService] " +
                "같은 GameObject에서 " +
                "NetworkModeGate를 찾지 못했습니다.",
                gameObject
            );

            return false;
        }


        if (statusHub == null)
        {
            Debug.LogError(
                "[UnityServicesAuthService] " +
                "같은 GameObject에서 " +
                "NetworkStatusHub를 찾지 못했습니다.",
                gameObject
            );

            return false;
        }


        return true;
    }


    // =========================================================
    // Public API
    // =========================================================

    public async Task EnsureReadyAsync()
    {
        RefreshReferences();


        if (!ValidateReferences())
        {
            throw new InvalidOperationException(
                "UnityServicesAuthService의 " +
                "필수 컴포넌트가 없습니다."
            );
        }


        if (!modeGate.NetworkEnabled)
        {
            throw new InvalidOperationException(
                "네트워크 기능이 OFF 상태입니다."
            );
        }


        // 이미 이 서비스가 초기화 완료한 상태
        if (IsInitialized)
        {
            return;
        }


        // 여러 곳에서 동시에 요청해도
        // 초기화 작업은 한 번만 수행
        initializationTask ??=
            InitializeAsync();


        try
        {
            await initializationTask;
        }
        catch
        {
            // 실패했다면 다음 시도에서 다시 초기화 가능
            initializationTask = null;

            throw;
        }
    }


    // =========================================================
    // UGS / Authentication
    // =========================================================

    private async Task InitializeAsync()
    {
        statusHub.SetStatus(
            "Unity Services 초기화 중..."
        );


        // -----------------------------------------------------
        // Unity Gaming Services 초기화
        // -----------------------------------------------------

        if (UnityServices.State !=
            ServicesInitializationState.Initialized)
        {
            var options =
                new InitializationOptions()
                    .SetEnvironmentName(
                        EnvironmentName
                    );


            await UnityServices.InitializeAsync(
                options
            );


            Debug.Log(
                "[UnityServicesAuthService] " +
                "Unity Services 초기화 완료"
            );
        }
        else
        {
            Debug.LogWarning(
                "[UnityServicesAuthService] " +
                "Unity Services가 이미 초기화되어 있습니다. " +
                "현재 Play 실행 전에 다른 코드가 " +
                "UnityServices.InitializeAsync()를 호출했는지 " +
                "확인하세요."
            );
        }


        // -----------------------------------------------------
        // Project / Environment 확인 로그
        // -----------------------------------------------------

        Debug.Log(
            "[UGS CHECK] " +
            $"CloudProjectId: {Application.cloudProjectId} | " +
            $"Environment: {EnvironmentName}"
        );


        // -----------------------------------------------------
        // Authentication
        // -----------------------------------------------------

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            statusHub.SetStatus(
                "익명 로그인 중..."
            );


            await AuthenticationService.Instance
                .SignInAnonymouslyAsync();
        }


        // -----------------------------------------------------
        // Complete
        // -----------------------------------------------------

        IsInitialized = true;


        string playerId =
            AuthenticationService.Instance.PlayerId;


        statusHub.SetStatus(
            "로그인 완료\n" +
            $"Player ID: {playerId}"
        );


        Debug.Log(
            "[UnityServicesAuthService] " +
            "UGS/Auth 준비 완료 | " +
            $"CloudProjectId: {Application.cloudProjectId} | " +
            $"Environment: {EnvironmentName} | " +
            $"PlayerId: {playerId}"
        );
    }
}