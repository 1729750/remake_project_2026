using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 자식으로 미리 배치된 ButtonComponent들을 좌우로 순환 선택하는 컴포넌트. select/unselect
// (하이라이트)와 "지금 선택된 버튼을 실행"만 담당하고, 각 버튼이 실행할 함수가 무엇인지는 전혀
// 모른다. 화면 쪽 매니저(예: GameEndManager)가 이 컴포넌트를 자신의 필드로 들고 있다가
// GetButton(i)으로 개별 ButtonComponent를 가져와 SetAction으로 실행할 함수를 직접 박아 넣는다.
// 그래서 실행 시점엔 switch-case 없이 Enter()만 호출하면 된다(ButtonComponent가 저장해둔
// 함수를 실행).
public class ButtonManager : MonoBehaviour
{
    // 자식 계층에 나타나는 순서 그대로 담긴다 — 왼쪽(낮은 인덱스)부터 오른쪽 순.
    // 인스펙터에서 직접 채워 넣지 않으면 Awake에서 자동으로 채운다.
    [SerializeField] private List<ButtonComponent> buttons;

    private int _selectedIndex = -1;

    private void Awake()
    {
        if (buttons == null || buttons.Count == 0)
            buttons = GetComponentsInChildren<ButtonComponent>(true).ToList();
    }

    public int GetButtonCount() => buttons?.Count ?? 0;

    public ButtonComponent GetButton(int index)
    {
        return buttons != null && index >= 0 && index < buttons.Count ? buttons[index] : null;
    }

    public void Select(int index)
    {
        if (buttons == null || index < 0 || index >= buttons.Count) return;

        if (_selectedIndex >= 0 && _selectedIndex < buttons.Count)
            Unselect(_selectedIndex);

        _selectedIndex = index;
        buttons[index]?.SetSelected(true);
    }

    public void Unselect(int index)
    {
        if (buttons == null || index < 0 || index >= buttons.Count) return;
        buttons[index]?.SetSelected(false);
    }

    public void Move(int delta)
    {
        if (buttons == null || buttons.Count == 0) return;

        int count = buttons.Count;
        int current = _selectedIndex < 0 ? 0 : _selectedIndex;
        Select(((current + delta) % count + count) % count);
    }

    public void Enter()
    {
        if (buttons == null || _selectedIndex < 0 || _selectedIndex >= buttons.Count) return;
        buttons[_selectedIndex]?.Enter();
    }
}
