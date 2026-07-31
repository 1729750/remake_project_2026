using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] protected InputActionAsset inputActions;

    private readonly Stack<InputContext> _stack = new Stack<InputContext>();

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

    private void Update()
    {
        if (_stack.Count > 0)
            _stack.Peek().Poll();
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
