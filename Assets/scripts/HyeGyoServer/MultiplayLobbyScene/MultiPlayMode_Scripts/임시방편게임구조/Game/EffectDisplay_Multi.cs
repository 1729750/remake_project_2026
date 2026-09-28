using TMPro;
using UnityEngine;

// Multi 전용 EffectDisplay.
// 레이아웃/표시 구조는 Single과 동일하고,
// Emoji 조회만 BattleManager_Multi를 사용한다.
public class EffectDisplay_Multi : MonoBehaviour
{
    private static GameObject _prefab;

    private SpriteRenderer _effectSprite;
    private TMP_Text _magnitudeText;

    private void Awake()
    {
        _effectSprite =
            transform.Find("EffectSprite")
                .GetComponent<SpriteRenderer>();

        _magnitudeText =
            transform.Find("MagnitudeText")
                .GetComponent<TMP_Text>();
    }

    public static EffectDisplay_Multi Spawn(
        Transform parent)
    {
        if (_prefab == null)
        {
            _prefab =
                Resources.Load<GameObject>(
                    "Prefabs/EffectDisplay_Multi");
        }

        if (_prefab == null)
        {
            Debug.LogError(
                "[EffectDisplay_Multi] " +
                "Resources/Prefabs/EffectDisplay_Multi 프리팹을 찾지 못했습니다.");

            return null;
        }

        return Instantiate(
                _prefab,
                parent)
            .GetComponent<EffectDisplay_Multi>();
    }

    public void SetEffect(
        Effect effect,
        string magnitudeText)
    {
        if (effect == null)
        {
            Clear();
            return;
        }

        _effectSprite.sprite =
            BattleManager_Multi.GetEmoji(
                effect.GetEffectType());

        _magnitudeText.text =
            effect.DoesntUseMagnitude
                ? ""
                : magnitudeText;
    }

    public void SetEffect(
        EffectType effectType,
        string magnitudeText)
    {
        _effectSprite.sprite =
            BattleManager_Multi.GetEmoji(
                effectType);

        _magnitudeText.text =
            magnitudeText;
    }

    public void Clear()
    {
        _effectSprite.sprite = null;
        _magnitudeText.text = "";
    }

    public Vector2 GetSpriteSize()
    {
        return _effectSprite.sprite != null
            ? (Vector2)_effectSprite.sprite.bounds.size
            : Vector2.zero;
    }

    public void SetScale(float scale)
    {
        transform.localScale =
            Vector3.one * scale;
    }

    public void SetLocalPosition(
        Vector3 position)
    {
        transform.localPosition =
            position;
    }

    public void SetSortingLayer(
        string layerName)
    {
        int layerID =
            SortingLayer.NameToID(
                layerName);

        _effectSprite.sortingLayerID =
            layerID;

        _magnitudeText
            .GetComponent<Renderer>()
            .sortingLayerID =
            layerID;
    }
}
