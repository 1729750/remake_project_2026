using TMPro;
using UnityEngine;

// Prefabs/PopUpComponent 프리팹(배경 SpriteRenderer + Icon + Text) 루트에 부착.
// PopupManager가 씬에 고정 배치된 인스턴스 하나만 붙잡고 SetEffect로 아이콘/텍스트를 갈아 끼운다.
public class PopupComponent : MonoBehaviour
{
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
}
