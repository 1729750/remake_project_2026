using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardVisual : MonoBehaviour
{
    private static GameObject _effectDisplayPrefab;

    private SpriteRenderer _background;
    private SpriteRenderer _sprite;
    private Transform _effectArea;
    private SpriteRenderer _effectAreaRenderer;
    private TextMeshPro _costText;
    private TextMeshPro _cooltimeText;
    private GameObject _selectHighlight;
    private GameObject _front;
    private GameObject _back;
    private CardInstance _cardInstance;
    private Coroutine _moveCoroutine;
    private readonly List<GameObject> _effectDisplays = new List<GameObject>();

    private void Awake()
    {
        _front              = transform.Find("Front").gameObject;
        _back               = transform.Find("Back").gameObject;
        _background         = transform.Find("Front/background").GetComponent<SpriteRenderer>();
        _sprite             = transform.Find("Front/sprite").GetComponent<SpriteRenderer>();
        _effectArea         = transform.Find("Front/effect");
        _effectAreaRenderer = _effectArea.GetComponent<SpriteRenderer>();
        _costText           = transform.Find("Front/cost/CostText").GetComponent<TextMeshPro>();
        _cooltimeText       = transform.Find("Front/cooltimeText").GetComponent<TextMeshPro>();
        _selectHighlight    = transform.Find("Front/SelectHighlight").gameObject;
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

        RefreshEffectDisplays(def);

        RefreshCooltime();
        SetFace(showFront);
    }

    private static GameObject GetEffectDisplayPrefab()
    {
        if (_effectDisplayPrefab == null)
            _effectDisplayPrefab = Resources.Load<GameObject>("Prefabs/EffectDisplay");
        return _effectDisplayPrefab;
    }

    // effect 영역을 3등분해서 왼쪽부터 EffectDisplay(아이콘+수치)를 채워 넣는다.
    private void RefreshEffectDisplays(CardDefinition def)
    {
        foreach (GameObject display in _effectDisplays)
            Destroy(display);
        _effectDisplays.Clear();

        GameObject effectDisplayPrefab = GetEffectDisplayPrefab();
        if (effectDisplayPrefab == null) return;

        float areaWidth = _effectAreaRenderer.sprite.bounds.size.x;
        float slotWidth = areaWidth / 3f;
        float leftEdge = -areaWidth / 2f;

        int index = 0;
        foreach (CardEffect cardEffect in def.GetEffects())
        {
            GameObject display = Instantiate(effectDisplayPrefab, _effectArea);
            _effectDisplays.Add(display);

            SpriteRenderer effectSprite = display.transform.Find("EffectSprite").GetComponent<SpriteRenderer>();
            effectSprite.sprite = BattleManager.GetEmoji(cardEffect.GetEffect().GetEffectType());

            int magnitude = cardEffect.GetEffect().GetMagnitude();
            TextMeshPro magnitudeText = display.transform.Find("MagnitudeText").GetComponent<TextMeshPro>();
            magnitudeText.text = magnitude <= -1 ? "" : magnitude.ToString();

            float nativeWidth = effectSprite.sprite != null ? effectSprite.sprite.bounds.size.x : slotWidth;
            float scale = nativeWidth > 0f ? slotWidth / nativeWidth : 1f;
            display.transform.localScale = Vector3.one * scale;
            display.transform.localPosition = new Vector3(leftEdge + slotWidth * (index + 0.5f), 0f, display.transform.localPosition.z);

            index++;
        }
    }

    public Vector2 GetBackgroundSize() => _background.bounds.size;

    // 자식에 있는 모든 Renderer(SpriteRenderer, TextMeshPro 내부 MeshRenderer 등)의 sortingLayer를
    // 한 번에 옮긴다. 자주 호출되는 경로가 아니라 필드별로 캐싱하지 않고 그때그때 순회한다.
    public void SetLayer(string sortingLayerName)
    {
        int sortingLayerID = SortingLayer.NameToID(sortingLayerName);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            renderer.sortingLayerID = sortingLayerID;
    }

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
