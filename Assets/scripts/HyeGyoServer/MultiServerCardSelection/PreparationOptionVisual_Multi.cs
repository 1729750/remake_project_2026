using TMPro;
using UnityEngine;

public sealed class PreparationOptionVisual_Multi : MonoBehaviour
{
    private SpriteRenderer _attack;
    private SpriteRenderer _defense;

    private TextMeshPro _hpText;

    private EffectDisplay _mainEffect;
    private EffectDisplay _subEffect;

    private GameObject _highlight;

    private PreparationOption _option;


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
                attackTransform
                    .GetComponent<SpriteRenderer>();
        }

        if (defenseTransform != null)
        {
            _defense =
                defenseTransform
                    .GetComponent<SpriteRenderer>();
        }

        if (hpTextTransform != null)
        {
            _hpText =
                hpTextTransform
                    .GetComponent<TextMeshPro>();
        }

        if (mainEffectTransform != null)
        {
            _mainEffect =
                mainEffectTransform
                    .GetComponent<EffectDisplay>();
        }

        if (subEffectTransform != null)
        {
            _subEffect =
                subEffectTransform
                    .GetComponent<EffectDisplay>();
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
        _option = option;

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
                Clear();
                break;
        }
    }


    private void ShowAddCard(
        CardDefinition card)
    {
        if (card == null)
        {
            Clear();
            return;
        }

        RefreshFromCard(
            card
        );

        if (_hpText != null)
        {
            _hpText.text =
                "ADD";
        }
    }


    private void ShowEnhance(
        PreparationOption option)
    {
        // 실제 강화 대상 CardDefinition을
        // 다음 단계에서 전달받도록 연결 예정

        if (_hpText != null)
        {
            _hpText.text =
                "UP";
        }
    }


    private void RefreshFromCard(
        CardDefinition card)
    {
        if (card == null)
        {
            Clear();
            return;
        }

        int attackValue = 0;
        int defenseValue = 0;

        EffectType? mainType = null;
        EffectType? subType = null;

        foreach (
            CardEffect cardEffect
            in card.GetEffects())
        {
            if (cardEffect == null ||
                cardEffect.GetEffect() == null)
            {
                continue;
            }

            Effect effect =
                cardEffect.GetEffect();

            EffectType type =
                effect.GetEffectType();

            if (type ==
                EffectType.Attack)
            {
                attackValue +=
                    effect.GetMagnitude();

                continue;
            }

            if (type ==
                EffectType.Defend)
            {
                defenseValue +=
                    effect.GetMagnitude();

                continue;
            }

            if (mainType == null)
            {
                mainType = type;
            }
            else if (subType == null)
            {
                subType = type;
            }
        }


        if (_attack != null)
        {
            _attack.size =
                new Vector2(
                    ScoreToWidth(
                        attackValue
                    ),
                    _attack.size.y
                );
        }

        if (_defense != null)
        {
            _defense.size =
                new Vector2(
                    ScoreToWidth(
                        defenseValue
                    ),
                    _defense.size.y
                );
        }


        if (_mainEffect != null)
        {
            if (mainType.HasValue)
            {
                _mainEffect.SetEffect(
                    mainType.Value,
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
            if (subType.HasValue)
            {
                _subEffect.SetEffect(
                    subType.Value,
                    ""
                );
            }
            else
            {
                _subEffect.Clear();
            }
        }
    }


    private void Clear()
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

        _mainEffect?.Clear();
        _subEffect?.Clear();

        if (_hpText != null)
        {
            _hpText.text =
                string.Empty;
        }
    }


    private static float ScoreToWidth(
        float score)
    {
        const float firstBreakpointScore =
            100f;

        const float firstBreakpointWidth =
            1f;

        const float maxScore =
            500f;

        const float maxWidth =
            2.7f;


        if (score <= 0f)
            return 0f;

        if (score >= maxScore)
            return maxWidth;

        if (score <=
            firstBreakpointScore)
        {
            return
                score /
                firstBreakpointScore *
                firstBreakpointWidth;
        }

        float t =
            (score -
             firstBreakpointScore)
            /
            (maxScore -
             firstBreakpointScore);

        return
            firstBreakpointWidth +
            t *
            (maxWidth -
             firstBreakpointWidth);
    }
}