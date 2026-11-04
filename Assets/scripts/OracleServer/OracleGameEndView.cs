using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MultiPlayMode_Oracle 씬에서 GameEndManager 자리에 들어가는 결과 화면.
/// 싱글과 같은 다시하기(0번) / 나가기(1번) 버튼을 ←/→ + 선택 키로 고른다.
/// 다시하기 = 같은 상대와 새 판 요청, 나가기 = 로비 씬으로.
/// </summary>
public sealed class OracleGameEndView : MonoBehaviour
{
    [SerializeField] private TrophiesManager trophiesManager;
    [SerializeField] private ButtonManager buttonManager;

    private bool inputLoaded;

    public event Action RematchSelected;
    public event Action ExitSelected;

    private void Awake()
    {
        if (buttonManager == null) return;
        buttonManager.GetButton(0)?.SetAction(() => RematchSelected?.Invoke());
        buttonManager.GetButton(1)?.SetAction(() => ExitSelected?.Invoke());
    }

    public void Show()
    {
        gameObject.SetActive(true);
        trophiesManager?.ResetTrophies();

        if (buttonManager == null || inputLoaded || PlayerInputManager.Instance == null) return;
        buttonManager.Select(0);
        inputLoaded = true;
        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"] = () => buttonManager.Move(-1),
            ["Right"] = () => buttonManager.Move(1),
            ["Select"] = buttonManager.Enter,
        });
    }

    public void Hide()
    {
        UnloadInput();
        gameObject.SetActive(false);
    }

    private void OnDisable() => UnloadInput();

    private void UnloadInput()
    {
        if (!inputLoaded) return;
        inputLoaded = false;
        PlayerInputManager.Instance?.Unload();
    }
}
