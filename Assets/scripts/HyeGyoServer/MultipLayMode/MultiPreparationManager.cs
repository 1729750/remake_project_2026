using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class MultiPreparationManager : MonoBehaviour
{
    private const int MaxRounds = 10;
    [SerializeField] private PlayerPreparationRegistry preparationRegistry;
    [SerializeField] private NetworkMatchState matchState;
    [SerializeField] private NetworkMatchBridge networkMatchBridge;
    [SerializeField] private CardOptionGenerator cardOptionGenerator;
    [SerializeField, Min(0f)] private float transitionDelaySeconds = 1f;
    private bool initializationRequested, initializing, transitionStarted, transitionCancelled;
    private Coroutine transition;
    private readonly Dictionary<ulong, int> currentRoundByClient = new();
    private readonly Dictionary<ulong, PreparationOption[]> currentOptionsByClient = new();
    private readonly Dictionary<ulong, int> revisionByClient = new();
    private readonly Dictionary<ulong, long> lastRequestByClient = new();
    public int Epoch => matchState.PreparationEpoch;

    private void OnEnable()
    {
        if (matchState != null) matchState.LifecycleChanged += TryInitialize;
        if (networkMatchBridge != null) networkMatchBridge.PreparationAvailabilityChanged += TryInitialize;
        if (preparationRegistry != null)
        {
            preparationRegistry.PlayerRegistered += HandleRegistered;
            preparationRegistry.PlayerRemoved += HandleRemoved;
        }
        TryInitialize();
    }

    public void RequestInitialization()
    {
        initializationRequested = true;
        TryInitialize();
    }

    private void HandleRegistered(ulong clientId) => TryInitialize();
    private void TryInitialize()
    {
        if (!initializationRequested || initializing || !isActiveAndEnabled) return;
        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsServer && matchState != null && matchState.IsSpawned &&
            networkMatchBridge != null && (!networkMatchBridge.IsSpawned || !networkMatchBridge.IsMultiGameMode))
            matchState.ServerSetPreparationReady(false);
        if (transitionStarted || transitionCancelled) return;
        if (network == null || !network.IsServer || matchState == null || !matchState.IsSpawned ||
            networkMatchBridge == null || !networkMatchBridge.IsSpawned || !networkMatchBridge.IsMultiGameMode ||
            preparationRegistry == null || cardOptionGenerator == null || matchState.PreparationReady) return;
        initializing = true;
        try
        {
            preparationRegistry.BindSession(network);
            if (preparationRegistry.Count != 2) return;
            if (!cardOptionGenerator.ValidateConfiguration(out string reason))
                throw new InvalidOperationException(reason);
            foreach (PlayerPreparationData player in preparationRegistry.Players)
            {
                if (!network.ConnectedClients.ContainsKey(player.ClientId)) return;
                if (!ValidateDeck(player)) throw new InvalidOperationException("Existing owner deck has invalid source metadata.");
            }
            matchState.ServerBeginCardSelection();
            matchState.ServerSetPreparationReady(true);
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
        finally { initializing = false; }
    }

    private bool ValidateDeck(PlayerPreparationData player)
    {
        if (player.FinalDeck.Count > MaxRounds || player.FinalDeck.Count != player.FinalDeckSourceIndices.Count) return false;
        for (int i = 0; i < player.FinalDeck.Count; i++)
            if (player.FinalDeck[i] == null || !player.FinalDeck[i].TryGetValidatedEffects(out _) ||
                cardOptionGenerator.GetCardByPoolIndex(player.FinalDeckSourceIndices[i]) == null) return false;
        return true;
    }

    private bool Validate(ulong clientId, out PlayerPreparationData player, out string reason)
    {
        player = null;
        reason = string.Empty;
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || !network.ConnectedClients.ContainsKey(clientId) ||
            preparationRegistry == null || !preparationRegistry.TryGetPlayer(clientId, out player))
        {
            reason = "Unregistered preparation owner.";
            return false;
        }
        if (matchState == null || !matchState.PreparationReady || cardOptionGenerator == null ||
            (matchState.CurrentPhase != MatchPhase.ChoosingCard && matchState.CurrentPhase != MatchPhase.ChoosingCondition))
        {
            reason = "Preparation is not active.";
            return false;
        }
        if (!ValidateDeck(player))
        {
            reason = "Existing owner deck has invalid source metadata.";
            return false;
        }
        return true;
    }

    public bool TryBeginForPlayer(ulong clientId, out PreparationOption[] options, out string rejectReason)
    {
        options = null;
        if (!Validate(clientId, out PlayerPreparationData player, out rejectReason)) return false;
        if (player.ConditionCompleted) return true;
        if (currentOptionsByClient.TryGetValue(clientId, out options)) return true;
        try
        {
            options = cardOptionGenerator.GenerateOptions(player, MaxRounds - GetRemainingChoices(clientId));
            currentOptionsByClient[clientId] = options;
            revisionByClient[clientId] = GetRevision(clientId) + 1;
            return true;
        }
        catch (Exception exception)
        {
            rejectReason = "Candidate generation failed; owner state can be requested again.";
            Debug.LogException(exception, this);
            return false;
        }
    }

    public bool TryConfirmOption(ulong clientId, int optionIndex, int expectedRound, int expectedRevision,
        int expectedEpoch, long requestId, out PreparationOption[] nextOptions, out string rejectReason)
    {
        nextOptions = null;
        if (!Validate(clientId, out PlayerPreparationData player, out rejectReason)) return false;
        int remaining = GetRemainingChoices(clientId);
        if (requestId <= GetLastRequest(clientId))
        {
            rejectReason = "Duplicate preparation request.";
            return false;
        }
        lastRequestByClient[clientId] = requestId;
        if (player.ConditionCompleted || expectedRound != MaxRounds - remaining || expectedRevision != GetRevision(clientId) ||
            expectedEpoch != Epoch || !currentOptionsByClient.TryGetValue(clientId, out PreparationOption[] options) ||
            optionIndex < -1 || optionIndex >= options.Length)
        {
            rejectReason = "Stale or invalid preparation request.";
            return false;
        }
        PreparationOption selected = optionIndex == -1
            ? new PreparationOption { Type = PreparationOptionType.Skip } : options[optionIndex];
        switch (selected.Type)
        {
            case PreparationOptionType.AddCard:
                if (selected.Card == null || !selected.Card.TryGetValidatedEffects(out _) ||
                    cardOptionGenerator.GetCardByPoolIndex(selected.CardPoolIndex) == null)
                {
                    rejectReason = "Invalid card source.";
                    return false;
                }
                player.FinalDeck.Add(CardOptionGenerator.CopyCard(selected.Card));
                player.FinalDeckSourceIndices.Add(selected.CardPoolIndex);
                break;
            case PreparationOptionType.EnhanceCard:
                int target = selected.TargetCardIndex;
                if (target < 0 || target >= player.FinalDeck.Count || selected.Upgrade == null ||
                    selected.CardPoolIndex != player.FinalDeckSourceIndices[target] ||
                    !RewardManager.CanEnhance(player.FinalDeck[target], selected.Upgrade))
                {
                    rejectReason = "Invalid owner upgrade target.";
                    return false;
                }
                player.FinalDeck[target].ApplyUpgrade(CardOptionGenerator.CopyUpgrade(selected.Upgrade));
                break;
            case PreparationOptionType.Skip: break;
            default:
                rejectReason = "Unknown preparation option.";
                return false;
        }
        currentRoundByClient[clientId] = expectedRound + 1;
        revisionByClient[clientId] = GetRevision(clientId) + 1;
        currentOptionsByClient.Remove(clientId);
        CardOptionGenerator.DisposeOptions(options);
        player.ConditionCompleted = GetRemainingChoices(clientId) == 0;
        if (!player.ConditionCompleted) TryBeginForPlayer(clientId, out nextOptions, out _);
        // A failed next-offer generation cannot undo or repeat the committed decision.
        return true;
    }

    public CardDefinition[] GetConfirmedDeck(ulong clientId) =>
        preparationRegistry.TryGetPlayer(clientId, out PlayerPreparationData player) ? player.FinalDeck.ToArray() : Array.Empty<CardDefinition>();
    public int[] GetConfirmedSources(ulong clientId) =>
        preparationRegistry.TryGetPlayer(clientId, out PlayerPreparationData player) ? player.FinalDeckSourceIndices.ToArray() : Array.Empty<int>();
    public int GetRemainingChoices(ulong clientId) => MaxRounds - (currentRoundByClient.TryGetValue(clientId, out int round) ? round : 0);
    public int GetRevision(ulong clientId) => revisionByClient.TryGetValue(clientId, out int revision) ? revision : 0;
    private long GetLastRequest(ulong clientId) => lastRequestByClient.TryGetValue(clientId, out long request) ? request : 0;

    public void CheckAllPlayersCompleted()
    {
        if (transitionStarted || transitionCancelled || !AllCompleted()) return;
        transitionStarted = true;
        transition = StartCoroutine(CompleteAfterDelay());
    }

    private bool AllCompleted()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || preparationRegistry.Count != 2) return false;
        foreach (PlayerPreparationData player in preparationRegistry.Players)
            if (!player.ConditionCompleted || !network.ConnectedClients.ContainsKey(player.ClientId)) return false;
        return true;
    }

    private void HandleRemoved(ulong clientId)
    {
        matchState.ServerSetPreparationReady(false);
        transitionCancelled = true;
        if (transition != null) StopCoroutine(transition);
        transition = null;
        if (currentOptionsByClient.TryGetValue(clientId, out PreparationOption[] choices)) CardOptionGenerator.DisposeOptions(choices);
        currentOptionsByClient.Remove(clientId);
        currentRoundByClient.Remove(clientId);
        revisionByClient.Remove(clientId);
        lastRequestByClient.Remove(clientId);
    }

    private void OnDisable()
    {
        if (matchState != null)
        {
            matchState.LifecycleChanged -= TryInitialize;
            matchState.ServerSetPreparationReady(false);
        }
        if (networkMatchBridge != null) networkMatchBridge.PreparationAvailabilityChanged -= TryInitialize;
        if (preparationRegistry != null)
        {
            preparationRegistry.PlayerRegistered -= HandleRegistered;
            preparationRegistry.PlayerRemoved -= HandleRemoved;
        }
        if (transition != null) StopCoroutine(transition);
        transition = null;
    }

    private void OnDestroy()
    {
        foreach (PreparationOption[] choices in currentOptionsByClient.Values) CardOptionGenerator.DisposeOptions(choices);
    }

    private IEnumerator CompleteAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, transitionDelaySeconds));
        transition = null;
        if (transitionCancelled || !AllCompleted()) yield break;
        matchState.ServerSetPhase(MatchPhase.Battle);
        MapManager_Multi.Instance?.NotifyBothPlayersConfirmed();
    }
}
