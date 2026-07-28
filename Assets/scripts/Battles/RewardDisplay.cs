using TMPro;
using UnityEngine;

// RewardDisplay 프리팹 루트에 부착. Card/HighLight를 켜고 끄는 것과 RewardSprite/RewardText 초기화를 담당한다.
public class RewardDisplay : MonoBehaviour
{
    private GameObject _highlight;
    private SpriteRenderer _rewardSprite;
    private TMP_Text _rewardText;

    private void Awake()
    {
        _highlight = transform.Find("Card/HighLight").gameObject;
        _highlight.SetActive(false);

        _rewardSprite = transform.Find("RewardSprite").GetComponent<SpriteRenderer>();
        _rewardText = transform.Find("RewardText").GetComponent<TMP_Text>();
    }

    public void Init(string rewardText)
    {
        _rewardText.text = rewardText;
    }

    public void SetSelected(bool selected) => _highlight.SetActive(selected);
}
