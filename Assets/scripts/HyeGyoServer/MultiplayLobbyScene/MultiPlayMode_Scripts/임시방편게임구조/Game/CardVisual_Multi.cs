using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Multi 전용 CardVisual.
// Single CardVisual과 프리팹 구조는 동일하게 유지한다.
// Popup / EffectDisplay 참조만 _Multi 버전을 사용한다.
public class CardVisual_Multi : MonoBehaviour
{
    private SpriteRenderer _background;
    private SpriteRenderer _sprite;
    private SpriteRenderer _spriteBackground;
    private Transform _effectArea;
    private TextMeshPro _costText;
    private TextMeshPro _cooltimeText;
    private GameObject _selectHighlight;
    private GameObject _isUnplayable;
    private GameObject _front;
    private GameObject _back;
    private Coroutine _moveCoroutine;

    private readonly List<EffectDisplay_Multi>
        _effectDisplays =
            new List<EffectDisplay_Multi>();

    private PopupDisplay_Multi _popupDisplay;

    private bool _popupTrigger;
    private bool _selected;

    private List<EffectType>
        _pendingEffectTypes =
            new List<EffectType>();

    private void Awake()
    {
        _front =
            transform.Find("Front").gameObject;

        _back =
            transform.Find("Back").gameObject;

        _background =
            transform.Find("Front/background")
                .GetComponent<SpriteRenderer>();

        _sprite =
            transform.Find("Front/sprite")
                .GetComponent<SpriteRenderer>();

        _spriteBackground =
            transform
                .Find("Front/sprite/background")
                .GetComponent<SpriteRenderer>();

        _effectArea =
            transform.Find("Front/effect");

        _costText =
            transform
                .Find("Front/cost/CostText")
                .GetComponent<TextMeshPro>();

        _cooltimeText =
            transform
                .Find(
                    "Front/cooltime/cooltimeText")
                .GetComponent<TextMeshPro>();

        _selectHighlight =
            transform
                .Find("Front/SelectHighlight")
                .gameObject;

        _selectHighlight.SetActive(false);

        _isUnplayable =
            transform
                .Find("Front/IsUnplayable")
                .gameObject;

        _isUnplayable.SetActive(false);

        Transform popupTransform =
            transform.Find("PopUpDisplay");

        if (popupTransform != null)
        {
            _popupDisplay =
                popupTransform
                    .GetComponent<
                        PopupDisplay_Multi>();
        }

        if (_effectArea != null)
        {
            _effectDisplays.AddRange(
                _effectArea
                    .GetComponentsInChildren<
                        EffectDisplay_Multi>(
                        true));
        }
    }

    public void SetSelected(
        bool selected)
    {
        if (_selectHighlight != null)
        {
            _selectHighlight
                .SetActive(selected);
        }

        _selected = selected;

        RefreshPopupVisibility();
    }

    public void SetFace(bool front)
    {
        _front?.SetActive(front);
        _back?.SetActive(!front);
    }

    public void SetCardDefinition(
        CardDefinition def)
    {
        if (def == null)
            return;

        if (_sprite != null)
            _sprite.sprite =
                def.GetSprite();

        if (_spriteBackground != null)
        {
            _spriteBackground.sprite =
                def.GetSpriteBackground();
        }

        CardEffect[] effects =
            def.GetEffects();

        _pendingEffectTypes =
            new List<EffectType>(
                effects.Length);

        foreach (
            CardEffect cardEffect
            in effects)
        {
            if (cardEffect == null ||
                cardEffect.GetEffect() == null)
            {
                continue;
            }

            _pendingEffectTypes.Add(
                cardEffect
                    .GetEffect()
                    .GetEffectType());
        }

        RefreshPopupVisibility();
    }

    public void SetUnplayable(
        bool unplayable)
    {
        _isUnplayable?
            .SetActive(unplayable);
    }

    public void SetCostText(
        string text)
    {
        if (_costText != null)
            _costText.text = text;
    }

    public void SetCooldownText(
        string text)
    {
        if (_cooltimeText != null)
            _cooltimeText.text = text;
    }

    public void RefreshEffectDisplays(
        List<CardEffect_Multi> cardEffects)
    {
        for (int i = 0;
             i < _effectDisplays.Count;
             i++)
        {
            if (cardEffects != null &&
                i < cardEffects.Count)
            {
                CardEffect_Multi cardEffect =
                    cardEffects[i];

                if (cardEffect == null ||
                    cardEffect.GetEffect() == null)
                {
                    _effectDisplays[i].Clear();
                    continue;
                }

                int magnitude =
                    cardEffect.GetMagnitude();

                _effectDisplays[i].SetEffect(
                    cardEffect.GetEffect(),
                    magnitude <= -1
                        ? ""
                        : magnitude.ToString());
            }
            else
            {
                _effectDisplays[i].Clear();
            }
        }
    }

    public Vector2 GetBackgroundSize()
    {
        return _background != null
            ? _background.bounds.size
            : Vector2.zero;
    }

    public void SetLayer(
        string sortingLayerName)
    {
        _popupTrigger =
            sortingLayerName == "UI";

        RefreshPopupVisibility();

        int sortingLayerID =
            SortingLayer.NameToID(
                sortingLayerName);

        foreach (
            Renderer renderer
            in GetComponentsInChildren<
                Renderer>(true))
        {
            renderer.sortingLayerID =
                sortingLayerID;
        }
    }

    private void RefreshPopupVisibility()
    {
        if (_popupDisplay == null)
            return;

        _popupDisplay.SetEffects(
            _popupTrigger &&
            _selected
                ? _pendingEffectTypes
                : null);
    }

    public void SetSize(
        Vector2 targetSize)
    {
        if (_background == null)
            return;

        Vector2 currentSize =
            _background.bounds.size;

        if (currentSize.x == 0f ||
            currentSize.y == 0f)
        {
            return;
        }

        transform.localScale =
            new Vector3(
                transform.localScale.x *
                targetSize.x /
                currentSize.x,

                transform.localScale.y *
                targetSize.y /
                currentSize.y,

                transform.localScale.z);
    }

    public void MoveTo(
        Vector3 targetPosition,
        float duration = 0.3f)
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(
                _moveCoroutine);
        }

        _moveCoroutine =
            StartCoroutine(
                MoveRoutine(
                    targetPosition,
                    duration));
    }

    private IEnumerator MoveRoutine(
        Vector3 targetPosition,
        float duration)
    {
        Vector3 startPosition =
            transform.position;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(
                        elapsed /
                        duration));

            transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    t);

            yield return null;
        }

        transform.position =
            targetPosition;

        _moveCoroutine = null;
    }
}
