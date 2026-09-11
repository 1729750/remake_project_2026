using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client/UI 요청을 Server로 전달하는 중계 전용 클래스.
/// 상태 저장과 게임 판정은 다른 클래스가 담당한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkMatchState))]
public sealed class NetworkMatchBridge : NetworkBehaviour
{
    [Header("Network Mode")]
    [SerializeField]
    private NetworkModeGate networkModeGate;

    [Header("Server Logic")]
    [SerializeField]
    private MatchServerController serverController;

    [Header("Shared State")]
    [SerializeField]
    private NetworkMatchState matchState;

    public event Action<string> LocalMessage;

    // 기존 코드 호환용 프로퍼티.
    // 새 UI에서는 가능하면 NetworkMatchState를 직접 읽는 것을 권장한다.
    public bool IsMultiGameMode =>
        networkModeGate != null &&
        networkModeGate.NetworkEnabled;

    public MatchPhase CurrentPhase =>
        matchState != null
            ? matchState.CurrentPhase
            : MatchPhase.WaitingForPlayers;

    public ulong CurrentTurnClientId =>
        matchState != null
            ? matchState.CurrentTurnClientId
            : 0;

    public int SelectedCardCount =>
        matchState != null
            ? matchState.SelectedCardCount
            : 0;

    public event Action MatchStateChanged
    {
        add
        {
            if (matchState != null)
                matchState.MatchStateChanged += value;
        }
        remove
        {
            if (matchState != null)
                matchState.MatchStateChanged -= value;
        }
    }

    private void Awake()
    {
        if (matchState == null)
        {
            matchState =
                GetComponent<NetworkMatchState>();
        }

        if (serverController == null)
        {
            serverController =
                GetComponent<MatchServerController>();
        }
    }

    // =========================================================
    // Match Start
    // =========================================================

    public void RequestStartMatch()
    {
        if (!CanSendNetworkRequest())
            return;

        RequestStartMatchRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestStartMatchRpc(
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController.TryStartMatch(
                senderClientId,
                out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );
        }
    }

    // =========================================================
    // Card Selection
    // =========================================================

    public void RequestChooseCard(int cardId)
    {
        if (!CanSendNetworkRequest())
            return;

        RequestChooseCardRpc(cardId);
    }

    [Rpc(SendTo.Server)]
    private void RequestChooseCardRpc(
        int cardId,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController.TryChooseCard(
                senderClientId,
                cardId,
                out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );
        }
    }

    // =========================================================
    // Backward-compatible State Read
    // =========================================================

    public bool TryGetSelectedCard(
        ulong clientId,
        out int cardId)
    {
        if (matchState == null)
        {
            cardId = -1;
            return false;
        }

        return matchState.TryGetSelectedCard(
            clientId,
            out cardId
        );
    }

    // =========================================================
    // Validation
    // =========================================================

    private bool CanSendNetworkRequest()
    {
        if (networkModeGate == null)
        {
            LocalMessage?.Invoke(
                "NetworkModeGate가 연결되어 있지 않습니다."
            );

            return false;
        }

        if (!networkModeGate.NetworkEnabled)
        {
            LocalMessage?.Invoke(
                "네트워크 모드가 OFF 상태입니다."
            );

            return false;
        }

        if (!IsSpawned)
        {
            LocalMessage?.Invoke(
                "NetworkMatchBridge가 아직 Spawn되지 않았습니다."
            );

            return false;
        }

        return true;
    }

    // =========================================================
    // Reject Message
    // =========================================================

    private void SendRejectMessage(
        ulong targetClientId,
        string message)
    {
        if (!IsServer)
            return;

        RejectRequestRpc(
            targetClientId,
            new FixedString128Bytes(message)
        );
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RejectRequestRpc(
        ulong targetClientId,
        FixedString128Bytes message)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClientId !=
            targetClientId)
        {
            return;
        }

        LocalMessage?.Invoke(
            message.ToString()
        );
    }
}
