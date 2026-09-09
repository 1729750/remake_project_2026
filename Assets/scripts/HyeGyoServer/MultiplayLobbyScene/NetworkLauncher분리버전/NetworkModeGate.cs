using System;
using UnityEngine;

/// <summary>
/// 게임 네트워크 기능 자체를 사용할지 결정하는 게이트.
/// UI Toggle에서 SetNetworkEnabled(bool)을 연결할 수 있다.
/// </summary>
public sealed class NetworkModeGate : MonoBehaviour
{
    [Header("Runtime Network")]
    [Tooltip("OFF면 Session/Relay 연결 요청을 막습니다.")]
    [SerializeField]
    private bool networkEnabled = false;

    [Tooltip(
        "Network Enabled가 ON일 때, Play 직후 UGS/Auth까지만 미리 초기화합니다.\n" +
        "방 생성/참가는 자동으로 하지 않습니다."
    )]
    [SerializeField]
    private bool initializeServicesOnStart = false;

    public bool NetworkEnabled => networkEnabled;

    public bool InitializeServicesOnStart =>
        networkEnabled && initializeServicesOnStart;

    public event Action<bool> NetworkEnabledChanged;

    public void SetNetworkEnabled(bool enabled)
    {
        if (networkEnabled == enabled)
            return;

        networkEnabled = enabled;
        NetworkEnabledChanged?.Invoke(enabled);
    }
}
