using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class PreparationOptionVisual_Multi
    : MonoBehaviour
{
    private const float FirstBreakpointScore = 100f;
    private const float FirstBreakpointWidth = 1f;
    private const float MaxScore = 500f;
    private const float MaxWidth = 2.7f;

    private SpriteRenderer _attack;
    private SpriteRenderer _defense;

    private TextMeshPro _hpText;

    private EffectDisplay _mainEffect;
    private EffectDisplay _subEffect;

    private GameObject _highlight;


    private void Awake()
    {
        Transform attackTransform =
            transform.Find("Attack");

        Transform defenseTransform =
            transform.Find("Defense");

        Transform hpTextTransform =
            transform.Find("HP/HPText");

        Transform mainEffectTransform =
            transform.Find("MainEffect");

        Transform subEffectTransform =
            transform.Find("SubEffect");

        Transform highlightTransform =
            transform.Find("HighLight");


        if (attackTransform != null)
        {
            _attack =
                attackTransform.GetComponent<
                    SpriteRenderer
                >();
        }

        if (defenseTransform != null)
        {
            _defense =
                defenseTransform.GetComponent<
                    SpriteRenderer
                >();
        }

        if (hpTextTransform != null)
        {
            _hpText =
                hpTextTransform.GetComponent<
                    TextMeshPro
                >();
        }

        if (mainEffectTransform != null)
        {
            _mainEffect =
                mainEffectTransform.GetComponent<
                    EffectDisplay
                >();
        }

        if (subEffectTransform != null)
        {
            _subEffect =
                subEffectTransform.GetComponent<
                    EffectDisplay
                >();
        }

        if (highlightTransform != null)
        {
            _highlight =
                highlightTransform.gameObject;

            _highlight.SetActive(false);
        }
    }


    public void SetSelected(
        bool selected)
    {
        if (_highlight != null)
        {
            _highlight.SetActive(
                selected
            );
        }
    }


    public void SetOption(
        PreparationOption option)
    {
        Debug.Log(
            "[PreparationOptionVisual_Multi] " +
            $"SetOption | " +
            $"Type: {option.Type} | " +
            $"Card: {(option.Card != null ? option.Card.name : "NULL")} | " +
            $"TargetIndex: {option.TargetCardIndex}"
        );

        switch (option.Type)
        {
            case PreparationOptionType.AddCard:

                ShowAddCard(
                    option.Card
                );

                break;


            case PreparationOptionType.EnhanceCard:

                ShowEnhance(
                    option
                );

                break;


            case PreparationOptionType.Skip:

                ClearVisual();

                if (_hpText != null)
                {
                    _hpText.text =
                        "SKIP";
                }

                break;
        }
    }


    // =========================================================
    // Add Card
    // =========================================================

    private void ShowAddCard(
        CardDefinition card)
    {
        if (card == null)
        {
            Debug.LogWarning(
                "[PreparationOptionVisual_Multi] " +
                "AddCard인데 Card가 NULL입니다."
            );

            ClearVisual();
            return;
        }

        ShowCard(
            card
        );

        if (_hpText != null)
        {
            _hpText.text =
                "ADD";
        }
    }


    // =========================================================
    // Enhance
    // =========================================================

    private void ShowEnhance(
        PreparationOption option)
    {
        /*
         * 현재 네트워크 데이터에는
         * TargetCardIndex만 들어 있고
         * 강화 대상의 CardDefinition 자체는
         * 아직 Client에 동기화되지 않는다.
         *
         * 다음 단계에서 Local FinalDeck 동기화를 붙이면
         * 여기서 TargetCardIndex로 실제 카드를 찾아
         * ShowCard()를 호출하게 된다.
         */

        ClearVisual();

        if (_hpText != null)
        {
            _hpText.text =
                "UP";
        }

        Debug.Log(
            "[PreparationOptionVisual_Multi] " +
            $"강화 옵션 표시 | " +
            $"TargetCardIndex: {option.TargetCardIndex} | " +
            $"EnhanceId: {option.EnhanceId}"
        );
    }


    // =========================================================
    // Card Visual
    // =========================================================

    private void ShowCard(
        CardDefinition card)
    {
        if (card == null)
        {
            ClearVisual();
            return;
        }

        int attackScore =
            GetEffectWeight(
                card,
                EffectType.Attack
            );

        int defenseScore =
            GetEffectWeight(
                card,
                EffectType.Defend
            );

        if (_attack != null)
        {
            _attack.size =
                new Vector2(
                    ScoreToWidth(
                        attackScore
                    ),
                    _attack.size.y
                );
        }

        if (_defense != null)
        {
            _defense.size =
                new Vector2(
                    ScoreToWidth(
                        defenseScore
                    ),
                    _defense.size.y
                );
        }

        List<EffectType> topEffects =
            GetMostFrequentEffectTypes(
                card
            );

        RefreshEffectDisplays(
            topEffects
        );

        Debug.Log(
            "[PreparationOptionVisual_Multi] " +
            $"카드 표시 | " +
            $"Card: {card.name} | " +
            $"Attack: {attackScore} | " +
            $"Defense: {defenseScore}"
        );
    }


    // =========================================================
    // Effect Analysis
    // =========================================================

    private static int GetEffectWeight(
        CardDefinition card,
        EffectType targetType)
    {
        if (card == null)
            return 0;

        CardEffect[] effects =
            card.GetEffects();

        if (effects == null)
            return 0;

        int sum = 0;
        int count = 0;

        foreach (
            CardEffect cardEffect
            in effects)
        {
            if (cardEffect == null)
                continue;

            Effect effect =
                cardEffect.GetEffect();

            if (effect == null)
                continue;

            if (effect.GetEffectType() !=
                targetType)
            {
                continue;
            }

            sum +=
                effect.GetMagnitude();

            count++;
        }

        return sum * count;
    }


    private static List<EffectType>
        GetMostFrequentEffectTypes(
            CardDefinition card)
    {
        if (card == null)
        {
            return new List<EffectType>();
        }

        CardEffect[] effects =
            card.GetEffects();

        if (effects == null)
        {
            return new List<EffectType>();
        }

        Dictionary<
            EffectType,
            int
        > magnitudeSums =
            new Dictionary<
                EffectType,
                int
            >();

        Dictionary<
            EffectType,
            int
        > counts =
            new Dictionary<
                EffectType,
                int
            >();

        foreach (
            CardEffect cardEffect
            in effects)
        {
            if (cardEffect == null)
                continue;

            Effect effect =
                cardEffect.GetEffect();

            if (effect == null)
                continue;

            EffectType type =
                effect.GetEffectType();

            if (type ==
                    EffectType.Attack ||
                type ==
                    EffectType.Defend)
            {
                continue;
            }

            int magnitude =
                effect.GetMagnitude();

            if (magnitudeSums.TryGetValue(
                    type,
                    out int sum))
            {
                magnitudeSums[type] =
                    sum + magnitude;
            }
            else
            {
                magnitudeSums[type] =
                    magnitude;
            }

            if (counts.TryGetValue(
                    type,
                    out int count))
            {
                counts[type] =
                    count + 1;
            }
            else
            {
                counts[type] = 1;
            }
        }

        return counts.Keys
            .OrderByDescending(
                type =>
                    magnitudeSums[type] *
                    counts[type]
            )
            .Take(2)
            .ToList();
    }


    // =========================================================
    // Effect Display
    // =========================================================

    private void RefreshEffectDisplays(
        List<EffectType> effects)
    {
        if (_mainEffect != null)
        {
            if (effects != null &&
                effects.Count >= 1)
            {
                _mainEffect.SetEffect(
                    effects[0],
                    ""
                );
            }
            else
            {
                _mainEffect.Clear();
            }
        }

        if (_subEffect != null)
        {
            if (effects != null &&
                effects.Count >= 2)
            {
                _subEffect.SetEffect(
                    effects[1],
                    ""
                );
            }
            else
            {
                _subEffect.Clear();
            }
        }
    }


    // =========================================================
    // Clear
    // =========================================================

    private void ClearVisual()
    {
        if (_attack != null)
        {
            _attack.size =
                new Vector2(
                    0f,
                    _attack.size.y
                );
        }

        if (_defense != null)
        {
            _defense.size =
                new Vector2(
                    0f,
                    _defense.size.y
                );
        }

        if (_mainEffect != null)
        {
            _mainEffect.Clear();
        }

        if (_subEffect != null)
        {
            _subEffect.Clear();
        }

        if (_hpText != null)
        {
            _hpText.text =
                string.Empty;
        }
    }


    // =========================================================
    // Width
    // =========================================================

    private static float ScoreToWidth(
        float score)
    {
        if (score <= 0f)
        {
            return 0f;
        }

        if (score >= MaxScore)
        {
            return MaxWidth;
        }

        if (score <=
            FirstBreakpointScore)
        {
            return
                score /
                FirstBreakpointScore *
                FirstBreakpointWidth;
        }

        float t =
            (score -
             FirstBreakpointScore)
            /
            (MaxScore -
             FirstBreakpointScore);

        return
            FirstBreakpointWidth +
            t *
            (MaxWidth -
             FirstBreakpointWidth);
    }
}