using System.Collections;
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
    private Coroutine _moveCoroutine;

    private void Awake()
    {
        _front           = transform.Find("Front").gameObject;
        _back            = transform.Find("Back").gameObject;
        _background      = transform.Find("Front/background").GetComponent<SpriteRenderer>();
        _sprite          = transform.Find("Front/sprite").GetComponent<SpriteRenderer>();
        _effectText      = transform.Find("Front/effect/effectText").GetComponent<TextMeshPro>();
        _costText        = transform.Find("Front/cost/CostText").GetComponent<TextMeshPro>();
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

       // _background.color = def.GetCardType() == CardType.Attack ? Color.red : Color.blue;
        _sprite.sprite = def.GetSprite();
        _costText.text = def.GetCost().ToString();

        var sb = new StringBuilder();
        foreach (CardEffect cardEffect in def.GetEffects())
        {
            string emoji = BattleManager.GetEmoji(cardEffect.GetEffect().GetEffectType());
            int magnitude = cardEffect.GetEffect().GetMagnitude();
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{emoji}:{magnitude}");
        }
        _effectText.text = sb.ToString();

        RefreshCooltime();
        SetFace(showFront);
    }

    public Vector2 GetBackgroundSize() => _background.bounds.size;

    // 카드 배경(SpriteRenderer)의 월드 크기가 targetSize가 되도록 균등하지 않게(가로/세로 개별) 스케일한다.
    public void SetSize(Vector2 targetSize)
    {
        Vector2 currentSize = _background.bounds.size;
        if (currentSize.x == 0f || currentSize.y == 0f) return;

        transform.localScale = new Vector3(
            transform.localScale.x * targetSize.x / currentSize.x,
            transform.localScale.y * targetSize.y / currentSize.y,
            transform.localScale.z
        );
    }

    public void MoveTo(Vector3 targetPosition, float duration = 0.3f)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveRoutine(targetPosition, duration));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        _moveCoroutine = null;
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
