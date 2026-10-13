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
        PreparationOption[] result =
            new PreparationOption[3];

        // 1번: 무조건 강화
        if (HasEnhanceableCard(player))
        {
            result[0] =
                CreateEnhanceOption(player);
        }
        else
        {
            // 강화할 카드가 없으면 새 카드
            result[0] =
                CreateAddCardOption(player);
        }

        // 2번: 무조건 새 카드
        result[1] =
            CreateAddCardOption(player);

        // 3번: 50% 강화 / 50% 새 카드
        bool enhance =
            Random.value < 0.5f;

        if (enhance &&
            HasEnhanceableCard(player))
        {
            result[2] =
                CreateEnhanceOption(player);
        }
        else
        {
            result[2] =
                CreateAddCardOption(player);
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
                "[CardOptionGenerator] " +
                "Card Pool이 비어 있습니다."
            );

            return CreateSkipOption();
        }

        int cardPoolIndex =
            Random.Range(
                0,
                cardPool.Count
            );

        CardDefinition card =
            cardPool[
                cardPoolIndex
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

            // 현재는 null이 아닌 모든 카드를
            // 강화 가능 대상으로 취급
            result.Add(i);
        }

        return result;
    }


    // =========================================================
    // Network Card Pool Mapping
    // =========================================================

    public int GetCardPoolIndex(
        CardDefinition card)
    {
        if (card == null ||
            cardPool == null)
        {
            return -1;
        }

        return cardPool.IndexOf(
            card
        );
    }


    public CardDefinition GetCardByPoolIndex(
        int index)
    {
        if (cardPool == null)
        {
            return null;
        }

        if (index < 0 ||
            index >= cardPool.Count)
        {
            Debug.LogWarning(
                "[CardOptionGenerator] " +
                $"잘못된 CardPoolIndex: {index}"
            );

            return null;
        }

        return cardPool[
            index
        ];
    }

    public PreparationOptionNetData ToNetData(
    PreparationOption option)
{
    int cardPoolIndex = -1;

    if (option.Type ==
        PreparationOptionType.AddCard &&
        option.Card != null)
    {
        cardPoolIndex =
            GetCardPoolIndex(
                option.Card
            );
    }

    return new PreparationOptionNetData
    {
        Type =
            (byte)option.Type,

        CardPoolIndex =
            cardPoolIndex,

        TargetCardIndex =
            option.TargetCardIndex,

        EnhanceId =
            option.EnhanceId
    };
}
public PreparationOption FromNetData(
    PreparationOptionNetData data)
{
    PreparationOptionType type =
        (PreparationOptionType)data.Type;

    CardDefinition card = null;

    if (type ==
        PreparationOptionType.AddCard)
    {
        card =
            GetCardByPoolIndex(
                data.CardPoolIndex
            );
    }

    return new PreparationOption
    {
        Type =
            type,

        Card =
            card,

        TargetCardIndex =
            data.TargetCardIndex,

        EnhanceId =
            data.EnhanceId
    };
}

}