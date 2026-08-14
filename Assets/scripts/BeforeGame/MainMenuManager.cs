using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string startSceneName;

    [Header("Panels")]
    [SerializeField] private GameObject battlePanel;
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        // 게임 시작 시 패널 비활성화
        if (battlePanel != null)
            battlePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // 1. Start 버튼
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

    // 2. 대전하기 버튼
    public void OnClickBattle()
    {
        if (battlePanel != null)
            battlePanel.SetActive(true);
    }

    // 3. 설정 버튼
    public void OnClickSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    // 대전하기 패널 닫기
    public void CloseBattlePanel()
    {
        if (battlePanel != null)
            battlePanel.SetActive(false);
    }

    // 설정 패널 닫기
    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // 4. Out 버튼
    public void OnClickOut()
    {
#if UNITY_EDITOR
        Debug.Log("게임 종료 버튼을 눌렀습니다. Build에서는 게임이 종료됩니다.");
#else
        Application.Quit();
#endif
    }
}