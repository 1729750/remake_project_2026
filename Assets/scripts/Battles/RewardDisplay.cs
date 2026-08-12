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

    // RewardDisplay를 빈 상태로 되돌린다: RewardText/RewardSprite를 비우고, SetUpgrade가 띄웠던
    // effectDisplay를 지우고, cost/cooltime 표시를 끈다. Init/SetUpgrade 둘 다 각자 내용을 채우기
    // 전에 먼저 이걸 불러서, 이전에 표시했던 내용(RewardSprite 아이콘 등)이 안 지워지고 새 내용과
    // 겹쳐 보이는 일이 없게 한다.
    public void ResetDisplay()
    {
        _rewardText.text = "";
        _rewardSprite.sprite = null;
        ClearEffect();
    }

    public void Init(string rewardText, Sprite rewardSprite)
    {
        ResetDisplay();
        _rewardText.text = rewardText;
        _rewardSprite.sprite = rewardSprite;
    }

    // 강화 후보 표시: RewardText는 비워 두고, effectDisplay(아이콘+수치)를 정 가운데(루트 원점)에 띄우고
    // cost/cooltime 변화량도 함께 켜서 표기한다. 카드 획득/삭제 등 다른 항목에서는 꺼진 채로 남는다.
    public void SetUpgrade(CardUpgrade upgrade)
    {
        ResetDisplay();

        CardEffect cardEffect = upgrade.effect;
        _effectDisplay = EffectDisplay.Spawn(transform);
        if (_effectDisplay != null)
        {
            _effectDisplay.SetEffect(cardEffect.GetEffect(), cardEffect.GetEffect().GetMagnitude().ToString());
            _effectDisplay.SetSortingLayer("UI");

            // EffectDisplay 프리팹은 항상 원본(1x1) 스케일로 스폰되므로, RewardSprite가 이미
            // 카드 아이콘 자리에 맞게 튜닝된 localScale.y를 목표 높이로 삼아 맞춘다
            // (CharacterManager.UpdateEffectList와 동일한 높이 기준 스케일 방식).
            Vector2 spriteSize = _effectDisplay.GetSpriteSize();
            float targetHeight = _rewardSprite.transform.localScale.y;
            float nativeHeight = spriteSize.y > 0f ? spriteSize.y : targetHeight;
            float scale = nativeHeight > 0f ? targetHeight / nativeHeight : 1f;
            _effectDisplay.SetScale(scale);
            _effectDisplay.SetLocalPosition(Vector3.zero);
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
