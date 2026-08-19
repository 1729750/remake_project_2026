using System;
using UnityEngine;

// 선택 가능한 버튼 하나. HighLight 자식(MapVisual/RewardDisplay와 같은 패턴)을 켜고 끄는 것과,
// 자신이 실행할 함수를 들고 있다가 Enter()가 호출되면 그 함수를 실행하는 것까지만 담당한다 —
// 이 버튼이 실제로 무엇을 하는지는 전혀 모른다. 실행할 함수는 부모 쪽 매니저(ButtonManager를
// 상속하는 화면, 예: GameEndManager)가 SetAction으로 직접 박아 넣는다.
public class ButtonComponent : MonoBehaviour
{
    private GameObject _highlight;
    private Action _action;

    private void Awake()
    {
        Transform highlight = transform.Find("HighLight");
        _highlight = highlight != null ? highlight.gameObject : null;
        _highlight?.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        _highlight?.SetActive(selected);
    }

    // 이 버튼을 선택(Enter)했을 때 실행할 함수를 (재)설정한다. 이미 설정되어 있던 함수는 덮어써진다.
    public void SetAction(Action action)
    {
        _action = action;
    }

    public void Enter()
    {
        _action?.Invoke();
    }
}
