using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] protected InputActionAsset inputActions;

    private readonly Stack<InputContext> _stack = new Stack<InputContext>();

    // Load/Unload 스택과 별개로, 화면이 뭘 띄우고 있든 항상 폴링되어야 하는 입력(예: 팝업의
    // NextPage/PrevPage)을 위한 보조 스택들. mapName별로 하나씩 두어, 여러 PopupManager처럼 같은
    // map을 쓰는 호출부가 동시에(예: 맵 화면 위에 플레이어 덱 패널이 겹쳐 뜬 경우) LoadOverlay를
    // 걸어도 서로를 밀어내지 않고 각자 쌓인다 — 제일 위(가장 최근에 Enable된 화면)만 폴링된다.
    private readonly Dictionary<string, Stack<InputContext>> _overlayStacks = new Dictionary<string, Stack<InputContext>>();

    // mapName의 액션들 중 bindings에 값이 있는 것만 연결해 새 context를 만들어 stack에 쌓는다.
    // 이전까지 맨 위였던 context의 map은 비활성화되고, 새로 올라온 context의 map만 활성화된다.
    public void Load(string mapName, IReadOnlyDictionary<string, Action> bindings)
    {
        if (inputActions == null) return;

        InputActionMap map = inputActions.FindActionMap(mapName, throwIfNotFound: true);

        if (_stack.Count > 0)
            _stack.Peek().Map.Disable();

        _stack.Push(new InputContext(map, bindings));
        map.Enable();
    }

    // stack 맨 위 context를 제거한다. 그 map은 비활성화되고, 아래 깔려있던 context가 있다면
    // 그 map을 다시 활성화해 이어서 입력을 받는다.
    public void Unload()
    {
        if (_stack.Count == 0) return;

        InputContext context = _stack.Pop();
        context.Map.Disable();

        if (_stack.Count > 0)
            _stack.Peek().Map.Enable();
    }

    // Load/Unload 스택(화면별 커서 이동 등)과 독립적으로 항상 활성화되어 있어야 하는 map을 등록한다.
    // 메인 스택처럼 다른 context를 Disable시키지 않고, map을 계속 Enable 상태로 둔 채 이 mapName
    // 전용 보조 스택에만 쌓는다 — 같은 mapName으로 여러 번 걸려도(여러 PopupManager가 동시에
    // 활성화된 경우 등) 서로 덮어쓰지 않고, 가장 최근 것만 폴링되다가 Unload되면 그 아래 걸로 되돌아간다.
    public void LoadOverlay(string mapName, IReadOnlyDictionary<string, Action> bindings)
    {
        if (inputActions == null) return;

        InputActionMap map = inputActions.FindActionMap(mapName, throwIfNotFound: true);

        if (!_overlayStacks.TryGetValue(mapName, out Stack<InputContext> stack))
        {
            stack = new Stack<InputContext>();
            _overlayStacks[mapName] = stack;
        }
        stack.Push(new InputContext(map, bindings));
        map.Enable();
    }

    // LoadOverlay로 등록했던 mapName의 맨 위 context를 제거한다. map 자체는 끄지 않는다 — 아래에
    // 남은 context가 이어서 쓰거나, 없어도 다음 LoadOverlay가 다시 쓸 수 있으니 굳이 Disable할 필요가 없다.
    public void UnloadOverlay(string mapName)
    {
        if (!_overlayStacks.TryGetValue(mapName, out Stack<InputContext> stack) || stack.Count == 0) return;

        stack.Pop();
        if (stack.Count == 0)
            _overlayStacks.Remove(mapName);
    }

    private void Update()
    {
        if (_stack.Count > 0)
            _stack.Peek().Poll();

        foreach (Stack<InputContext> stack in _overlayStacks.Values)
            if (stack.Count > 0)
                stack.Peek().Poll();
    }

    // 특정 입력 map과, 그 map의 각 액션에 연결된 콜백들을 함께 들고 있는 stack 원소.
    private class InputContext
    {
        public readonly InputActionMap Map;
        private readonly List<(InputAction action, Action callback)> _bindings = new List<(InputAction, Action)>();

        public InputContext(InputActionMap map, IReadOnlyDictionary<string, Action> bindings)
        {
            Map = map;
            foreach (var pair in bindings)
            {
                if (pair.Value == null) continue;
                _bindings.Add((map.FindAction(pair.Key, throwIfNotFound: true), pair.Value));
            }
        }

        public void Poll()
        {
            foreach (var (action, callback) in _bindings)
                if (action.triggered)
                    callback();
        }
    }
}
