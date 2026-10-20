using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class MultiPreparationManager : MonoBehaviour
{
    private const int MaxRounds = 10;
    [SerializeField] private PlayerPreparationRegistry preparationRegistry;
    [SerializeField] private NetworkMatchState matchState;
    [SerializeField] private CardOptionGenerator cardOptionGenerator;
    [SerializeField, Min(0f)] private float transitionDelaySeconds = 1f;
    private bool transitionStarted;
    private readonly Dictionary<ulong, int> currentRoundByClient = new();
    private readonly Dictionary<ulong, PreparationOption[]> currentOptionsByClient = new();

    private bool Validate(ulong clientId, out PlayerPreparationData player, out string reason)
    {
        player = null;
        reason = string.Empty;
        var network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || !network.ConnectedClients.ContainsKey(clientId) ||
            preparationRegistry == null || !preparationRegistry.TryGetPlayer(clientId, out player))
        {
            reason = "Unregistered preparation owner.";
            return false;
        }
        if (matchState == null || cardOptionGenerator == null ||
            (matchState.CurrentPhase != MatchPhase.ChoosingCard && matchState.CurrentPhase != MatchPhase.ChoosingCondition))
        {
            reason = "Preparation is not active.";
            return false;
        }
        return true;
    }

    public bool TryBeginForPlayer(ulong clientId, out PreparationOption[] options, out string rejectReason)
    {
        options = null;
        if (!Validate(clientId, out PlayerPreparationData player, out rejectReason)) return false;
        if (player.FinalDeck.Count != player.FinalDeckSourceIndices.Count)
        {
            rejectReason = "Existing deck has no source metadata.";
            return false;
        }
        if (player.ConditionCompleted) return true;
        if (currentOptionsByClient.TryGetValue(clientId, out options)) return true;
        options = cardOptionGenerator.GenerateOptions(player, MaxRounds - GetRemainingChoices(clientId));
        if (options == null || options.Length != 3)
        {
            rejectReason = "Invalid candidate pool.";
            return false;
        }
        currentOptionsByClient[clientId] = options;
        return true;
    }

    public bool TryConfirmOption(ulong clientId, int optionIndex, int expectedRound,
        out PreparationOption selectedOption, out PreparationOption[] nextOptions,
        out int remaining, out string rejectReason)
    {
        selectedOption = default;
        nextOptions = null;
        remaining = GetRemainingChoices(clientId);
        if (!Validate(clientId, out PlayerPreparationData player, out rejectReason)) return false;
        if (player.ConditionCompleted || expectedRound != MaxRounds - remaining ||
            !currentOptionsByClient.TryGetValue(clientId, out PreparationOption[] options) ||
            optionIndex < -1 || optionIndex >= options.Length)
        {
            rejectReason = "Stale or invalid preparation request.";
            return false;
        }
        selectedOption = optionIndex == -1
            ? new PreparationOption { Type = PreparationOptionType.Skip } : options[optionIndex];
        switch (selectedOption.Type)
        {
            case PreparationOptionType.AddCard:
                if (selectedOption.Card == null || cardOptionGenerator.GetCardByPoolIndex(selectedOption.CardPoolIndex) == null)
                {
                    rejectReason = "Invalid card source.";
                    return false;
                }
                player.FinalDeck.Add(selectedOption.Card.Clone());
                player.FinalDeckSourceIndices.Add(selectedOption.CardPoolIndex);
                break;
            case PreparationOptionType.EnhanceCard:
                int target = selectedOption.TargetCardIndex;
                if (target < 0 || target >= player.FinalDeck.Count || selectedOption.Upgrade == null ||
                    !RewardManager.CanEnhance(player.FinalDeck[target], selectedOption.Upgrade))
                {
                    rejectReason = "Invalid owner upgrade target.";
                    return false;
                }
                player.FinalDeck[target].ApplyUpgrade(selectedOption.Upgrade);
                break;
            case PreparationOptionType.Skip:
                break;
            default:
                rejectReason = "Unknown preparation option.";
                return false;
        }

        int round = expectedRound + 1;
        currentRoundByClient[clientId] = round;
        currentOptionsByClient.Remove(clientId);
        foreach (PreparationOption option in options)
            if (option.Card != null) Destroy(option.Card);
        remaining = MaxRounds - round;
        player.ConditionCompleted = remaining == 0;
        if (!player.ConditionCompleted)
        {
            nextOptions = cardOptionGenerator.GenerateOptions(player, round);
            if (nextOptions != null) currentOptionsByClient[clientId] = nextOptions;
            // The decision is already committed; candidate retry must never consume it twice.
        }
        return true;
    }

    public bool TrySkip(ulong clientId, int expectedRound, out PreparationOption[] nextOptions,
        out int remaining, out string rejectReason) => TryConfirmOption(clientId, -1, expectedRound,
            out _, out nextOptions, out remaining, out rejectReason);

    public CardDefinition[] GetConfirmedDeck(ulong clientId) =>
        preparationRegistry.TryGetPlayer(clientId, out PlayerPreparationData player) ? player.FinalDeck.ToArray() : null;

    public int[] GetConfirmedSources(ulong clientId) =>
        preparationRegistry.TryGetPlayer(clientId, out PlayerPreparationData player) ? player.FinalDeckSourceIndices.ToArray() : null;

    public int GetRemainingChoices(ulong clientId) =>
        MaxRounds - (currentRoundByClient.TryGetValue(clientId, out int round) ? round : 0);

    public void CheckAllPlayersCompleted()
    {
        if (transitionStarted || !AllCompleted()) return;
        transitionStarted = true;
        StartCoroutine(CompleteAfterDelay());
    }

    private bool AllCompleted()
    {
        var network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || preparationRegistry.Count != 2) return false;
        foreach (PlayerPreparationData player in preparationRegistry.Players)
            if (!player.ConditionCompleted || !network.ConnectedClients.ContainsKey(player.ClientId)) return false;
        return true;
    }

    private void OnDestroy()
    {
        foreach (PreparationOption[] choices in currentOptionsByClient.Values)
            foreach (PreparationOption option in choices)
                if (option.Card != null) Destroy(option.Card);
    }

    private IEnumerator CompleteAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, transitionDelaySeconds));
        if (AllCompleted()) MapManager_Multi.Instance?.NotifyBothPlayersConfirmed();
        else Debug.LogWarning("[MultiPreparationManager] Transition cancelled: owner disconnected.");
    }
}
