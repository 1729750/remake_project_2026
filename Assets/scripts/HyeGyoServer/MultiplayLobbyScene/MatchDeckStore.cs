using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lobby Scene에서 확정된 서버 FinalDeck을 MultiPlayMode까지 보존한다.
/// NetworkManager와 같은 persistent GameObject에 런타임으로 부착한다.
/// CardDefinition은 네트워크로 직접 보내지 않고 서버 내부 깊은 복제본만 보관한다.
/// </summary>
public sealed class MatchDeckStore : MonoBehaviour
{
    public static MatchDeckStore Instance { get; private set; }

    private readonly Dictionary<ulong, CardDefinition[]>
        decksByClient = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Clear();
            Instance = null;
        }
    }

    public static MatchDeckStore EnsureOn(
        GameObject persistentRoot)
    {
        if (Instance != null)
            return Instance;

        if (persistentRoot == null)
            return null;

        MatchDeckStore store =
            persistentRoot.GetComponent<MatchDeckStore>();

        if (store == null)
        {
            store =
                persistentRoot.AddComponent<MatchDeckStore>();
        }

        return store;
    }

    public bool Capture(
        PlayerPreparationRegistry registry,
        out string rejectReason)
    {
        rejectReason = string.Empty;

        if (registry == null || registry.Count != 2)
        {
            rejectReason =
                "FinalDeck을 저장할 플레이어가 2명이 아닙니다.";

            return false;
        }

        Clear();

        foreach (PlayerPreparationData player
                 in registry.Players)
        {
            if (player == null ||
                !player.CardSelectionCompleted ||
                !player.ConditionCompleted ||
                player.FinalDeck == null ||
                player.FinalDeck.Count == 0)
            {
                rejectReason =
                    "완료되지 않은 플레이어 FinalDeck이 있습니다.";

                Clear();
                return false;
            }

            CardDefinition[] snapshot =
                CloneDeck(player.FinalDeck);

            if (snapshot.Length == 0)
            {
                rejectReason = "FinalDeck이 비어 있습니다.";
                Clear();
                return false;
            }

            decksByClient[player.ClientId] = snapshot;
        }

        return decksByClient.Count == 2;
    }

    public bool TryCreateDeck(
        ulong clientId,
        out CardDefinition[] deck)
    {
        if (!decksByClient.TryGetValue(
                clientId,
                out CardDefinition[] snapshot))
        {
            deck = null;
            return false;
        }

        deck = CloneDeck(snapshot);
        return deck.Length > 0;
    }

    public void Clear()
    {
        foreach (CardDefinition[] deck
                 in decksByClient.Values)
        {
            if (deck == null)
                continue;

            foreach (CardDefinition card in deck)
            {
                if (card != null)
                {
                    Destroy(card);
                }
            }
        }

        decksByClient.Clear();
    }

    private static CardDefinition[] CloneDeck(
        IEnumerable<CardDefinition> source)
    {
        List<CardDefinition> result = new();

        if (source == null)
            return result.ToArray();

        foreach (CardDefinition card in source)
        {
            if (card != null)
            {
                result.Add(card.Clone());
            }
        }

        return result.ToArray();
    }
}
