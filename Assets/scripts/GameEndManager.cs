using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// GameState.GameOver 화면. ButtonManager를 component로 들고 있다가(GetButton(0)=Restart,
// GetButton(1)=Exit) 각 버튼이 실행할 함수를 SetAction으로 직접 박아 넣는다 — switch-case 없이
// ButtonManager.Enter()가 그 ButtonComponent에게 실행을 위임한다. 자신만의 TrophiesManager
// (RewardPanel처럼 mapManager와는 별개 인스턴스)로 이번 런에서 mapManager가 쌓아온 트로피
// (격파한 적들) + 방금 플레이어를 이긴 적의 데이터를 옮겨 담아 전시한다.
public class GameEndManager : MonoBehaviour
{
    [SerializeField] private TrophiesManager trophiesManager;
    [SerializeField] private ButtonManager buttonManager;
    [SerializeField] private string beforeGameSceneName = "BeforeGame";

    private void Awake()
    {
        if (buttonManager == null) return;

        buttonManager.GetButton(0)?.SetAction(OnClickRestart);
        buttonManager.GetButton(1)?.SetAction(OnClickExit);
    }

    // GameManager.GameOver가 상태 전환 직후 호출한다. mapManager가 들고 있던 트로피 데이터를
    // 그대로 옮겨 담고, 마지막으로 플레이어를 패배시킨 상대(=mapManager의 battleHistory 마지막
    // 항목, 승리 트로피로는 추가된 적 없다)까지 채운 뒤 버튼 선택을 시작 상태로 되돌린다.
    public void ShowResult()
    {
        if (trophiesManager != null && MapManager.Instance != null)
        {
            trophiesManager.ResetTrophies();

            foreach (CharacterData defeated in MapManager.Instance.GetTrophyData())
                trophiesManager.AddTrophy(defeated);

            IReadOnlyList<CharacterData> history = MapManager.Instance.GetBattleHistory();
            if (history.Count > 0)
                trophiesManager.AddTrophy(history[history.Count - 1]);
        }

        if (buttonManager == null) return;

        buttonManager.Select(0);

        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => buttonManager.Move(-1),
            ["Right"]  = () => buttonManager.Move(1),
            ["Select"] = buttonManager.Enter,
        });
    }

    // "게임 시작"(재시작) 버튼. GameManager를 비롯한 씬 오브젝트들을 상태로 되돌리는 대신, 현재
    // 씬을 통째로 다시 로드해서 완전히 새로 시작한다.
    private void OnClickRestart()
    {
        PlayerInputManager.Instance.Unload();
        // SoundManager는 DontDestroyOnLoad라 씬을 다시 로드해도 재생 중이던 Win/LoseBGM이
        // 저절로 멈추지 않는다 — 다음 전투가 시작되기 전까지(BattleManager.StartBattle의
        // StopBgm) 새 런의 적 선택 화면까지 새어 나가므로 여기서 직접 끊는다.
        SoundManager.Instance?.StopBgm();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // "타이틀로" 버튼. BeforeGame 씬으로 돌아간다.
    private void OnClickExit()
    {
        PlayerInputManager.Instance.Unload();
        // 위와 같은 이유로, 타이틀로 돌아갈 때도 Win/LoseBGM이 계속 흐르지 않도록 끊는다.
        SoundManager.Instance?.StopBgm();
        SceneManager.LoadScene(beforeGameSceneName);
    }
}
