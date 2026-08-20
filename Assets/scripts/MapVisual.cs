using System.Collections.Generic;
using TMPro;
using UnityEngine;

// EnemyDisplay 프리팹 루트에 부착. MapManager가 넘겨주는 CharacterData를 기반으로
// Attack/Defense sprite 너비, HP 텍스트, MainEffect/SubEffect(효과 아이콘 2슬롯)를 갱신한다.
public class MapVisual : MonoBehaviour
{
    // score(해당 이펙트가 있는 카드 수 x 그 이펙트의 magnitude 합)를 Attack/Defense sprite
    // 너비로 바꾸는 구간별 선형 매핑. score 0에서 너비 0, score 100에서 너비 1,
    // score 500(이상)에서 너비 2.7(최대)에 도달한다 — 초반 구간의 기울기가 더 가파르다.
    private const float FirstBreakpointScore = 100f;
    private const float FirstBreakpointWidth = 1f;
    private const float MaxScore = 500f;
    private const float MaxWidth = 2.7f;

    private SpriteRenderer _attack;
    private SpriteRenderer _defense;
    private TextMeshPro _hpText;
    private EffectDisplay _mainEffect;
    private EffectDisplay _subEffect;
    private GameObject _highlight;
    private PopupDisplay _popupDisplay;
    private bool _selected;
    private List<EffectType> _pendingPopupEffects;

    private void Awake()
    {
        _attack = transform.Find("Attack").GetComponent<SpriteRenderer>();
        _defense = transform.Find("Defense").GetComponent<SpriteRenderer>();
        _hpText = transform.Find("HP/HPText").GetComponent<TextMeshPro>();
        _mainEffect = transform.Find("MainEffect").GetComponent<EffectDisplay>();
        _subEffect = transform.Find("SubEffect").GetComponent<EffectDisplay>();
        _highlight = transform.Find("HighLight").gameObject;
        _highlight.SetActive(false);
        _popupDisplay = transform.Find("PopUpDisplay").GetComponent<PopupDisplay>();
    }

    public void SetSelected(bool selected)
    {
        _highlight.SetActive(selected);
        _selected = selected;
        RefreshPopupVisibility();
    }

    public void SetCharacter(CharacterData data)
    {
        if (data == null || MapManager.Instance == null) return;

        float attackWidth = ScoreToWidth(MapManager.Instance.GetAttackWeight(data));
        float defenseWidth = ScoreToWidth(MapManager.Instance.GetDefenseWeight(data));

        _attack.size = new Vector2(attackWidth, _attack.size.y);
        _defense.size = new Vector2(defenseWidth, _defense.size.y);

        if (_hpText != null)
            _hpText.text = MapManager.Instance.GetHealth(data).ToString();

        RefreshEffectDisplays(MapManager.Instance.GetMostFrequentEffectType(data));
    }

    // score를 0~MaxWidth 사이 너비로 바꾼다. 0~FirstBreakpointScore 구간과
    // FirstBreakpointScore~MaxScore 구간의 기울기가 서로 달라(초반이 더 가파르다) 구간별로 따로
    // 선형보간한다. MaxScore 이상이면 MaxWidth로 고정(clamp)한다.
    private static float ScoreToWidth(float score)
    {
        if (score <= 0f) return 0f;
        if (score >= MaxScore) return MaxWidth;

        if (score <= FirstBreakpointScore)
            return score / FirstBreakpointScore * FirstBreakpointWidth;

        float t = (score - FirstBreakpointScore) / (MaxScore - FirstBreakpointScore);
        return FirstBreakpointWidth + t * (MaxWidth - FirstBreakpointWidth);
    }

    // 1번째 effect는 MainEffect, 2번째 effect는 SubEffect에 표시한다(수치 텍스트는 항상 비워 둔다).
    // topEffects가 그보다 적으면 남는 슬롯은 비운다.
    private void RefreshEffectDisplays(List<EffectType> topEffects)
    {
        _pendingPopupEffects = topEffects;
        RefreshPopupVisibility();

        if (topEffects != null && topEffects.Count > 0)
            _mainEffect.SetEffect(topEffects[0], "");
        else
            _mainEffect.Clear();

        if (topEffects != null && topEffects.Count > 1)
            _subEffect.SetEffect(topEffects[1], "");
        else
            _subEffect.Clear();
    }

    // select된 동안에만 팝업을 보여준다.
    private void RefreshPopupVisibility()
    {
        _popupDisplay.SetEffects(_selected ? _pendingPopupEffects : null);
    }
}
