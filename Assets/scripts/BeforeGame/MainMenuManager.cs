using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string startSceneName;

    [Header("Panels")]
    [SerializeField] private GameObject battlePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Menu Selection")]
    [SerializeField] private Image[] menuImages;

    [SerializeField] private Sprite[] normalSprites;
    [SerializeField] private Sprite[] hoverSprites;

    private int currentIndex = 0;

    private void Start()
    {
        if (battlePanel != null)
            battlePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        UpdateSelection();
    }

    private void Update()
    {
        // A : 왼쪽 이동
        if (Input.GetKeyDown(KeyCode.A))
        {
            currentIndex--;

            if (currentIndex < 0)
                currentIndex = menuImages.Length - 1;

            UpdateSelection();
        }

        // D : 오른쪽 이동
        if (Input.GetKeyDown(KeyCode.D))
        {
            currentIndex++;

            if (currentIndex >= menuImages.Length)
                currentIndex = 0;

            UpdateSelection();
        }

        // 선택
        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            SelectCurrentMenu();
        }
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < menuImages.Length; i++)
        {
            if (menuImages[i] == null)
                continue;

            // 현재 선택된 메뉴
            if (i == currentIndex)
            {
                if (i < hoverSprites.Length)
                    menuImages[i].sprite = hoverSprites[i];
            }
            // 선택되지 않은 메뉴
            else
            {
                if (i < normalSprites.Length)
                    menuImages[i].sprite = normalSprites[i];
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

    // Start
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

    // Battle
    public void OnClickBattle()
    {
        if (battlePanel != null)
            battlePanel.SetActive(true);
    }

    // Settings
    public void OnClickSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseBattlePanel()
    {
        if (battlePanel != null)
            battlePanel.SetActive(false);
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // Out
    public void OnClickOut()
    {
#if UNITY_EDITOR
        Debug.Log("게임 종료 버튼을 눌렀습니다. Build에서는 게임이 종료됩니다.");
#else
        Application.Quit();
#endif
    }
}