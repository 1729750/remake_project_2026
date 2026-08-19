using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("각 버튼의 기본 이미지")]
    [SerializeField] private Sprite[] normalSprites;

    [Header("Hover Sprites")]
    [Tooltip("각 버튼의 Hover 이미지")]
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
        // 설정창이 열려 있으면 메인 메뉴 조작 X
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            // ESC로 설정창 닫기
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseSettingsPanel();
            }

            return;
        }

        // A = 왼쪽
        if (Input.GetKeyDown(KeyCode.A))
        {
            MoveLeft();
        }

        // D = 오른쪽
        if (Input.GetKeyDown(KeyCode.D))
        {
            MoveRight();
        }

        // Enter 또는 Space = 선택
        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            SelectCurrentMenu();
        }
    }

    private void MoveLeft()
    {
        currentIndex--;

        // 시작에서 A를 누르면 종료하기로 이동
        if (currentIndex < 0)
        {
            currentIndex = menuImages.Length - 1;
        }

        UpdateSelection();
    }

    private void MoveRight()
    {
        currentIndex++;

        // 종료하기에서 D를 누르면 시작으로 이동
        if (currentIndex >= menuImages.Length)
        {
            currentIndex = 0;
        }

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < menuImages.Length; i++)
        {
            if (menuImages[i] == null)
                continue;

            // 현재 선택된 버튼
            if (i == currentIndex)
            {
                if (i < hoverSprites.Length && hoverSprites[i] != null)
                {
                    menuImages[i].sprite = hoverSprites[i];
                }
            }
            // 선택되지 않은 버튼
            else
            {
                if (i < normalSprites.Length && normalSprites[i] != null)
                {
                    menuImages[i].sprite = normalSprites[i];
                }
            }
        }
    }

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

    // ==============================
    // 시작
    // ==============================
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

    // ==============================
    // 대전모드
    // ==============================
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

    // ==============================
    // 설정
    // ==============================
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

        // 설정창 닫은 후 현재 선택 표시 다시 갱신
        UpdateSelection();
    }

    // ==============================
    // 종료하기
    // ==============================
    public void OnClickOut()
    {
#if UNITY_EDITOR
        Debug.Log("게임 종료");
#else
        Application.Quit();
#endif
    }
}