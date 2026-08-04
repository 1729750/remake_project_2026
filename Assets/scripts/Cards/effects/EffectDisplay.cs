using TMPro;
using UnityEngine;

// EffectDisplay 프리팹(EffectSprite+MagnitudeText) 루트에 부착.
// 아이콘/수치 세팅, 정렬 레이어 변경처럼 프리팹 내부 구조에 의존하는 로직을 한 곳에 모아
// CardVisual/RewardDisplay/MapVisual/CharacterManager는 배치(레이아웃) 계산만 신경 쓰면 되게 한다.
public class EffectDisplay : MonoBehaviour
{
    private static GameObject _prefab;

    private SpriteRenderer _effectSprite;
    private TMP_Text _magnitudeText;

    private void Awake()
    {
        _effectSprite = transform.Find("EffectSprite").GetComponent<SpriteRenderer>();
        _magnitudeText = transform.Find("MagnitudeText").GetComponent<TMP_Text>();
    }

    public static EffectDisplay Spawn(Transform parent)
    {
        if (_prefab == null)
            _prefab = Resources.Load<GameObject>("Prefabs/EffectDisplay");
        if (_prefab == null) return null;

        return Instantiate(_prefab, parent).GetComponent<EffectDisplay>();
    }

    public void SetEffect(EffectType effectType, string magnitudeText)
    {
        _effectSprite.sprite = BattleManager.GetEmoji(effectType);
        switch (effectType)
        {
            case EffectType.Disposable:
            case EffectType.Preserve: 
                _magnitudeText.text = ""; 
                break;
            default:
                _magnitudeText.text = magnitudeText;
                break;
        }
    }

    public Vector2 GetSpriteSize()
    {
        return _effectSprite.sprite != null ? (Vector2)_effectSprite.sprite.bounds.size : Vector2.zero;
    }

    public void SetScale(float scale) => transform.localScale = Vector3.one * scale;

    public void SetLocalPosition(Vector3 position) => transform.localPosition = position;

    public void SetSortingLayer(string layerName)
    {
        int layerID = SortingLayer.NameToID(layerName);
        _effectSprite.sortingLayerID = layerID;
        _magnitudeText.GetComponent<Renderer>().sortingLayerID = layerID;
    }
}
