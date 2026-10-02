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

private void Awake()
{
    if (Instance != null &&
        Instance != this)
    {
        Destroy(gameObject);
        return;
    }

    Instance = this;

    // 자기 오브젝트 활성화
    if (!gameObject.activeSelf)
    {
        gameObject.SetActive(true);
    }

    // 준비 화면 루트 활성화
    if (preparationViewRoot != null &&
        !preparationViewRoot.activeSelf)
    {
        preparationViewRoot.SetActive(true);
    }

    Debug.Log(
        "[MapManager_Multi] Awake - 활성화"
    );
}

private void Start()
{
    gameObject.SetActive(true);

    if (preparationViewRoot != null)
    {
        preparationViewRoot.SetActive(true);
    }

    Debug.Log(
        "[MapManager_Multi] Start - 활성화"
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
                "[MapManager_Multi] 표시할 옵션이 없습니다."
            );

            return;
        }

        _options = options;
        _selectedIndex = 0;

        Debug.Log(
            "[MapManager_Multi] " +
            $"옵션 표시 완료 | Count: {_options.Length}"
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

        Debug.Log(
            "[MapManager_Multi] " +
            $"현재 선택 Index: {_selectedIndex}"
        );
    }


    public void ConfirmLocalPreparation()
    {
        if (_options == null ||
            _options.Length == 0)
        {
            Debug.LogWarning(
                "[MapManager_Multi] 현재 선택지가 없습니다."
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
            $"선택 확정 요청 | Index: {_selectedIndex}"
        );

        // 다음 단계에서 NetworkMatchBridge를 통해
        // Server로 _selectedIndex 전송
    }


    public void NotifyBothPlayersConfirmed()
    {
        Debug.Log(
            "[MapManager_Multi] " +
            "Host / Client 모두 준비 완료"
        );

        // 준비 화면 끄기
        SetPreparationViewActive(false);

        // 이후 Battle 시작
        GameManager_Multi.Instance
            ?.StartBattle();
    }
}