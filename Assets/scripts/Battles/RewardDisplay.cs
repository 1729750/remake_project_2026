using TMPro;
using UnityEngine;

// RewardDisplay 프리팹 루트에 부착. Card/HighLight를 켜고 끄는 것과 RewardSprite/RewardText 초기화를 담당한다.
public class RewardDisplay : MonoBehaviour
{
    private GameObject _highlight;
    private SpriteRenderer _rewardSprite;
    private TMP_Text _rewardText;
    private EffectDisplay _effectDisplay;

    private void Awake()
    {
        _highlight = transform.Find("Card/HighLight").gameObject;
        _highlight.SetActive(false);

        _rewardSprite = transform.Find("RewardSprite").GetComponent<SpriteRenderer>();
        _rewardText = transform.Find("RewardText").GetComponent<TMP_Text>();
    }

    public void Init(string rewardText)
    {
        ClearEffect();
        _rewardText.text = rewardText;
    }

    // 강화 후보 표시: RewardText는 비워 두고, effectDisplay(아이콘+수치)를 정 가운데(루트 원점)에 띄운다.
    public void SetEffect(CardEffect cardEffect)
    {
        ClearEffect();
        _rewardText.text = "";

        _effectDisplay = EffectDisplay.Spawn(transform);
        if (_effectDisplay == null) return;

        _effectDisplay.SetLocalPosition(Vector3.zero);
        _effectDisplay.SetEffect(cardEffect.GetEffect().GetEffectType(), cardEffect.GetEffect().GetMagnitude().ToString());
        _effectDisplay.SetSortingLayer("UI");
    }

    private void ClearEffect()
    {
        if (_effectDisplay != null) Destroy(_effectDisplay.gameObject);
        _effectDisplay = null;
    }

    public void SetSelected(bool selected) => _highlight.SetActive(selected);
}
