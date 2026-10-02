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

    FindOptionVisuals();

    if (preparationViewRoot != null)
    {
        preparationViewRoot.SetActive(true);
    }

    Debug.Log(
        "[MapManager_Multi] Awake - 활성화"
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
private void FindOptionVisuals()
{
    string[] paths =
    {
        "MapSelectPanel/Panel1/EnemyDisplay",
        "MapSelectPanel/Panel2/EnemyDisplay",
        "MapSelectPanel/Panel3/EnemyDisplay"
    };

    optionVisuals =
        new PreparationOptionVisual_Multi[
            paths.Length
        ];

    for (int i = 0; i < paths.Length; i++)
    {
        Transform target =
            transform.Find(
                paths[i]
            );

        if (target == null)
        {
            Debug.LogError(
                "[MapManager_Multi] " +
                $"찾지 못함: {paths[i]}"
            );

            continue;
        }

        PreparationOptionVisual_Multi visual =
            target.GetComponent<
                PreparationOptionVisual_Multi
            >();

        // Inspector에서 직접 안 붙여놨어도
        // 런타임에 자동으로 붙인다.
        if (visual == null)
        {
            visual =
                target.gameObject.AddComponent<
                    PreparationOptionVisual_Multi
                >();
        }

        optionVisuals[i] =
            visual;

        Debug.Log(
            "[MapManager_Multi] " +
            $"OptionVisual 연결 성공: {paths[i]}"
        );
    }
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