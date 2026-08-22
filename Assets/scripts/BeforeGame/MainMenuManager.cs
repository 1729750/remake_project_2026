using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [System.Serializable]
    public class MenuItem
    {
        [Header("이 메뉴에 포함된 Image들")]
        public Image[] targetImages;

        [Header("각 Image의 기본 Sprite")]
        public Sprite[] normalSprites;

        [Header("각 Image의 Hover Sprite")]
        public Sprite[] hoverSprites;
    }

    [Header("Scene")]
    [SerializeField] private string startSceneName;
    [SerializeField] private string battleSceneName;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Menu Items")]
    [Tooltip("0 시작 / 1 대전모드 / 2 설정 / 3 종료하기")]
    [SerializeField] private MenuItem[] menuItems;

    private int currentIndex = 0;

    // MapManager.PlayMoveSelectSound와 같은 패턴 — Left/Right로 메뉴를 옮길 때 MoveSelect1/2를
    // 번갈아 재생하기 위한 토글.
    private bool _moveSelectToggle;


    // ============================================
    // [추가 - Input System]
    // PlayerInputManager에 MainMenu 입력을 등록했는지 확인
    // ============================================
    private bool inputLoaded = false;


    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        currentIndex = 0;
        UpdateSelection();

        SoundManager.Instance?.Play(BgmName.Title);


        // ============================================
        // [추가 - Input System]
        // 기존 InputManager의 Load 기능을 사용한다.
        //
        // PlayerAction / Select
        // Left   -> MoveLeft
        // Right  -> MoveRight
        // Select -> SelectCurrentMenu
        // ============================================

        if (PlayerInputManager.Instance == null)
        {
            Debug.LogError(
                "[MainMenuManager] PlayerInputManager가 존재하지 않습니다."
            );

            return;
        }

        Dictionary<string, Action> bindings =
            new Dictionary<string, Action>
            {
                { "Left", OnInputLeft },
                { "Right", OnInputRight },
                { "Select", OnInputSelect }
            };

        PlayerInputManager.Instance.Load("Select", bindings);

        inputLoaded = true;
    }


    // ============================================
    // [추가 - Input System]
    // MainMenu가 사라질 때 등록했던 입력 Context 제거
    // ============================================

    private void OnDestroy()
    {
        if (inputLoaded &&
            PlayerInputManager.Instance != null)
        {
            PlayerInputManager.Instance.Unload();

            inputLoaded = false;
        }
    }


    // ============================================
    // [추가 - Input System]
    // Select / Left 입력
    // ============================================

    private void OnInputLeft()
    {
        // 설정창이 켜져 있으면
        // 메인 메뉴 자체는 움직이지 않도록 방어
        if (settingsPanel != null &&
            settingsPanel.activeSelf)
            return;

        MoveLeft();
    }


    // ============================================
    // [추가 - Input System]
    // Select / Right 입력
    // ============================================

    private void OnInputRight()
    {
        if (settingsPanel != null &&
            settingsPanel.activeSelf)
            return;

        MoveRight();
    }


    // ============================================
    // [추가 - Input System]
    // Select / Select 입력
    // ============================================

    private void OnInputSelect()
    {
        if (settingsPanel != null &&
            settingsPanel.activeSelf)
            return;

        SelectCurrentMenu();
    }


    // ============================================
    // 아래부터 기존 메뉴 로직 그대로
    // ============================================

    private void MoveLeft()
    {
        currentIndex--;

        if (currentIndex < 0)
            currentIndex = menuItems.Length - 1;

        UpdateSelection();
        PlayMoveSelectSound();
    }


    private void MoveRight()
    {
        currentIndex++;

        if (currentIndex >= menuItems.Length)
            currentIndex = 0;

        UpdateSelection();
        PlayMoveSelectSound();
    }


    private void PlayMoveSelectSound()
    {
        SoundManager.Instance?.Play(_moveSelectToggle ? EffectSound.MoveSelect1 : EffectSound.MoveSelect2);
        _moveSelectToggle = !_moveSelectToggle;
    }


    private void UpdateSelection()
    {
        for (int i = 0; i < menuItems.Length; i++)
        {
            bool isSelected = (i == currentIndex);
            ApplyMenuSprites(menuItems[i], isSelected);
        }
    }


    private void ApplyMenuSprites(MenuItem item, bool isSelected)
    {
        if (item == null || item.targetImages == null)
            return;

        for (int j = 0; j < item.targetImages.Length; j++)
        {
            if (item.targetImages[j] == null)
                continue;

            if (isSelected)
            {
                if (item.hoverSprites != null &&
                    j < item.hoverSprites.Length &&
                    item.hoverSprites[j] != null)
                {
                    item.targetImages[j].sprite =
                        item.hoverSprites[j];
                }
            }
            else
            {
                if (item.normalSprites != null &&
                    j < item.normalSprites.Length &&
                    item.normalSprites[j] != null)
                {
                    item.targetImages[j].sprite =
                        item.normalSprites[j];
                }
            }
        }
    }


    private void SelectCurrentMenu()
    {
        switch (currentIndex)
        {
            case 0:
                OnClickStart();
                break;

            case 1:
                OnClickBattle();
                break;

            case 2:
                OnClickSettings();
                break;

            case 3:
                OnClickOut();
                break;
        }
    }


    public void OnClickStart()
    {
        if (!string.IsNullOrEmpty(startSceneName))
        {
            SceneManager.LoadScene(startSceneName);
        }
        else
        {
            Debug.LogWarning(
                "Start Scene 이름을 입력해주세요."
            );
        }
    }


    public void OnClickBattle()
    {
        if (!string.IsNullOrEmpty(battleSceneName))
        {
            SceneManager.LoadScene(battleSceneName);
        }
        else
        {
            Debug.LogWarning(
                "Battle Scene 이름을 입력해주세요."
            );
        }
    }


    public void OnClickSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }


    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        UpdateSelection();
    }


    public void OnClickOut()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}