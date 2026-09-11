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
    private readonly NetworkVariable<MatchPhase>
        matchPhase =
            new NetworkVariable<MatchPhase>(
                MatchPhase.WaitingForPlayers,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

    private readonly NetworkVariable<ulong>
        currentTurnClientId =
            new NetworkVariable<ulong>(
                0,
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
            return;

        selectedCards.Clear();

        currentTurnClientId.Value =
            NetworkManager.ServerClientId;

        matchPhase.Value =
            MatchPhase.WaitingForPlayers;
    }

    public void ServerBeginCardSelection(
        ulong firstTurnClientId)
    {
        if (!IsServer)
            return;

        selectedCards.Clear();

        currentTurnClientId.Value =
            firstTurnClientId;

        matchPhase.Value =
            MatchPhase.ChoosingCard;
    }

    public void ServerAddSelectedCard(
        ulong clientId,
        int cardId)
    {
        if (!IsServer)
            return;

        selectedCards.Add(
            new CardSelectionData(
                clientId,
                cardId
            )
        );
    }

    public void ServerSetTurn(
        ulong clientId)
    {
        if (!IsServer)
            return;

        currentTurnClientId.Value =
            clientId;
    }

    public void ServerSetPhase(
        MatchPhase phase)
    {
        if (!IsServer)
            return;

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
            return false;

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
            $"[NetworkMatchState] " +
            $"MatchPhase: {previous} → {current}"
        );

        MatchStateChanged?.Invoke();
    }

    private void HandleTurnChanged(
        ulong previous,
        ulong current)
    {
        Debug.Log(
            $"[NetworkMatchState] " +
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
