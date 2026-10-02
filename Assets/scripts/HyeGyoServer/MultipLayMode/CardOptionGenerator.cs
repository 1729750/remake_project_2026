using System.Collections.Generic;
using UnityEngine;

public sealed class CardOptionGenerator : MonoBehaviour
{
    [Header("Card Pool")]
    [SerializeField]
    private List<CardDefinition> cardPool;

    public PreparationOption[] GenerateOptions(
        PlayerPreparationData player,
        int round)
    {
        var result =
            new PreparationOption[3];

        // 1번 슬롯:
        // 카드 추가는 항상 1개
        result[0] =
            CreateAddCardOption(player);

        // 2번 슬롯:
        // 강화 가능하면 강화 1개 보장
        if (HasEnhanceableCard(player))
        {
            result[1] =
                CreateEnhanceOption(player);
        }
        else
        {
            // 강화 대상이 없으면 카드 추가로 대체
            result[1] =
                CreateAddCardOption(player);
        }

        // 3번 슬롯:
        // 카드 / 강화 / 스킵 중 랜덤
        int randomType =
            Random.Range(0, 3);

        switch (randomType)
        {
            case 0:
                result[2] =
                    CreateAddCardOption(player);
                break;

            case 1:
                if (HasEnhanceableCard(player))
                {
                    result[2] =
                        CreateEnhanceOption(player);
                }
                else
                {
                    result[2] =
                        CreateSkipOption();
                }

                break;

            default:
                result[2] =
                    CreateSkipOption();
                break;
        }

        return result;
    }

    private PreparationOption
        CreateAddCardOption(
            PlayerPreparationData player)
    {
        if (cardPool == null ||
            cardPool.Count == 0)
        {
            Debug.LogWarning(
                "[CardOptionGenerator] Card Pool이 비어 있습니다."
            );

            return CreateSkipOption();
        }

        CardDefinition card =
            cardPool[
                Random.Range(
                    0,
                    cardPool.Count
                )
            ];

        return new PreparationOption
        {
            Type =
                PreparationOptionType.AddCard,

            Card =
                card,

            TargetCardIndex =
                -1,

            EnhanceId =
                -1
        };
    }

    private PreparationOption
        CreateEnhanceOption(
            PlayerPreparationData player)
    {
        List<int> candidates =
            GetEnhanceableCardIndexes(
                player
            );

        if (candidates.Count == 0)
        {
            return
                CreateAddCardOption(
                    player
                );
        }

        int targetIndex =
            candidates[
                Random.Range(
                    0,
                    candidates.Count
                )
            ];

        return new PreparationOption
        {
            Type =
                PreparationOptionType.EnhanceCard,

            Card =
                null,

            TargetCardIndex =
                targetIndex,

            EnhanceId =
                0
        };
    }

    private PreparationOption
        CreateSkipOption()
    {
        return new PreparationOption
        {
            Type =
                PreparationOptionType.Skip,

            Card =
                null,

            TargetCardIndex =
                -1,

            EnhanceId =
                -1
        };
    }

    private bool HasEnhanceableCard(
        PlayerPreparationData player)
    {
        if (player == null)
        {
            return false;
        }

        return
            GetEnhanceableCardIndexes(
                player
            ).Count > 0;
    }

    private List<int>
        GetEnhanceableCardIndexes(
            PlayerPreparationData player)
    {
        var result =
            new List<int>();

        if (player == null ||
            player.FinalDeck == null)
        {
            return result;
        }

        for (int i = 0;
             i < player.FinalDeck.Count;
             i++)
        {
            CardDefinition card =
                player.FinalDeck[i];

            if (card == null)
            {
                continue;
            }

            // TODO:
            // 나중에 실제 강화 가능 조건으로 교체.
            // 지금은 null이 아닌 모든 카드를
            // 강화 가능 대상으로 취급.
            result.Add(i);
        }

        return result;
    }
}