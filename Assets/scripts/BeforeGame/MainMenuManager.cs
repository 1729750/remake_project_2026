using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string startSceneName;
    [SerializeField] private string battleSceneName;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Menu Images")]
    [Tooltip("0 시작 / 1 대전모드 / 2 설정 / 3 종료하기")]
    [SerializeField] private Image[] menuImages;

    [Header("Normal Sprites")]
    [SerializeField] private Sprite[] normalSprites;

    [Header("Hover Sprites")]
    [SerializeField] private Sprite[] hoverSprites;

    // 0 = 시작
    // 1 = 대전모드
    // 2 = 설정
    // 3 = 종료하기
    private int currentIndex = 0;

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // 처음에는 시작 버튼 선택
        currentIndex = 0;

        UpdateSelection();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // =========================
        // 설정창이 열려 있는 경우
        // =========================
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            // ESC로 설정창 닫기
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseSettingsPanel();
            }

            return;
        }

        // =========================
        // A = 왼쪽 이동
        // =========================
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            MoveLeft();
        }

        // =========================
        // D = 오른쪽 이동
        // =========================
        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            MoveRight();
        }

        // =========================
        // Enter = 현재 메뉴 실행
        // =========================
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            SelectCurrentMenu();
        }
    }

    // =========================
    // 왼쪽 이동
    // =========================
    private void MoveLeft()
    {
        currentIndex--;

        // 시작에서 A 누르면 종료하기로
        if (currentIndex < 0)
        {
            currentIndex = menuImages.Length - 1;
        }

        UpdateSelection();
    }

    // =========================
    // 오른쪽 이동
    // =========================
    private void MoveRight()
    {
        currentIndex++;

        // 종료하기에서 D 누르면 시작으로
        if (currentIndex >= menuImages.Length)
        {
            currentIndex = 0;
        }

        UpdateSelection();
    }

    // =========================
    // 선택된 버튼 Hover 처리
    // =========================
    private void UpdateSelection()
    {
        for (int i = 0; i < menuImages.Length; i++)
        {
            if (menuImages[i] == null)
                continue;

            // 현재 선택된 버튼
            if (i == currentIndex)
            {
                if (i < hoverSprites.Length &&
                    hoverSprites[i] != null)
                {
                    menuImages[i].sprite = hoverSprites[i];
                }
            }
            // 선택되지 않은 버튼
            else
            {
                if (i < normalSprites.Length &&
                    normalSprites[i] != null)
                {
                    menuImages[i].sprite = normalSprites[i];
                }
            }
        }
    }

    // =========================
    // Enter 눌렀을 때 실행
    // =========================
    private void SelectCurrentMenu()
    {
        switch (currentIndex)
        {
            // 시작
            case 0:
                OnClickStart();
                break;

            // 대전모드
            case 1:
                OnClickBattle();
                break;

            // 설정
            case 2:
                OnClickSettings();
                break;

            // 종료하기
            case 3:
                OnClickOut();
                break;
        }
    }

    // =========================
    // 0. 시작
    // =========================
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

    // =========================
    // 1. 대전모드
    // =========================
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

    // =========================
    // 2. 설정
    // =========================
    public void OnClickSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        UpdateSelection();
    }

    // =========================
    // 3. 종료하기
    // =========================
    public void OnClickOut()
    {
#if UNITY_EDITOR
        Debug.Log("게임 종료");
#else
        Application.Quit();
#endif
    }
}