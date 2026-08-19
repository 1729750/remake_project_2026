using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        currentIndex = 0;
        UpdateSelection();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseSettingsPanel();
            }

            return;
        }

        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            MoveLeft();
        }

        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            MoveRight();
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            SelectCurrentMenu();
        }
    }

    private void MoveLeft()
    {
        currentIndex--;

        if (currentIndex < 0)
            currentIndex = menuItems.Length - 1;

        UpdateSelection();
    }

    private void MoveRight()
    {
        currentIndex++;

        if (currentIndex >= menuItems.Length)
            currentIndex = 0;

        UpdateSelection();
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
                    item.targetImages[j].sprite = item.hoverSprites[j];
                }
            }
            else
            {
                if (item.normalSprites != null &&
                    j < item.normalSprites.Length &&
                    item.normalSprites[j] != null)
                {
                    item.targetImages[j].sprite = item.normalSprites[j];
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
            Debug.LogWarning("Start Scene 이름을 입력해주세요.");
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
            Debug.LogWarning("Battle Scene 이름을 입력해주세요.");
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