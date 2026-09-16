using Unity.Netcode;
using UnityEngine;

public sealed class CardSelectionServerService : MonoBehaviour
{
    [SerializeField]
    private CardDatabase cardDatabase;

    [SerializeField]
    private NetworkMatchState matchState;

    [SerializeField]
    private PlayerPreparationRegistry preparationRegistry;


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

        // 현재 기존 턴 방식.
        // 추후 각자 동시 선택으로 변경할 예정.
        if (matchState.CurrentTurnClientId !=
            senderClientId)
        {
            rejectReason =
                "현재 당신의 차례가 아닙니다.";

            return false;
        }

        if (cardDatabase == null)
        {
            rejectReason =
                "CardDatabase가 연결되어 있지 않습니다.";

            return false;
        }

        if (!cardDatabase.TryGetCard(
                cardId,
                out CardDefinition selectedCard))
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

        if (!preparationRegistry.TryGetPlayer(
                senderClientId,
                out PlayerPreparationData player))
        {
            rejectReason =
                "플레이어 준비 데이터를 찾을 수 없습니다.";

            return false;
        }

        return ApplyCardSelection(
            player,
            cardId,
            selectedCard,
            out rejectReason
        );
    }


    private bool ApplyCardSelection(
        PlayerPreparationData player,
        int cardId,
        CardDefinition selectedCard,
        out string rejectReason)
    {
        ulong clientId =
            player.ClientId;

        bool completesSelection =
            matchState.SelectedCardCount + 1 >= 2;

        ulong nextClientId =
            ulong.MaxValue;


        if (!completesSelection)
        {
            if (!TryGetOtherPlayer(
                    clientId,
                    out nextClientId))
            {
                rejectReason =
                    "상대 플레이어가 연결되어 있지 않습니다.";

                return false;
            }
        }


        // PlayerPreparationData에도 결과 저장
        player.SelectedCardIndex =
            cardId;

        player.CardSelectionCompleted =
            true;

        player.FinalDeck.Add(
            selectedCard
        );


        // Network 공용 상태에도 확정 결과 저장
        matchState.ServerAddSelectedCard(
            clientId,
            cardId
        );


        Debug.Log(
            "[CardSelectionServerService] " +
            $"카드 선택 승인 | " +
            $"ClientId: {clientId} | " +
            $"CardId: {cardId}"
        );


        if (completesSelection)
        {
            matchState.ServerSetTurn(
                ulong.MaxValue
            );

            matchState.ServerSetPhase(
                MatchPhase.ChoosingCondition
            );

            Debug.Log(
                "[CardSelectionServerService] " +
                "카드 선택 완료 → 조건 선택"
            );

            rejectReason =
                string.Empty;

            return true;
        }


        matchState.ServerSetTurn(
            nextClientId
        );

        rejectReason =
            string.Empty;

        return true;
    }


    private bool IsRegisteredPlayer(
        ulong clientId)
    {
        return
            preparationRegistry != null &&
            preparationRegistry.ContainsPlayer(
                clientId
            );
    }


    private bool TryGetOtherPlayer(
        ulong currentClientId,
        out ulong otherClientId)
    {
        otherClientId =
            ulong.MaxValue;

        if (NetworkManager.Singleton == null)
            return false;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList)
        {
            ulong candidateId =
                client.ClientId;

            if (candidateId ==
                currentClientId)
            {
                continue;
            }

            if (!IsRegisteredPlayer(
                    candidateId))
            {
                continue;
            }

            otherClientId =
                candidateId;

            return true;
        }

        return false;
    }


    private bool ValidateServerState(
        out string rejectReason)
    {
        if (matchState == null)
        {
            rejectReason =
                "NetworkMatchState가 없습니다.";

            return false;
        }

        if (!matchState.IsSpawned ||
            !matchState.IsServer)
        {
            rejectReason =
                "Server에서 처리할 수 없는 상태입니다.";

            return false;
        }

        rejectReason =
            string.Empty;

        return true;
    }
}