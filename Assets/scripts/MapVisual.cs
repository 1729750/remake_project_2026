using TMPro;
using UnityEngine;

// EnemyDisplay 프리팹 루트에 부착. MapManager가 넘겨주는 CharacterData를 기반으로
// AtkBar/DefBar 비율과 effectsText(이펙트 이모지 + 체력)를 갱신한다.
public class MapVisual : MonoBehaviour
{
    // AtkBar와 DefBar는 중심에서 좌우로 자라나는 한 쌍이라, 두 폭의 합이 이 값일 때 꽉 찬 것으로 본다.
    private const float MaxBarWidth = 2f;

    private SpriteRenderer _atkBar;
    private SpriteRenderer _defBar;
    private TextMeshPro _effectsText;
    private GameObject _highlight;

    private void Awake()
    {
        _atkBar = transform.Find("AtkDefBar/AtkBar").GetComponent<SpriteRenderer>();
        _defBar = transform.Find("AtkDefBar/DefBar").GetComponent<SpriteRenderer>();
        _effectsText = transform.Find("MainEffects/EffectsText").GetComponent<TextMeshPro>();
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

        var topEffects = MapManager.Instance.GetMostFrequentEffectType(data);
        int emojiCount = Mathf.Min(2, topEffects.Count);
        string emojis = "";
        for (int i = 0; i < emojiCount; i++)
            emojis += BattleManager.GetEmoji(topEffects[i]);

        _effectsText.text = emojis.Length > 0
            ? $"{emojis}\n{MapManager.Instance.GetHealth(data)}"
            : $"{MapManager.Instance.GetHealth(data)}";
    }
}
