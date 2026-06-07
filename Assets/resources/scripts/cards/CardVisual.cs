using System.Text;
using TMPro;
using UnityEngine;

public class CardVisual : MonoBehaviour
{
    private SpriteRenderer _background;
    private SpriteRenderer _sprite;
    private TextMeshPro _effectText;
    private TextMeshPro _costText;
    private TextMeshPro _cooltimeText;
    private GameObject _selectHighlight;
    private CardInstance _cardInstance;

    private void Awake()
    {
        _background      = transform.Find("background").GetComponent<SpriteRenderer>();
        _sprite          = transform.Find("sprite").GetComponent<SpriteRenderer>();
        _effectText      = transform.Find("effect/effectText").GetComponent<TextMeshPro>();
        _costText        = transform.Find("cost/CostText").GetComponent<TextMeshPro>();
        _cooltimeText    = transform.Find("cooltimeText").GetComponent<TextMeshPro>();
        _selectHighlight = transform.Find("background/SelectHighlight").gameObject;
        _selectHighlight.SetActive(false);
    }

    public void SetSelected(bool selected) => _selectHighlight.SetActive(selected);

    public void SetCard(CardInstance cardInstance)
    {
        _cardInstance = cardInstance;
        CardDefinition def = cardInstance.GetDefinition();

        _background.color = def.GetCardType() == CardType.Attack ? Color.red : Color.blue;
        _sprite.sprite = def.GetSprite();
        _costText.text = def.GetCost().ToString();

        var sb = new StringBuilder();
        foreach (CardEffect cardEffect in def.GetEffects())
        {
            string emoji = GameManager.Instance.GetEmoji(cardEffect.GetEffect().GetEffectType());
            int magnitude = cardEffect.GetEffect().GetMagnitude();
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{emoji}:{magnitude}");
        }
        _effectText.text = sb.ToString();

        FitToParent();
        RefreshCooltime();
    }

    private void FitToParent()
    {
        if (transform.parent == null) return;
        SpriteRenderer parentSR = transform.parent.GetComponent<SpriteRenderer>();
        if (parentSR == null) return;

        Vector2 targetSize = parentSR.bounds.size;
        Vector2 currentSize = _background.bounds.size;
        if (currentSize.x == 0f || currentSize.y == 0f) return;

        transform.localScale = new Vector3(
            transform.localScale.x * targetSize.x / currentSize.x,
            transform.localScale.y * targetSize.y / currentSize.y,
            transform.localScale.z
        );
    }

    private void Update()
    {
        if (_cardInstance != null)
            RefreshCooltime();
    }

    private void RefreshCooltime()
    {
        _cooltimeText.text = $"⏱:{_cardInstance.GetCooldownLeft()}";
    }
}
