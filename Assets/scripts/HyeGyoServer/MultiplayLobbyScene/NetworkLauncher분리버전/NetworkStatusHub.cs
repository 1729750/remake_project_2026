using System;
using UnityEngine;

/// <summary>
/// 네트워크 관련 상태 문자열을 한 곳에서 관리한다.
/// UI는 이 클래스의 StatusChanged 이벤트만 구독하면 된다.
/// </summary>
public sealed class NetworkStatusHub : MonoBehaviour
{
    public string StatusMessage { get; private set; } =
        "네트워크 OFF";

    public event Action<string> StatusChanged;

    public void SetStatus(string message)
    {
        StatusMessage = message ?? string.Empty;
        StatusChanged?.Invoke(StatusMessage);
    }
}
