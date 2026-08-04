using System.Collections.Generic;
using TMPro;
using UnityEngine;

// EnemyDisplay 프리팹 루트에 부착. MapManager가 넘겨주는 CharacterData를 기반으로
// AtkBar/DefBar 비율과 MainEffects(이펙트 아이콘) 를 갱신한다.
public class MapVisual : MonoBehaviour
{
    // AtkBar와 DefBar는 중심에서 좌우로 자라나는 한 쌍이라, 두 폭의 합이 이 값일 때 꽉 찬 것으로 본다.
    private const float MaxBarWidth = 2f;

    private static GameObject _effectDisplayPrefab;

    private SpriteRenderer _atkBar;
    private SpriteRenderer _defBar;
    private Transform _mainEffects;
    private GameObject _highlight;
    private readonly List<GameObject> _effectDisplays = new List<GameObject>();

    private void Awake()
    {
        _atkBar = transform.Find("AtkDefBar/AtkBar").GetComponent<SpriteRenderer>();
        _defBar = transform.Find("AtkDefBar/DefBar").GetComponent<SpriteRenderer>();
        _mainEffects = transform.Find("MainEffects");
        _highlight = transform.Find("HighLight").gameObject;
        _highlight.SetActive(false);
    }

    public void SetSelected(bool selected) => _highlight.SetActive(selected);

    public void SetCharacter(CharacterData data)
    {
        if (data == null || MapManager.Instance == null) return;

        int atkWeight = MapManager.Instance.GetAttackWeight(data);
        int defWeight = MapManager.Instance.GetDefenseWeight(data);
        int totalWeight = atkWeight + defWeight;

        float atkWidth = totalWeight > 0 ? MaxBarWidth * atkWeight / totalWeight : MaxBarWidth * 0.5f;
        float defWidth = MaxBarWidth - atkWidth;

        _atkBar.size = new Vector2(atkWidth, _atkBar.size.y);
        _defBar.size = new Vector2(defWidth, _defBar.size.y);

        RefreshEffectDisplays(MapManager.Instance.GetMostFrequentEffectType(data));
    }

    // MainEffects 아래에 effectDisplay(아이콘)를 나란히 배치한다. 수치 텍스트는 항상 비워 둔다.
    private void RefreshEffectDisplays(List<EffectType> topEffects)
    {
        foreach (GameObject display in _effectDisplays)
            Destroy(display);
        _effectDisplays.Clear();

        GameObject effectDisplayPrefab = GetEffectDisplayPrefab();
        if (effectDisplayPrefab == null) return;

        int emojiCount = Mathf.Min(2, topEffects.Count);
        var widths = new float[emojiCount];
        var displays = new GameObject[emojiCount];

        for (int i = 0; i < emojiCount; i++)
        {
            GameObject display = Instantiate(effectDisplayPrefab, _mainEffects);
            _effectDisplays.Add(display);
            displays[i] = display;

            SpriteRenderer effectSprite = display.transform.Find("EffectSprite").GetComponent<SpriteRenderer>();
            effectSprite.sprite = BattleManager.GetEmoji(topEffects[i]);

            TextMeshPro magnitudeText = display.transform.Find("MagnitudeText").GetComponent<TextMeshPro>();
            magnitudeText.text = "";

            // effectDisplay는 EffectSprite/MagnitudeText 둘뿐으로 구조가 고정이라 재귀 없이 직접 UI 레이어로 옮긴다.
            int uiLayerID = SortingLayer.NameToID("UI");
            effectSprite.sortingLayerID = uiLayerID;
            magnitudeText.GetComponent<Renderer>().sortingLayerID = uiLayerID;

            widths[i] = effectSprite.sprite != null ? effectSprite.sprite.bounds.size.x * display.transform.localScale.x : 0f;
        }

        float totalWidth = 0f;
        foreach (float w in widths) totalWidth += w;

        float cursor = -totalWidth / 2f;
        for (int i = 0; i < emojiCount; i++)
        {
            float centerX = cursor + widths[i] / 2f;
            displays[i].transform.localPosition = new Vector3(centerX, 0f, displays[i].transform.localPosition.z);
            cursor += widths[i];
        }
    }

    private static GameObject GetEffectDisplayPrefab()
    {
        if (_effectDisplayPrefab == null)
            _effectDisplayPrefab = Resources.Load<GameObject>("Prefabs/EffectDisplay");
        return _effectDisplayPrefab;
    }
}
