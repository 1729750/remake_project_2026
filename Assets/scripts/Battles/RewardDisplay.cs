using TMPro;
using UnityEngine;

// RewardDisplay 프리팹 루트에 부착. Card/HighLight를 켜고 끄는 것과 RewardSprite/RewardText 초기화를 담당한다.
public class RewardDisplay : MonoBehaviour
{
    private GameObject _highlight;
    private SpriteRenderer _rewardSprite;
    private TMP_Text _rewardText;
    private EffectDisplay _effectDisplay;
    private GameObject _costRoot;
    private TMP_Text _costText;
    private GameObject _cooltimeRoot;
    private TMP_Text _cooltimeText;

    private void Awake()
    {
        _highlight = transform.Find("Card/HighLight").gameObject;
        _highlight.SetActive(false);

        _rewardSprite = transform.Find("RewardSprite").GetComponent<SpriteRenderer>();
        _rewardText = transform.Find("RewardText").GetComponent<TMP_Text>();

        _costRoot = transform.Find("cost").gameObject;
        _costText = transform.Find("cost/CostText").GetComponent<TMP_Text>();
        _cooltimeRoot = transform.Find("cooltime").gameObject;
        _cooltimeText = transform.Find("cooltime/cooltimeText").GetComponent<TMP_Text>();
        SetCostCooldownActive(false);
    }

    public void Init(string rewardText, Sprite rewardSprite)
    {
        ClearEffect();
        _rewardText.text = rewardText;
        _rewardSprite.sprite = rewardSprite;
    }

    // 강화 후보 표시: RewardText는 비워 두고, effectDisplay(아이콘+수치)를 정 가운데(루트 원점)에 띄우고
    // cost/cooltime 변화량도 함께 켜서 표기한다. 카드 획득/삭제 등 다른 항목에서는 꺼진 채로 남는다.
    public void SetUpgrade(CardUpgrade upgrade)
    {
        ClearEffect();
        _rewardText.text = "";

        CardEffect cardEffect = upgrade.effect;
        _effectDisplay = EffectDisplay.Spawn(transform);
        if (_effectDisplay != null)
        {
            _effectDisplay.SetLocalPosition(Vector3.zero);
            _effectDisplay.SetEffect(cardEffect.GetEffect().GetEffectType(), cardEffect.GetEffect().GetMagnitude().ToString());
            _effectDisplay.SetSortingLayer("UI");
        }

        SetCostCooldownActive(true);
        _costText.text = FormatDelta(upgrade.costDelta);
        _cooltimeText.text = FormatDelta(upgrade.cooldownDelta);
    }

    private static string FormatDelta(int delta) => delta > 0 ? $"+{delta}" : delta.ToString();

    private void SetCostCooldownActive(bool active)
    {
        _costRoot.SetActive(active);
        _cooltimeRoot.SetActive(active);
    }

    private void ClearEffect()
    {
        if (_effectDisplay != null) Destroy(_effectDisplay.gameObject);
        _effectDisplay = null;
        SetCostCooldownActive(false);
    }

    public void SetSelected(bool selected) => _highlight.SetActive(selected);
}
