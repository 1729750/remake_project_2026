using System.Collections.Generic;
using UnityEngine;

// EnemyDisplay 프리팹 루트에 부착. MapManager가 넘겨주는 CharacterData를 기반으로
// AtkBar/DefBar 비율과 MainEffects(이펙트 아이콘) 를 갱신한다.
public class MapVisual : MonoBehaviour
{
    // AtkBar와 DefBar는 중심에서 좌우로 자라나는 한 쌍이라, 두 폭의 합이 이 값일 때 꽉 찬 것으로 본다.
    private const float MaxBarWidth = 2f;

    private SpriteRenderer _atkBar;
    private SpriteRenderer _defBar;
    private Transform _mainEffects;
    private SpriteRenderer _mainEffectsBackground;
    private GameObject _highlight;
    private readonly List<EffectDisplay> _effectDisplays = new List<EffectDisplay>();

    private void Awake()
    {
        _atkBar = transform.Find("AtkDefBar/AtkBar").GetComponent<SpriteRenderer>();
        _defBar = transform.Find("AtkDefBar/DefBar").GetComponent<SpriteRenderer>();
        _mainEffects = transform.Find("MainEffects");
        _mainEffectsBackground = _mainEffects.GetComponent<SpriteRenderer>();
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
        foreach (EffectDisplay display in _effectDisplays)
            Destroy(display.gameObject);
        _effectDisplays.Clear();

        int emojiCount = Mathf.Min(2, topEffects.Count);
        var widths = new float[emojiCount];
        var displays = new EffectDisplay[emojiCount];

        // effectDisplay는 MainEffects의 자식으로 스폰되므로 MainEffects의 자체 스케일(1.2배)을
        // 그대로 물려받는다. 그래서 목표 높이도 MainEffects의 월드 bounds(이미 그 1.2배가 반영된 값)가
        // 아니라 sprite 자체의(스케일 미반영) 크기를 써야 한다 — 안 그러면 자식에게 스케일을 한 번 더
        // 곱해 얹는 꼴이 되어(1.2배가 중복 적용) 이중으로 커진다. GetSpriteSize()도 동일하게
        // sprite.bounds(스케일 미반영)를 반환하므로 이렇게 해야 서로 같은 기준(스프라이트 원본 크기)으로
        // 비교된다.
        float targetHeight = _mainEffectsBackground != null && _mainEffectsBackground.sprite != null
            ? _mainEffectsBackground.sprite.bounds.size.y
            : 1f;

        for (int i = 0; i < emojiCount; i++)
        {
            EffectDisplay display = EffectDisplay.Spawn(_mainEffects);
            if (display == null) return;
            _effectDisplays.Add(display);
            displays[i] = display;

            display.SetEffect(topEffects[i], "");
            display.SetSortingLayer("UI");

            Vector2 spriteSize = display.GetSpriteSize();
            float nativeHeight = spriteSize.y > 0f ? spriteSize.y : targetHeight;
            float scale = nativeHeight > 0f ? targetHeight / nativeHeight : 1f;
            display.SetScale(scale);

            widths[i] = spriteSize.x * scale;
        }

        float totalWidth = 0f;
        foreach (float w in widths) totalWidth += w;

        float cursor = -totalWidth / 2f;
        for (int i = 0; i < emojiCount; i++)
        {
            float centerX = cursor + widths[i] / 2f;
            displays[i].SetLocalPosition(new Vector3(centerX, 0f, 0f));
            cursor += widths[i];
        }
    }
}
