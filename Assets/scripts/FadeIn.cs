using System.Collections;
using UnityEngine;

// 화면 전체를 덮는 검정 SpriteRenderer를 가진 게임 오브젝트에 붙여서 쓴다. 이 오브젝트는 평소
// 비활성 상태로 씬에 배치해두고, Play()를 호출하면 스스로를 활성화한 뒤 alpha를 1(불투명)로
// 초기화하고, duration(기본 2초)에 걸쳐 0(투명)까지 옅어지다가 다시 비활성화된다.
[RequireComponent(typeof(SpriteRenderer))]
public class FadeIn : MonoBehaviour
{
    [SerializeField] private float duration = 2f;

    private SpriteRenderer _spriteRenderer;
    private Coroutine _routine;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        // sprite가 따로 지정돼 있지 않으면 단색 흰 픽셀을 만들어 채운다 — 이 컴포넌트는 별도
        // 아트 에셋 없이도 그 자체로 화면을 덮는 단색 오버레이로 동작한다.
        if (_spriteRenderer.sprite == null)
            _spriteRenderer.sprite = CreateWhiteSprite();
    }

    public void Play()
    {
        gameObject.SetActive(true);
        _spriteRenderer.color = Color.black;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _spriteRenderer.color = new Color(0f, 0f, 0f, Mathf.Lerp(1f, 0f, elapsed / duration));
            yield return null;
        }

        _spriteRenderer.color = new Color(0f, 0f, 0f, 0f);
        _routine = null;
        gameObject.SetActive(false);
    }

    private static Sprite CreateWhiteSprite()
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
