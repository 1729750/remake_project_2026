using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server의 게임 요청 검증 / 판정 전용 클래스.
/// RPC와 상태 저장은 담당하지 않는다.
/// </summary>
public sealed class MatchServerController : MonoBehaviour
{
    [Header("Lobby")]
    [SerializeField]
    private HostGameManager hostGameManager;

    [Header("Data")]
    [SerializeField]
    private CardDatabase cardDatabase;

    [Header("Shared State")]
    [SerializeField]
    private NetworkMatchState matchState;

    private void Awake()
    {
        if (matchState == null)
        {
            matchState =
                GetComponent<NetworkMatchState>();
        }
    }

    // =========================================================
    // Match Start
    // =========================================================

    public bool TryStartMatch(
        ulong senderClientId,
        out string rejectReason)
    {
        if (!ValidateServerState(
                out rejectReason))
        {
            return false;
        }

        if (senderClientId !=
            NetworkManager.ServerClientId)
        {
            rejectReason =
                "Host만 게임을 시작할 수 있습니다.";

            return false;
        }

        if (hostGameManager == null)
        {
            rejectReason =
                "HostGameManager가 연결되어 있지 않습니다.";

            return false;
        }

        if (!hostGameManager.IsRoomReady)
        {
            rejectReason =
                "플레이어 2명이 모두 접속해야 합니다.";

            return false;
        }

        if (matchState.CurrentPhase !=
            MatchPhase.WaitingForPlayers)
        {
            rejectReason =
                "이미 게임이 진행 중입니다.";

            return false;
        }

        matchState.ServerBeginCardSelection(
            NetworkManager.ServerClientId
        );

        Debug.Log(
            "[Server] 멀티 게임 시작 | " +
            $"첫 번째 턴 ClientId: " +
            $"{NetworkManager.ServerClientId}"
        );

        rejectReason =
            string.Empty;

        return true;
    }

    // =========================================================
    // Card Selection
    // =========================================================

    public bool TryChooseCard(
        ulong senderClientId,
        int cardId,
        out string rejectReason)
    {
        if (!ValidateServerState(
                out rejectReason))
        {
            return false;
        }

        Debug.Log(
            "[Server] 카드 선택 요청 | " +
            $"Client: {senderClientId} | " +
            $"CardId: {cardId}"
        );

        if (!IsRegisteredPlayer(
                senderClientId))
        {
            rejectReason =
                "등록되지 않은 플레이어입니다.";

            return false;
        }

        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCard)
        {
            rejectReason =
                "현재는 카드를 선택할 수 없습니다.";

            return false;
        }

        if (matchState.CurrentTurnClientId !=
            senderClientId)
        {
            rejectReason =
                "현재 당신의 차례가 아닙니다.";

            return false;
        }

        if (cardDatabase == null)
        {
            Debug.LogError(
                "[MatchServerController] " +
                "CardDatabase가 Inspector에 연결되어 있지 않습니다."
            );

            rejectReason =
                "카드 데이터베이스 오류입니다.";

            return false;
        }

        if (!cardDatabase.TryGetCard(
                cardId,
                out _))
        {
            rejectReason =
                "존재하지 않는 카드입니다.";

            return false;
        }

        if (matchState.IsCardAlreadySelected(
                cardId))
        {
            rejectReason =
                "이미 선택된 카드입니다.";

            return false;
        }

        return ApplyCardSelection(
            senderClientId,
            cardId,
            out rejectReason
        );
    }

    private bool ApplyCardSelection(
        ulong clientId,
        int cardId,
        out string rejectReason)
    {
        matchState.ServerAddSelectedCard(
            clientId,
            cardId
        );

        Debug.Log(
            "[Server] 카드 선택 승인 | " +
            $"Client: {clientId} | " +
            $"CardId: {cardId}"
        );

        if (matchState.SelectedCardCount >= 2)
        {
            matchState.ServerSetPhase(
                MatchPhase.ChoosingCondition
            );

            Debug.Log(
                "[Server] 카드 선택 완료 → " +
                "조건 선택 단계"
            );

            rejectReason =
                string.Empty;

            return true;
        }

        if (!TryGetOtherClientId(
                clientId,
                out ulong nextClientId))
        {
            matchState.ServerResetToWaiting();

            rejectReason =
                "상대 플레이어가 연결되어 있지 않습니다.";

            return false;
        }

        matchState.ServerSetTurn(
            nextClientId
        );

        Debug.Log(
            "[Server] 카드 선택 턴 변경 | " +
            $"Next Client: {nextClientId}"
        );

        rejectReason =
            string.Empty;

        return true;
    }

    // =========================================================
    // Validation Helpers
    // =========================================================

    private bool ValidateServerState(
        out string rejectReason)
    {
        if (matchState == null)
        {
            rejectReason =
                "NetworkMatchState가 연결되어 있지 않습니다.";

            return false;
        }

        if (!matchState.IsSpawned)
        {
            rejectReason =
                "NetworkMatchState가 아직 Spawn되지 않았습니다.";

            return false;
        }

        if (!matchState.IsServer)
        {
            rejectReason =
                "Server에서만 처리할 수 있는 요청입니다.";

            return false;
        }

        rejectReason =
            string.Empty;

        return true;
    }

    private bool IsRegisteredPlayer(
        ulong clientId)
    {
        if (hostGameManager == null)
            return false;

        return hostGameManager.TryGetNickname(
            clientId,
            out _
        );
    }

    private bool TryGetOtherClientId(
        ulong currentClientId,
        out ulong otherClientId)
    {
        if (NetworkManager.Singleton == null)
        {
            otherClientId = default;
            return false;
        }

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList)
        {
            if (client.ClientId ==
                currentClientId)
            {
                continue;
            }

            otherClientId =
                client.ClientId;

            return true;
        }

        otherClientId = default;
        return false;
    }
}
