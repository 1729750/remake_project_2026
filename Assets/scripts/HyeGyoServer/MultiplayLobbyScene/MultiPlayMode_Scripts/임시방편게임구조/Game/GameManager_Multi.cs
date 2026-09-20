using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class GameManager_Multi : NetworkBehaviour
{
    public static GameManager_Multi Instance { get; private set; }

    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [Header("Managers")]
    [SerializeField]
    private BattleManager_Multi battleManager;

    [Header("View")]
    [SerializeField]
    private FadeIn fadeIn;

    private bool _gameStarted;
    private bool _initialized;

    public bool IsGameStarted => _gameStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;


        BuildEffectSummaryCache();
    }

    // =========================================================
// Effect Summary
// PopupDisplay_Multi에서 사용
// =========================================================

private const string EffectSummariesResourcePath =
    "Data/EffectSummaries";

private static Dictionary<EffectType, string>
    _effectSummaryCache;

private void BuildEffectSummaryCache()
{
    _effectSummaryCache =
        new Dictionary<EffectType, string>();

    TextAsset json =
        Resources.Load<TextAsset>(
            EffectSummariesResourcePath
        );

    if (json == null)
    {
        Debug.LogWarning(
            "[GameManager_Multi] " +
            $"Resources/{EffectSummariesResourcePath}.json을 찾지 못했습니다."
        );

        return;
    }

    EffectSummaryTable table =
        JsonUtility.FromJson<EffectSummaryTable>(
            json.text
        );

    if (table?.summaries == null)
        return;

    foreach (
        EffectSummaryJsonEntry entry
        in table.summaries)
    {
        if (Enum.TryParse(
            entry.effectType,
            true,
            out EffectType type))
        {
            _effectSummaryCache[type] =
                entry.summary;
        }
        else
        {
            Debug.LogWarning(
                "[GameManager_Multi] " +
                "알 수 없는 EffectType: " +
                entry.effectType
            );
        }
    }
}

public static string GetEffectSummary(
    EffectType effectType)
{
    if (_effectSummaryCache == null)
    {
        Instance?.BuildEffectSummaryCache();
    }

    if (_effectSummaryCache != null &&
        _effectSummaryCache.TryGetValue(
            effectType,
            out string summary))
    {
        return summary;
    }

    return string.Empty;
}

[Serializable]
private class EffectSummaryJsonEntry
{
    public string effectType;
    public string summary;
}

[Serializable]
private class EffectSummaryTable
{
    public List<EffectSummaryJsonEntry> summaries;
}

    public override void OnNetworkSpawn()
    {
        if (gameNetworkState == null)
        {
            Debug.LogError(
                "[GameManager_Multi] GameNetworkState가 없습니다."
            );
            return;
        }

        gameNetworkState.StateChanged +=
            HandleNetworkStateChanged;

        GameStart();

        fadeIn?.Play();

        Debug.Log(
            "[GameManager_Multi] OnNetworkSpawn\n" +
            $"IsServer: {IsServer}\n" +
            $"IsClient: {IsClient}\n" +
            $"LocalClientId: {NetworkManager.Singleton.LocalClientId}"
        );

        if (IsServer)
            TryStartBattleServer();
    }

    public override void OnNetworkDespawn()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged -=
                HandleNetworkStateChanged;
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (_gameStarted)
            return;

        TryStartBattleServer();
    }

    public void GameStart()
    {
        if (_initialized)
            return;

        _initialized = true;

        Debug.Log(
            "[GameManager_Multi] GameStart - " +
            "MapManager 없이 바로 Battle 준비"
        );
    }

    private void HandleNetworkStateChanged()
    {
        if (!IsServer)
            return;

        TryStartBattleServer();
    }

    private void TryStartBattleServer()
    {
        if (!IsServer)
            return;

        if (_gameStarted)
            return;

        if (gameNetworkState == null ||
            !gameNetworkState.IsSpawned)
        {
            return;
        }

        if (!gameNetworkState.PlayersAssigned)
            return;

        if (battleManager == null)
        {
            Debug.LogError(
                "[GameManager_Multi] " +
                "BattleManager_Multi가 연결되지 않았습니다."
            );
            return;
        }

        if (!battleManager.IsSpawned)
            return;

        _gameStarted = true;

        Debug.Log(
            "[GameManager_Multi] " +
            "2명 준비 완료 → Battle 시작\n" +
            $"Player0: {gameNetworkState.Player0ClientId.Value}\n" +
            $"Player1: {gameNetworkState.Player1ClientId.Value}"
        );

        gameNetworkState.SetMatchStateServer(
            MultiMatchState.BattleStarting
        );

        battleManager.InitializeBattleServer();
    }

    public void StartBattle()
    {
        if (!IsServer)
            return;

        TryStartBattleServer();
    }

    public MultiMatchState GetGameState()
    {
        if (gameNetworkState == null)
            return MultiMatchState.WaitingForPlayers;

        return gameNetworkState.MatchState.Value;
    }

    public ulong GetOpponentClientId(
        ulong clientId)
    {
        if (gameNetworkState == null)
        {
            return GameNetworkState.UnassignedClientId;
        }

        return gameNetworkState.GetOpponentClientId(
            clientId
        );
    }

    public ulong GetLocalOpponentClientId()
    {
        if (NetworkManager.Singleton == null)
        {
            return GameNetworkState.UnassignedClientId;
        }

        return GetOpponentClientId(
            NetworkManager.Singleton.LocalClientId
        );
    }
}