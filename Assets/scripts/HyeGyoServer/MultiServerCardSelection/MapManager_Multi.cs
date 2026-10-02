using UnityEngine;

public sealed class MapManager_Multi : MonoBehaviour
{
    public static MapManager_Multi Instance
    {
        get;
        private set;
    }

    [Header("Preparation")]
    [SerializeField]
    private GameObject preparationViewRoot;

    [SerializeField]
    private MultiPreparationManager preparationManager;

    private PreparationOption[] _options;
    private int _selectedIndex;

    // Inspector에 직접 넣지 않고
    // 자식의 PreparationOptionVisual_Multi를 자동 검색
    private PreparationOptionVisual_Multi[] optionVisuals;


    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 기존 MapManager 방식처럼
        // 자식 오브젝트의 Visual들을 자동으로 찾는다.
        optionVisuals =
            GetComponentsInChildren<
                PreparationOptionVisual_Multi
            >(true);

        if (preparationViewRoot != null)
        {
            preparationViewRoot.SetActive(true);
        }

        Debug.Log(
            "[MapManager_Multi] Awake - 활성화\n" +
            $"OptionVisual 자동 검색: {optionVisuals.Length}개"
        );
    }


    private void Start()
    {
        if (preparationViewRoot != null)
        {
            preparationViewRoot.SetActive(true);
        }

            Debug.Log(
                "[MapManager_Multi] Start - 준비 화면 활성화"
            );

        Debug.Log(
            "[MapManager_Multi] " +
            $"찾은 OptionVisual 수: " +
            $"{(optionVisuals != null ? optionVisuals.Length : 0)}"
        );
}


    public void SetPreparationViewActive(
        bool active)
    {
        if (preparationViewRoot != null)
        {
            preparationViewRoot.SetActive(active);
        }

        Debug.Log(
            "[MapManager_Multi] " +
            $"Preparation View Active: {active}"
        );
    }


    public void ShowOptions(
        PreparationOption[] options)
    {
        if (options == null ||
            options.Length == 0)
        {
            Debug.LogWarning(
                "[MapManager_Multi] " +
                "표시할 옵션이 없습니다."
            );

            return;
        }

        if (optionVisuals == null ||
            optionVisuals.Length == 0)
        {
            Debug.LogError(
                "[MapManager_Multi] " +
                "PreparationOptionVisual_Multi를 " +
                "자식에서 찾지 못했습니다."
            );

            return;
        }

        _options = options;
        _selectedIndex = 0;

        int count =
            Mathf.Min(
                optionVisuals.Length,
                options.Length
            );

        for (int i = 0;
             i < optionVisuals.Length;
             i++)
        {
            bool active =
                i < count;

            if (optionVisuals[i] == null)
                continue;

            optionVisuals[i]
                .gameObject
                .SetActive(active);

            if (active)
            {
                optionVisuals[i]
                    .SetOption(
                        options[i]
                    );
            }
        }

        RefreshSelectionHighlight();

        Debug.Log(
            "[MapManager_Multi] " +
            $"옵션 표시 완료 | " +
            $"Options: {_options.Length} | " +
            $"Visuals: {optionVisuals.Length}"
        );
    }


    public void MoveSelection(
        int delta)
    {
        if (_options == null ||
            _options.Length == 0)
        {
            return;
        }

        int count =
            _options.Length;

        _selectedIndex =
            ((_selectedIndex + delta)
            % count + count)
            % count;

        RefreshSelectionHighlight();

        Debug.Log(
            "[MapManager_Multi] " +
            $"현재 선택 Index: {_selectedIndex}"
        );
    }


    private void RefreshSelectionHighlight()
    {
        if (optionVisuals == null)
            return;

        for (int i = 0;
             i < optionVisuals.Length;
             i++)
        {
            if (optionVisuals[i] == null)
                continue;

            bool selected =
                i == _selectedIndex &&
                i < _options.Length;

            optionVisuals[i]
                .SetSelected(
                    selected
                );
        }
    }


    public void ConfirmLocalPreparation()
    {
        if (_options == null ||
            _options.Length == 0)
        {
            Debug.LogWarning(
                "[MapManager_Multi] " +
                "현재 선택지가 없습니다."
            );

            return;
        }

        if (preparationManager == null)
        {
            Debug.LogError(
                "[MapManager_Multi] " +
                "MultiPreparationManager가 없습니다."
            );

            return;
        }

        Debug.Log(
            "[MapManager_Multi] " +
            $"선택 확정 요청 | " +
            $"Index: {_selectedIndex}"
        );

        // 다음 단계:
        // NetworkMatchBridge
        // → ServerRpc
        // → 서버에서 실제 선택 적용
    }


    public void NotifyBothPlayersConfirmed()
    {
        Debug.Log(
            "[MapManager_Multi] " +
            "Host / Client 모두 준비 완료"
        );

        SetPreparationViewActive(false);

        GameManager_Multi.Instance
            ?.StartBattle();
    }
}