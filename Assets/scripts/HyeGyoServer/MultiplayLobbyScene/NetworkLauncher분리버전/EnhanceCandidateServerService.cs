using System.Collections.Generic;
using UnityEngine;

public sealed class EnhanceCandidateServerService
    : MonoBehaviour
{
    [SerializeField]
    private NetworkMatchState matchState;

    [SerializeField]
    private PlayerPreparationRegistry
        preparationRegistry;


    private readonly Dictionary<
        ulong,
        CardUpgrade[]
    > candidatesByClient = new();


    private void OnEnable()
    {
        if (preparationRegistry != null)
        {
            preparationRegistry.PlayerRemoved +=
                HandlePlayerRemoved;
        }
    }


    private void OnDisable()
    {
        if (preparationRegistry != null)
        {
            preparationRegistry.PlayerRemoved -=
                HandlePlayerRemoved;
        }
    }


    public bool TryCreateCandidates(
        ulong senderClientId,
        out EnhanceOptionNetData[] networkOptions,
        out string rejectReason)
    {
        networkOptions = null;

        if (!Validate(
                senderClientId,
                out rejectReason))
        {
            return false;
        }


        CardUpgrade[] options =
            new CardUpgrade[3];


        for (int i = 0;
             i < options.Length;
             i++)
        {
            // TODO:
            // 다음 단계에서 Multi 전용 Generator로 교체
            options[i] =
                RewardManager.RollEnhanceOption();
        }


        candidatesByClient[
            senderClientId
        ] = options;


        networkOptions =
            new EnhanceOptionNetData[
                options.Length
            ];


        for (int i = 0;
             i < options.Length;
             i++)
        {
            networkOptions[i] =
                EnhanceOptionNetData
                    .FromCardUpgrade(
                        options[i]
                    );
        }


        Debug.Log(
            "[EnhanceCandidateServerService] " +
            $"후보 생성 완료 | " +
            $"ClientId: {senderClientId}"
        );


        rejectReason =
            string.Empty;

        return true;
    }


    public bool TryConfirmCandidate(
        ulong senderClientId,
        int selectedIndex,
        out string rejectReason)
    {
        if (!Validate(
                senderClientId,
                out rejectReason))
        {
            return false;
        }


        if (!candidatesByClient.TryGetValue(
                senderClientId,
                out CardUpgrade[] options))
        {
            rejectReason =
                "발급된 강화 후보가 없습니다.";

            return false;
        }


        if (selectedIndex < 0 ||
            selectedIndex >= options.Length)
        {
            rejectReason =
                "잘못된 강화 후보 번호입니다.";

            return false;
        }


        CardUpgrade selected =
            options[selectedIndex];

        var effect =
            selected.effect.GetEffect();

        var effectType =
            effect.GetEffectType();

        var magnitude =
            effect.GetMagnitude();


        Debug.Log(
            "[EnhanceCandidateServerService] " +
            $"ClientId: {senderClientId} | " +
            $"Index: {selectedIndex} | " +
            $"Effect: {effectType} | " +
            $"Magnitude: {magnitude} | " +
            $"CostDelta: {selected.costDelta} | " +
            $"CooldownDelta: {selected.cooldownDelta}"
        );


        candidatesByClient.Remove(
            senderClientId
        );


        rejectReason =
            string.Empty;

        return true;
    }


    private bool Validate(
        ulong clientId,
        out string rejectReason)
    {
        if (matchState == null ||
            !matchState.IsSpawned ||
            !matchState.IsServer)
        {
            rejectReason =
                "Server 상태가 올바르지 않습니다.";

            return false;
        }


        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCondition)
        {
            rejectReason =
                "현재 강화 단계가 아닙니다.";

            return false;
        }


        if (preparationRegistry == null ||
            !preparationRegistry.ContainsPlayer(
                clientId))
        {
            rejectReason =
                "등록되지 않은 플레이어입니다.";

            return false;
        }


        rejectReason =
            string.Empty;

        return true;
    }


    private void HandlePlayerRemoved(
        ulong clientId)
    {
        candidatesByClient.Remove(
            clientId
        );
    }
}