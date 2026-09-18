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


    // =========================================================
    // Events
    // =========================================================

    public event Action<string> LocalMessage;

    /// <summary>
    /// Server에서 생성한 강화 후보가
    /// 이 Local Client에게 도착했을 때 발생한다.
    /// Reward_Multi가 구독한다.
    /// </summary>
    public event Action<EnhanceOptionNetData[]>
        EnhanceCandidatesReceived;


    // =========================================================
    // State Read
    // =========================================================

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


    // =========================================================
    // Unity
    // =========================================================

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

        if (networkModeGate == null)
        {
            Debug.LogWarning(
                "[NetworkMatchBridge] " +
                "NetworkModeGate가 Inspector에 연결되어 있지 않습니다."
            );
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

    public void RequestChooseCard(
        int cardId)
    {
        if (!CanSendNetworkRequest())
            return;

        RequestChooseCardRpc(
            cardId
        );
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
    // Enhance Candidates
    // =========================================================

    /// <summary>
    /// Host / Client UI에서 강화 후보 3개를 요청한다.
    /// 실제 랜덤 생성은 Server가 수행한다.
    /// </summary>
    public void RequestEnhanceCandidates()
    {
        if (!CanSendNetworkRequest())
            return;

        Debug.Log(
            "[NetworkMatchBridge] " +
            "강화 후보 요청 전송"
        );

        RequestEnhanceCandidatesRpc();
    }


    /// <summary>
    /// Client → Server
    ///
    /// 실제 요청을 보낸 ClientId는
    /// 사용자가 직접 보내지 않고 NGO에서 가져온다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestEnhanceCandidatesRpc(
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        Debug.Log(
            "[NetworkMatchBridge][Server] " +
            $"강화 후보 요청 수신 | " +
            $"ClientId: {senderClientId}"
        );

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController
                .TryCreateEnhanceCandidates(
                    senderClientId,
                    out EnhanceOptionNetData[] options,
                    out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );

            return;
        }

        // 현재 1차 구현에서는 후보가 정확히 3개여야 한다.
        if (options == null ||
            options.Length != 3)
        {
            SendRejectMessage(
                senderClientId,
                "강화 후보 생성 개수가 올바르지 않습니다."
            );

            return;
        }

        // Server → ClientsAndHost
        //
        // 후보 데이터는 모두에게 RPC 자체는 전송되지만
        // targetClientId와 일치하는 Local Client만 처리한다.
        SendEnhanceCandidatesRpc(
            senderClientId,
            options[0],
            options[1],
            options[2]
        );
    }


    /// <summary>
    /// Server → 해당 Host/Client
    ///
    /// MatchServerController가 생성한 후보 3개를
    /// Reward_Multi 쪽으로 전달한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void SendEnhanceCandidatesRpc(
        ulong targetClientId,
        EnhanceOptionNetData option0,
        EnhanceOptionNetData option1,
        EnhanceOptionNetData option2)
    {
        if (NetworkManager.Singleton == null)
            return;

        // 자기에게 온 데이터가 아니면 무시
        if (NetworkManager.Singleton.LocalClientId !=
            targetClientId)
        {
            return;
        }

        EnhanceOptionNetData[] options =
        {
            option0,
            option1,
            option2
        };

        Debug.Log(
            "[NetworkMatchBridge] " +
            $"강화 후보 수신 완료 | " +
            $"LocalClientId: " +
            $"{NetworkManager.Singleton.LocalClientId}"
        );

        EnhanceCandidatesReceived?.Invoke(
            options
        );
    }


    // =========================================================
    // Enhance Confirm
    // =========================================================

    /// <summary>
    /// Reward_Multi에서 선택한 후보 index만 Server에 전달한다.
    ///
    /// CardUpgrade 자체를 보내지 않는다.
    /// </summary>
    public void RequestConfirmEnhanceCandidate(
        int selectedIndex)
    {
        if (!CanSendNetworkRequest())
            return;

        Debug.Log(
            "[NetworkMatchBridge] " +
            $"강화 후보 선택 전송 | " +
            $"Index: {selectedIndex}"
        );

        RequestConfirmEnhanceCandidateRpc(
            selectedIndex
        );
    }


    /// <summary>
    /// Client → Server
    ///
    /// Server가 이전에 해당 ClientId에게 발급해둔
    /// CardUpgrade[]에서 selectedIndex를 다시 찾아 검증한다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestConfirmEnhanceCandidateRpc(
        int selectedIndex,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        Debug.Log(
            "[NetworkMatchBridge][Server] " +
            $"강화 후보 선택 수신 | " +
            $"ClientId: {senderClientId} | " +
            $"Index: {selectedIndex}"
        );

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController
                .TryConfirmEnhanceCandidate(
                    senderClientId,
                    selectedIndex,
                    out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );

            return;
        }

        Debug.Log(
            "[NetworkMatchBridge][Server] " +
            $"강화 후보 선택 검증 완료 | " +
            $"ClientId: {senderClientId} | " +
            $"Index: {selectedIndex}"
        );
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

            Debug.LogWarning(
                "[NetworkMatchBridge] " +
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

        Debug.LogWarning(
            "[NetworkMatchBridge][Server] " +
            $"요청 거절 | " +
            $"ClientId: {targetClientId} | " +
            $"Reason: {message}"
        );

        RejectRequestRpc(
            targetClientId,
            new FixedString128Bytes(
                message
            )
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

        Debug.LogWarning(
            "[NetworkMatchBridge] " +
            $"Server 요청 거절 | " +
            $"{message}"
        );

        LocalMessage?.Invoke(
            message.ToString()
        );
    }
}