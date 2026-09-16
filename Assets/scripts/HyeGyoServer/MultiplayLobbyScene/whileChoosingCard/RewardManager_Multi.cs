using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RewardManager_Multi :
    MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private NetworkMatchBridge networkMatchBridge;

    [Header("UI")]
    [SerializeField]
    private Sprite[] rewardSprites;

    private GameObject rewardDisplayPrefab;

    private RewardDisplay[]
        enhanceDisplays;

    private EnhanceOptionNetData[]
        enhanceOptions;

    private int enhanceSelectedIndex;

    private bool moveSelectToggle;

    private void Awake()
    {
        if (rewardDisplayPrefab == null)
        {
            rewardDisplayPrefab =
                Resources.Load<GameObject>(
                    "Prefabs/RewardDisplay"
                );
        }
    }

    private void OnEnable()
    {
        if (networkMatchBridge != null)
        {
            networkMatchBridge
                .EnhanceCandidatesReceived +=
                HandleEnhanceCandidatesReceived;
        }
    }

    private void OnDisable()
    {
        if (networkMatchBridge != null)
        {
            networkMatchBridge
                .EnhanceCandidatesReceived -=
                HandleEnhanceCandidatesReceived;
        }
    }

    // 버튼에 연결
    public void RequestEnhanceCandidates()
    {
        if (networkMatchBridge == null)
        {
            Debug.LogError(
                "[RewardManager_Multi] " +
                "NetworkMatchBridge가 없습니다."
            );

            return;
        }

        Debug.Log(
            "[RewardManager_Multi] " +
            "강화 후보 요청"
        );

        networkMatchBridge
            .RequestEnhanceCandidates();
    }

    private void HandleEnhanceCandidatesReceived(
        EnhanceOptionNetData[] options)
    {
        enhanceOptions = options;

        ShowEnhanceOptions();

        PlayerInputManager.Instance.Load(
            "Select",
            new Dictionary<string, Action>
            {
                ["Left"] =
                    () => MoveEnhanceSelection(-1),

                ["Right"] =
                    () => MoveEnhanceSelection(1),

                ["Select"] =
                    ConfirmEnhanceSelection,
            }
        );
    }

    private void ShowEnhanceOptions()
    {
        ClearEnhanceDisplays();

        if (enhanceOptions == null ||
            enhanceOptions.Length == 0)
        {
            return;
        }

        enhanceDisplays =
            new RewardDisplay[
                enhanceOptions.Length
            ];

        for (int i = 0;
             i < enhanceOptions.Length;
             i++)
        {
            GameObject obj =
                Instantiate(
                    rewardDisplayPrefab,
                    transform
                );

            RewardDisplay display =
                obj.GetComponent<
                    RewardDisplay>();

            enhanceDisplays[i] =
                display;

            Sprite sprite =
                rewardSprites != null &&
                i < rewardSprites.Length
                    ? rewardSprites[i]
                    : null;

            display.Init(
                "",
                sprite
            );

            CardUpgrade uiUpgrade =
                enhanceOptions[i]
                    .ToCardUpgrade();

            display.SetUpgrade(
                uiUpgrade
            );
        }

        // 기존 RewardManager와 같은 배치
        SpriteRenderer card =
            enhanceDisplays[0]
                .transform
                .Find("Card")
                ?.GetComponent<
                    SpriteRenderer>();

        float spacing =
            (card != null
                ? card.bounds.size.x
                : 1f) * 2f;

        for (int i = 0;
             i < enhanceDisplays.Length;
             i++)
        {
            enhanceDisplays[i]
                .transform
                .localPosition =
                    new Vector3(
                        (i -
                         (enhanceDisplays.Length - 1)
                         / 2f)
                        * spacing,
                        0f,
                        0f
                    );
        }

        enhanceSelectedIndex = 0;

        RefreshEnhanceSelection();
    }

    private void MoveEnhanceSelection(
        int delta)
    {
        if (enhanceDisplays == null ||
            enhanceDisplays.Length == 0)
        {
            return;
        }

        int count =
            enhanceDisplays.Length;

        int previous =
            enhanceSelectedIndex;

        enhanceSelectedIndex =
            ((enhanceSelectedIndex + delta)
             % count + count)
            % count;

        RefreshEnhanceSelection();

        if (previous !=
            enhanceSelectedIndex)
        {
            PlayMoveSelectSound();
        }
    }

    private void RefreshEnhanceSelection()
    {
        if (enhanceDisplays == null)
            return;

        for (int i = 0;
             i < enhanceDisplays.Length;
             i++)
        {
            enhanceDisplays[i]
                ?.SetSelected(
                    i ==
                    enhanceSelectedIndex
                );
        }
    }

    private void ConfirmEnhanceSelection()
    {
        Debug.Log(
            "[RewardManager_Multi] " +
            $"강화 후보 선택 | " +
            $"Index: {enhanceSelectedIndex}"
        );

        networkMatchBridge
            .RequestConfirmEnhanceCandidate(
                enhanceSelectedIndex
            );

        PlayerInputManager.Instance
            .Unload();

        ClearEnhanceDisplays();

        enhanceOptions = null;
    }

    private void ClearEnhanceDisplays()
    {
        if (enhanceDisplays == null)
            return;

        foreach (
            RewardDisplay display
            in enhanceDisplays)
        {
            if (display != null)
            {
                Destroy(
                    display.gameObject
                );
            }
        }

        enhanceDisplays = null;
    }

    private void PlayMoveSelectSound()
    {
        SoundManager.Instance?.Play(
            moveSelectToggle
                ? EffectSound.MoveSelect1
                : EffectSound.MoveSelect2
        );

        moveSelectToggle =
            !moveSelectToggle;
    }
}