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
    private GameObject _front;
    private GameObject _back;
    private CardInstance _cardInstance;

    private void Awake()
    {
        _front           = transform.Find("Front").gameObject;
        _back            = transform.Find("Back").gameObject;
        _background      = transform.Find("Front/background").GetComponent<SpriteRenderer>();
        _sprite          = transform.Find("Front/sprite").GetComponent<SpriteRenderer>();
        _effectText      = transform.Find("Front/effectText").GetComponent<TextMeshPro>();
        _costText        = transform.Find("Front/CostText").GetComponent<TextMeshPro>();
        _cooltimeText    = transform.Find("Front/cooltimeText").GetComponent<TextMeshPro>();
        _selectHighlight = transform.Find("Front/SelectHighlight").gameObject;
        _selectHighlight.SetActive(false);
    }

    public void SetSelected(bool selected) => _selectHighlight.SetActive(selected);

    public void SetFace(bool front)
    {
        _front.SetActive(front);
        _back.SetActive(!front);
    }

    public void SetCard(CardInstance cardInstance, bool showFront)
    {
        _cardInstance = cardInstance;
        CardDefinition def = cardInstance.GetDefinition();

        _background.color = def.GetCardType() == CardType.Attack ? Color.red : Color.blue;
        _sprite.sprite = def.GetSprite();
        _costText.text = def.GetCost().ToString();

        var sb = new StringBuilder();
        foreach (CardEffect cardEffect in def.GetEffects())
        {
            string emoji = BattleManager.Instance.GetEmoji(cardEffect.GetEffect().GetEffectType());
            int magnitude = cardEffect.GetEffect().GetMagnitude();
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{emoji}:{magnitude}");
        }
        _effectText.text = sb.ToString();

        FitToParent();
        RefreshCooltime();
        SetFace(showFront);
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
