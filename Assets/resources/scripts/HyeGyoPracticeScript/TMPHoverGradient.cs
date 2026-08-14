using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TMPHoverGradient : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private TMP_Text targetText;

    [Header("기본 상태")]
    [SerializeField] private Color normalTop = new Color(0.2f, 0.2f, 0.2f);
    [SerializeField] private Color normalBottom = new Color(0.7f, 0.7f, 0.7f);

    [Header("마우스 Hover 상태")]
    [SerializeField] private Color hoverTop = Color.white;
    [SerializeField] private Color hoverBottom = new Color(0.3f, 0.7f, 1f);

    [Header("전환 시간")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.15f;

    private Coroutine transitionCoroutine;

    private void Reset()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (targetText == null)
        {
            Debug.LogError("TMP_Text 컴포넌트를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        // 기본 색상이 그라데이션에 영향을 주지 않도록 흰색 사용
        targetText.color = Color.white;
        targetText.enableVertexGradient = true;

        ApplyGradient(normalTop, normalBottom);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartTransition(hoverTop, hoverBottom);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartTransition(normalTop, normalBottom);
    }

    private void StartTransition(Color top, Color bottom)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            AnimateGradient(CreateGradient(top, bottom))
        );
    }

    private IEnumerator AnimateGradient(VertexGradient targetGradient)
    {
        VertexGradient startGradient = targetText.colorGradient;

        if (transitionDuration <= 0f)
        {
            targetText.colorGradient = targetGradient;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            targetText.colorGradient = LerpGradient(
                startGradient,
                targetGradient,
                t
            );

            yield return null;
        }

        targetText.colorGradient = targetGradient;
        transitionCoroutine = null;
    }

    private void ApplyGradient(Color top, Color bottom)
    {
        targetText.colorGradient = CreateGradient(top, bottom);
    }

    private static VertexGradient CreateGradient(Color top, Color bottom)
    {
        return new VertexGradient(
            top,       // 왼쪽 위
            top,       // 오른쪽 위
            bottom,    // 왼쪽 아래
            bottom     // 오른쪽 아래
        );
    }

    private static VertexGradient LerpGradient(
        VertexGradient from,
        VertexGradient to,
        float t)
    {
        return new VertexGradient(
            Color.Lerp(from.topLeft, to.topLeft, t),
            Color.Lerp(from.topRight, to.topRight, t),
            Color.Lerp(from.bottomLeft, to.bottomLeft, t),
            Color.Lerp(from.bottomRight, to.bottomRight, t)
        );
    }
}