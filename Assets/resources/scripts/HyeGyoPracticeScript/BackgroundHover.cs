using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class BackgroundHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("배경")]
    [SerializeField] private Image background;

    [Header("색상")]
    [SerializeField] private Color normalColor =
        new Color(1f, 1f, 1f, 0f);

    [SerializeField] private Color hoverColor =
        new Color(0.25f, 0.5f, 1f, 0.35f);

    [Header("전환 시간")]
    [SerializeField, Min(0f)]
    private float transitionDuration = 0.15f;

    private Coroutine transitionCoroutine;

    private void Reset()
    {
        background = GetComponent<Image>();
    }

    private void Awake()
    {
        if (background == null)
            background = GetComponent<Image>();

        background.color = normalColor;
        background.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartTransition(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartTransition(normalColor);
    }

    private void StartTransition(Color targetColor)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            AnimateColor(targetColor)
        );
    }

    private IEnumerator AnimateColor(Color targetColor)
    {
        Color startColor = background.color;

        if (transitionDuration <= 0f)
        {
            background.color = targetColor;
            transitionCoroutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / transitionDuration
            );

            t = Mathf.SmoothStep(0f, 1f, t);
            background.color = Color.Lerp(
                startColor,
                targetColor,
                t
            );

            yield return null;
        }

        background.color = targetColor;
        transitionCoroutine = null;
    }
}