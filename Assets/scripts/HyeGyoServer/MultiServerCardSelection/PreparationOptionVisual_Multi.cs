using TMPro;
using UnityEngine;

public sealed class PreparationOptionVisual_Multi : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer cardSprite;

    [SerializeField]
    private TextMeshPro titleText;

    [SerializeField]
    private TextMeshPro descriptionText;

    [SerializeField]
    private GameObject highlight;

    private PreparationOption currentOption;

    public void SetOption(
        PreparationOption option)
    {
        currentOption = option;

        switch (option.Type)
        {
            case PreparationOptionType.AddCard:
                ShowCardOption(option.Card);
                break;

            case PreparationOptionType.EnhanceCard:
                ShowEnhanceOption(option);
                break;

            case PreparationOptionType.Skip:
                ShowSkipOption();
                break;
        }
    }

    public void SetSelected(
        bool selected)
    {
        if (highlight != null)
        {
            highlight.SetActive(selected);
        }
    }

    private void ShowCardOption(
        CardDefinition card)
    {
        if (card == null)
        {
            Clear();
            return;
        }

        titleText.text =
            card.name;

        descriptionText.text =
            "카드 추가";

        // 예:
        // cardSprite.sprite =
        //     card.GetSprite();
    }

    private void ShowEnhanceOption(
        PreparationOption option)
    {
        titleText.text =
            "카드 강화";

        descriptionText.text =
            $"대상 Index: {option.TargetCardIndex}";
    }

    private void ShowSkipOption()
    {
        titleText.text =
            "건너뛰기";

        descriptionText.text =
            "이번 선택을 건너뜁니다.";

        if (cardSprite != null)
        {
            cardSprite.sprite = null;
        }
    }

    private void Clear()
    {
        titleText.text =
            string.Empty;

        descriptionText.text =
            string.Empty;

        if (cardSprite != null)
        {
            cardSprite.sprite = null;
        }
    }
}