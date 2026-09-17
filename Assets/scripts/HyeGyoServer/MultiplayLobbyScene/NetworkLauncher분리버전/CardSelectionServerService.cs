using Unity.Netcode;
using UnityEngine;

public sealed class CardSelectionServerService : MonoBehaviour
{
    [Header("Preparation Data")]
    [SerializeField]
    private PlayerPreparationRegistry preparationRegistry;


    public int RegisteredPlayerCount =>
        preparationRegistry != null
            ? preparationRegistry.Count
            : 0;


    public bool HasTwoPlayers =>
        RegisteredPlayerCount == 2;


    private void Awake()
    {
        EnsureReferences();
    }


    private void OnEnable()
    {
        EnsureReferences();

        if (preparationRegistry == null)
        {
            Debug.LogError(
                "[CardSelectionServerService] " +
                "PlayerPreparationRegistry가 없습니다."
            );

            return;
        }

        preparationRegistry.PlayerRegistered +=
            HandlePlayerRegistered;

        preparationRegistry.PlayerRemoved +=
            HandlePlayerRemoved;

        Debug.Log(
            "[CardSelectionServerService] " +
            "PlayerPreparationRegistry 연결 완료"
        );

        LogCurrentPlayers();
    }


    private void OnDisable()
    {
        if (preparationRegistry == null)
        {
            return;
        }

        preparationRegistry.PlayerRegistered -=
            HandlePlayerRegistered;

        preparationRegistry.PlayerRemoved -=
            HandlePlayerRemoved;
    }


    private void EnsureReferences()
    {
        if (preparationRegistry != null)
        {
            return;
        }

        preparationRegistry =
            FindFirstObjectByType<
                PlayerPreparationRegistry>();
    }


    // ==================================================
    // PlayerPreparationData 조회
    // ==================================================

    public bool TryGetPlayerData(
        ulong clientId,
        out PlayerPreparationData player)
    {
        player = null;

        if (preparationRegistry == null)
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                "PlayerPreparationRegistry가 없습니다."
            );

            return false;
        }

        if (!preparationRegistry.TryGetPlayer(
                clientId,
                out player))
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                $"플레이어 데이터를 찾지 못했습니다. | " +
                $"ClientId: {clientId}"
            );

            return false;
        }

        return true;
    }


    public bool TryGetOtherPlayerData(
        ulong currentClientId,
        out PlayerPreparationData otherPlayer)
    {
        otherPlayer = null;

        if (preparationRegistry == null)
        {
            return false;
        }

        foreach (
            PlayerPreparationData player
            in preparationRegistry.Players)
        {
            if (player == null)
            {
                continue;
            }

            if (player.ClientId ==
                currentClientId)
            {
                continue;
            }

            otherPlayer = player;

            return true;
        }

        return false;
    }


    // ==================================================
    // Card Selection
    // ==================================================

    /// <summary>
    /// 현재는 카드 선택 로직을 연결하지 않는다.
    ///
    /// 다음 단계에서:
    /// Server 후보 생성
    /// → CardCandidates 저장
    /// → Client 선택 요청
    /// → Server 검증
    /// 순서로 구현한다.
    /// </summary>
    public bool TryChooseCard(
        ulong senderClientId,
        int cardId,
        out string rejectReason)
    {
        rejectReason =
            "카드 선택 기능은 아직 연결하지 않은 상태입니다.";

        Debug.Log(
            "[CardSelectionServerService] " +
            "카드 선택 요청 수신 - 현재 Blank 처리 | " +
            $"ClientId: {senderClientId} | " +
            $"CardId: {cardId}"
        );

        return false;
    }


    // ==================================================
    // Registry Events
    // ==================================================

    private void HandlePlayerRegistered(
        ulong clientId)
    {
        if (!preparationRegistry.TryGetPlayer(
                clientId,
                out PlayerPreparationData player))
        {
            return;
        }

        Debug.Log(
            "[CardSelectionServerService] " +
            $"준비 데이터 연결 | " +
            $"ClientId: {clientId} | " +
            $"Candidates: " +
            $"{GetCandidateCount(player)} | " +
            $"SelectedIndex: " +
            $"{player.SelectedCardIndex} | " +
            $"FinalDeck: " +
            $"{player.FinalDeck.Count} | " +
            $"RegistryCount: " +
            $"{preparationRegistry.Count}"
        );

        if (preparationRegistry.Count == 2)
        {
            Debug.Log(
                "[CardSelectionServerService] " +
                "Host / Client 준비 데이터 2개 연결 완료"
            );
        }
    }


    private void HandlePlayerRemoved(
        ulong clientId)
    {
        Debug.Log(
            "[CardSelectionServerService] " +
            $"플레이어 준비 데이터 연결 해제 | " +
            $"ClientId: {clientId} | " +
            $"RegistryCount: " +
            $"{preparationRegistry.Count}"
        );
    }


    // ==================================================
    // Debug
    // ==================================================

    [ContextMenu(
        "Debug/Print Player Preparation Data")]
    private void LogCurrentPlayers()
    {
        if (preparationRegistry == null)
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                "Registry가 없습니다."
            );

            return;
        }

        Debug.Log(
            "[CardSelectionServerService] " +
            $"현재 준비 데이터 수: " +
            $"{preparationRegistry.Count}"
        );

        foreach (
            PlayerPreparationData player
            in preparationRegistry.Players)
        {
            if (player == null)
            {
                continue;
            }

            Debug.Log(
                "[CardSelectionServerService] " +
                $"Player | " +
                $"ClientId: {player.ClientId} | " +
                $"Candidates: " +
                $"{GetCandidateCount(player)} | " +
                $"SelectedIndex: " +
                $"{player.SelectedCardIndex} | " +
                $"CardCompleted: " +
                $"{player.CardSelectionCompleted} | " +
                $"FinalDeck: " +
                $"{player.FinalDeck.Count}"
            );
        }
    }


    private int GetCandidateCount(
        PlayerPreparationData player)
    {
        if (player == null ||
            player.CardCandidates == null)
        {
            return 0;
        }

        return player.CardCandidates.Length;
    }
}