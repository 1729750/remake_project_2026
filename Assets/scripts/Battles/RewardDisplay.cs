using UnityEngine;

// RewardDisplay 프리팹 루트에 부착. Card/HighLight를 켜고 끄는 것만 담당한다.
public class RewardDisplay : MonoBehaviour
{
    private GameObject _highlight;

    private void Awake()
    {
        _highlight = transform.Find("Card/HighLight").gameObject;
        _highlight.SetActive(false);
    }

    public void SetSelected(bool selected) => _highlight.SetActive(selected);
}
