using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host / Client가 공유해야 하는 매치 상태 전용 클래스.
/// NetworkVariable / NetworkList의 보관과 동기화를 담당한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkMatchState : NetworkBehaviour
{
    /// <summary>
    /// 현재 유효한 ClientId가 없음을 나타내는 값.
    /// 새 카드 선택 방식에서는 턴을 사용하지 않는다.
    /// </summary>
    public const ulong NoClientId =
        ulong.MaxValue;


    private readonly NetworkVariable<MatchPhase>
        matchPhase =
            new NetworkVariable<MatchPhase>(
                MatchPhase.WaitingForPlayers,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );


    // 기존 코드 호환을 위해 아직 유지.
    // 새 카드 선택에서는 사용하지 않을 예정.
    private readonly NetworkVariable<ulong>
        currentTurnClientId =
            new NetworkVariable<ulong>(
                NoClientId,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );


    private NetworkList<CardSelectionData>
        selectedCards;


    public event Action MatchStateChanged;


    public MatchPhase CurrentPhase =>
        matchPhase.Value;


    public ulong CurrentTurnClientId =>
        currentTurnClientId.Value;


    public int SelectedCardCount =>
        selectedCards != null
            ? selectedCards.Count
            : 0;


    private void Awake()
    {
        selectedCards =
            new NetworkList<CardSelectionData>();
    }


    public override void OnNetworkSpawn()
    {
        matchPhase.OnValueChanged +=
            HandleMatchPhaseChanged;

        currentTurnClientId.OnValueChanged +=
            HandleTurnChanged;

        selectedCards.OnListChanged +=
            HandleSelectedCardsChanged;

        MatchStateChanged?.Invoke();
    }


    public override void OnNetworkDespawn()
    {
        matchPhase.OnValueChanged -=
            HandleMatchPhaseChanged;

        currentTurnClientId.OnValueChanged -=
            HandleTurnChanged;

        if (selectedCards != null)
        {
            selectedCards.OnListChanged -=
                HandleSelectedCardsChanged;
        }
    }


    // =========================================================
    // Server Write API
    // =========================================================

    public void ServerResetToWaiting()
    {
        if (!IsServer)
        {
            return;
        }

        // 먼저 상태 변경
        matchPhase.Value =
            MatchPhase.WaitingForPlayers;

        currentTurnClientId.Value =
            NoClientId;

        if (selectedCards != null &&
            selectedCards.Count > 0)
        {
            selectedCards.Clear();
        }
    }


    /// <summary>
    /// 새로운 멀티 방식.
    /// 두 플레이어가 준비되면 동시에 카드 선택 단계로 진입한다.
    /// </summary>
    public void ServerBeginCardSelection()
    {
        if (!IsServer)
        {
            return;
        }

        // 중복 실행 방지
        if (matchPhase.Value ==
            MatchPhase.ChoosingCard)
        {
            return;
        }

        // 중요:
        // NetworkList.Clear()보다 Phase를 먼저 변경한다.
        // 이벤트 재진입 시 다시 이 함수를 실행하지 않게 하기 위함.
        matchPhase.Value =
            MatchPhase.ChoosingCard;

        currentTurnClientId.Value =
            NoClientId;

        // 비어 있는 리스트를 Clear하지 않는다.
        if (selectedCards != null &&
            selectedCards.Count > 0)
        {
            selectedCards.Clear();
        }

        Debug.Log(
            "[NetworkMatchState] " +
            "카드 선택 단계 시작"
        );
    }


    /// <summary>
    /// 기존 MatchServerController 호환용.
    /// 새 방식에서는 firstTurnClientId를 사용하지 않는다.
    /// </summary>
    public void ServerBeginCardSelection(
        ulong firstTurnClientId)
    {
        ServerBeginCardSelection();
    }


    public void ServerAddSelectedCard(
        ulong clientId,
        int cardId)
    {
        if (!IsServer)
        {
            return;
        }

        selectedCards.Add(
            new CardSelectionData(
                clientId,
                cardId
            )
        );
    }


    /// <summary>
    /// 기존 코드 호환용.
    /// 새 카드 선택 방식에서는 사용하지 않을 예정.
    /// </summary>
    public void ServerSetTurn(
        ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        currentTurnClientId.Value =
            clientId;
    }


    public void ServerSetPhase(
        MatchPhase phase)
    {
        if (!IsServer)
        {
            return;
        }

        matchPhase.Value =
            phase;
    }


    // =========================================================
    // Read API
    // =========================================================

    public bool IsCardAlreadySelected(
        int cardId)
    {
        if (selectedCards == null)
        {
            return false;
        }

        for (int i = 0;
             i < selectedCards.Count;
             i++)
        {
            if (selectedCards[i].CardId ==
                cardId)
            {
                return true;
            }
        }

        return false;
    }


    public bool TryGetSelectedCard(
        ulong clientId,
        out int cardId)
    {
        if (selectedCards == null)
        {
            cardId = -1;

            return false;
        }

        for (int i = 0;
             i < selectedCards.Count;
             i++)
        {
            CardSelectionData data =
                selectedCards[i];

            if (data.ClientId ==
                clientId)
            {
                cardId =
                    data.CardId;

                return true;
            }
        }

        cardId = -1;

        return false;
    }


    // =========================================================
    // State Events
    // =========================================================

    private void HandleMatchPhaseChanged(
        MatchPhase previous,
        MatchPhase current)
    {
        Debug.Log(
            "[NetworkMatchState] " +
            $"MatchPhase: {previous} → {current}"
        );

        MatchStateChanged?.Invoke();
    }


    private void HandleTurnChanged(
        ulong previous,
        ulong current)
    {
        Debug.Log(
            "[NetworkMatchState] " +
            $"Turn: {previous} → {current}"
        );

        MatchStateChanged?.Invoke();
    }


    private void HandleSelectedCardsChanged(
        NetworkListEvent<CardSelectionData>
            changeEvent)
    {
        Debug.Log(
            "[NetworkMatchState] " +
            $"SelectedCards 변경 | " +
            $"Count: {selectedCards.Count}"
        );

        MatchStateChanged?.Invoke();
    }
}