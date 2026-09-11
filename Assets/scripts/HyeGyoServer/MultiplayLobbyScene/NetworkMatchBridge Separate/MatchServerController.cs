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

        // Host만 게임 시작 가능
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

        // 첫 번째 턴은 Host
        matchState.ServerBeginCardSelection(
            NetworkManager.ServerClientId
        );

        Debug.Log(
            "[Server] 멀티 게임 시작 | " +
            $"첫 번째 턴 ClientId: " +
            $"{NetworkManager.ServerClientId}"
        );

        rejectReason = string.Empty;
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

        // 실제 게임에 등록된 플레이어인지 확인
        if (!IsRegisteredPlayer(
                senderClientId))
        {
            rejectReason =
                "등록되지 않은 플레이어입니다.";

            return false;
        }

        // 현재 카드 선택 단계인지 확인
        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCard)
        {
            rejectReason =
                "현재는 카드를 선택할 수 없습니다.";

            return false;
        }

        // 현재 턴 플레이어인지 확인
        if (matchState.CurrentTurnClientId !=
            senderClientId)
        {
            rejectReason =
                "현재 당신의 차례가 아닙니다.";

            return false;
        }

        // CardDatabase 연결 확인
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

        // 존재하는 카드인지 확인
        if (!cardDatabase.TryGetCard(
                cardId,
                out _))
        {
            rejectReason =
                "존재하지 않는 카드입니다.";

            return false;
        }

        // 이미 다른 플레이어가 선택한 카드인지 확인
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

    /// <summary>
    /// 모든 기본 검증이 끝난 카드 선택 요청을 실제 상태에 반영한다.
    /// 상태를 변경하기 전에 다음 플레이어 존재 여부까지 검증한다.
    /// </summary>
    private bool ApplyCardSelection(
        ulong clientId,
        int cardId,
        out string rejectReason)
    {
        bool completesSelection =
            matchState.SelectedCardCount + 1 >= 2;

        ulong nextClientId = ulong.MaxValue;

        // 이번 선택으로 카드 선택 단계가 끝나지 않는다면
        // 다음 턴을 받을 실제 등록 플레이어가 존재해야 한다.
        if (!completesSelection)
        {
            if (!TryGetOtherRegisteredClientId(
                    clientId,
                    out nextClientId))
            {
                rejectReason =
                    "상대 플레이어가 연결되어 있지 않습니다.";

                return false;
            }
        }

        // =====================================================
        // 여기부터 실제 상태 Commit
        // =====================================================

        matchState.ServerAddSelectedCard(
            clientId,
            cardId
        );

        Debug.Log(
            "[Server] 카드 선택 승인 | " +
            $"Client: {clientId} | " +
            $"CardId: {cardId}"
        );

        // 두 플레이어가 모두 카드를 선택했으면
        // 카드 선택 단계를 종료한다.
        if (completesSelection)
        {
            // 현재 턴 없음
            matchState.ServerSetTurn(
                ulong.MaxValue
            );

            matchState.ServerSetPhase(
                MatchPhase.ChoosingCondition
            );

            Debug.Log(
                "[Server] 카드 선택 완료 → " +
                "조건 선택 단계"
            );

            rejectReason = string.Empty;
            return true;
        }

        // 다음 플레이어에게 턴 전달
        matchState.ServerSetTurn(
            nextClientId
        );

        Debug.Log(
            "[Server] 카드 선택 턴 변경 | " +
            $"Next Client: {nextClientId}"
        );

        rejectReason = string.Empty;
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

        rejectReason = string.Empty;
        return true;
    }

    /// <summary>
    /// 해당 ClientId가 실제 게임 플레이어로 등록되어 있는지 확인한다.
    /// </summary>
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

    /// <summary>
    /// 현재 플레이어를 제외한 다른 연결 중인
    /// 실제 등록 플레이어의 ClientId를 찾는다.
    /// </summary>
    private bool TryGetOtherRegisteredClientId(
        ulong currentClientId,
        out ulong otherClientId)
    {
        if (NetworkManager.Singleton == null)
        {
            otherClientId = ulong.MaxValue;
            return false;
        }

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList)
        {
            ulong candidateId =
                client.ClientId;

            // 자기 자신 제외
            if (candidateId ==
                currentClientId)
            {
                continue;
            }

            // NGO에 연결되어 있기만 한 Client가 아니라
            // 실제 게임 플레이어로 등록된 Client만 허용
            if (!IsRegisteredPlayer(
                    candidateId))
            {
                continue;
            }

            otherClientId =
                candidateId;

            return true;
        }

        otherClientId = ulong.MaxValue;
        return false;
    }
}