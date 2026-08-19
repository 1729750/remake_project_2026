using TMPro;
using UnityEngine;

// Prefabs/PopUpComponent 프리팹(배경 SpriteRenderer + Icon + Text) 루트에 부착.
// PopupDisplay 밑에 고정 슬롯으로 미리 배치되어 있고, PopupDisplay가 필요한 만큼만 enable하면서
// SetEffect로 아이콘/텍스트를 채워 넣는다.
public class PopupComponent : MonoBehaviour
{
    // 배경 SpriteRenderer가 루트가 아니라 자식에 있어서 GetComponent(this)로는 못 찾는다 —
    // 프리팹에서 인스펙터로 직접 연결해 준다.
    [SerializeField] private SpriteRenderer background;

    private SpriteRenderer _icon;
    private TMP_Text _text;

    private void Awake()
    {
        _icon = transform.Find("Icon").GetComponent<SpriteRenderer>();
        _text = transform.Find("Text").GetComponent<TMP_Text>();
    }

    public void SetEffect(Sprite icon, string text)
    {
        _icon.sprite = icon;
        _text.text = text;
    }

    // 자신(배경 SpriteRenderer)의 bounds가 camera 화면 밖으로 잘리는지 여부.
    public bool IsClipped(Camera camera)
    {
        if (camera == null || background == null) return false;

        Bounds bounds = background.bounds;
        Vector3 vpMin = camera.WorldToViewportPoint(bounds.min);
        Vector3 vpMax = camera.WorldToViewportPoint(bounds.max);
        return vpMin.x < 0f || vpMin.y < 0f || vpMax.x > 1f || vpMax.y > 1f;
    }
}
