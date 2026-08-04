using TMPro;
using UnityEngine;

// RewardDisplay 프리팹 루트에 부착. Card/HighLight를 켜고 끄는 것과 RewardSprite/RewardText 초기화를 담당한다.
public class RewardDisplay : MonoBehaviour
{
    private static GameObject _effectDisplayPrefab;

    private GameObject _highlight;
    private SpriteRenderer _rewardSprite;
    private TMP_Text _rewardText;
    private GameObject _effectDisplay;

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
        GameObject effectDisplayPrefab = GetEffectDisplayPrefab();
        if (effectDisplayPrefab == null) return;

        _effectDisplay = Instantiate(effectDisplayPrefab, transform);
        _effectDisplay.transform.localPosition = Vector3.zero;

        SpriteRenderer effectSprite = _effectDisplay.transform.Find("EffectSprite").GetComponent<SpriteRenderer>();
        effectSprite.sprite = BattleManager.GetEmoji(cardEffect.GetEffect().GetEffectType());

        TMP_Text magnitudeText = _effectDisplay.transform.Find("MagnitudeText").GetComponent<TMP_Text>();
        magnitudeText.text = cardEffect.GetEffect().GetMagnitude().ToString();

        // effectDisplay는 EffectSprite/MagnitudeText 둘뿐으로 구조가 고정이라 재귀 없이 직접 UI 레이어로 옮긴다.
        int uiLayerID = SortingLayer.NameToID("UI");
        effectSprite.sortingLayerID = uiLayerID;
        magnitudeText.GetComponent<Renderer>().sortingLayerID = uiLayerID;
    }

    private void ClearEffect()
    {
        if (_effectDisplay != null) Destroy(_effectDisplay);
        _effectDisplay = null;
    }

    private static GameObject GetEffectDisplayPrefab()
    {
        if (_effectDisplayPrefab == null)
            _effectDisplayPrefab = Resources.Load<GameObject>("Prefabs/EffectDisplay");
        return _effectDisplayPrefab;
    }

    public void SetSelected(bool selected) => _highlight.SetActive(selected);
}
