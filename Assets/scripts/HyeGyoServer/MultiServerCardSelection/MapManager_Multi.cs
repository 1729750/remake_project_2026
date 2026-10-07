using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    [SerializeField]
    private NetworkMatchBridge networkMatchBridge;


    // Inspector에 직접 넣지 않고
    // 자식의 PreparationOptionVisual_Multi를 자동 검색
    private PreparationOptionVisual_Multi[] optionVisuals;

    [Header("My Deck View")]
    [SerializeField]
    private DeckDisplay myDeckDisplay;

    [SerializeField]
    private float deckCardSize = 0.7f;

    [SerializeField]
    private int deckColumns = 6;

    [Header("My Deck Display")]
     [SerializeField]
    private TextMeshPro remainingText;

    [SerializeField]
    private float myDeckCardSize = 0.7f;

    [SerializeField]
    private int myDeckColumns = 6;

    private readonly List<CardDefinition>
        _localDeckView =
            new List<CardDefinition>();


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
    
    SetRemainingChoices(10);
    if (networkMatchBridge == null)
    {
        networkMatchBridge =
            FindFirstObjectByType<
                NetworkMatchBridge
            >();
    }

    if (networkMatchBridge == null)
    {
        Debug.LogError(
            "[MapManager_Multi] " +
            "NetworkMatchBridge를 찾을 수 없습니다."
        );

        return;
    }

    Debug.Log(
        "[MapManager_Multi] " +
        "첫 준비 선택지 요청"
    );

    networkMatchBridge
        .RequestBeginPreparation();
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
            "[MapManager_Multi] 표시할 옵션이 없습니다."
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
        if (optionVisuals[i] == null)
            continue;

        bool active =
            i < count;

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

    LoadPreparationInput();

    Debug.Log(
        "[MapManager_Multi] " +
        $"옵션 표시 완료 | " +
        $"Options: {_options.Length} | " +
        $"Visuals: {optionVisuals.Length}"
    );
}
private void LoadPreparationInput()
{
    if (PlayerInputManager.Instance == null)
    {
        Debug.LogError(
            "[MapManager_Multi] " +
            "PlayerInputManager.Instance가 없습니다."
        );

        return;
    }

    PlayerInputManager.Instance.Load(
        "Select",
        new Dictionary<string, Action>
        {
            ["Left"] = () =>
            {
                MoveSelection(-1);
            },

            ["Right"] = () =>
            {
                MoveSelection(1);
            },

            ["Select"] = () =>
            {
                ConfirmLocalPreparation();
            }
        }
    );

    Debug.Log(
        "[MapManager_Multi] " +
        "Preparation 입력 로드 완료"
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

    networkMatchBridge
        .RequestConfirmPreparationOption(
            _selectedIndex
        );
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

public void ApplyConfirmedOptionToLocalView(
    PreparationOption option)
{
    switch (option.Type)
    {
        case PreparationOptionType.AddCard:
        {
            if (option.Card != null)
            {
                _localDeckView.Add(
                    option.Card
                );

                RefreshMyDeck(
                    _localDeckView
                );
            }

            break;
        }

        case PreparationOptionType.EnhanceCard:
        {
            // 현재는 강화 표시 로직이 아직 미완성
            // 그래도 현재 덱을 다시 그리도록 함
            RefreshMyDeck(
                _localDeckView
            );

            break;
        }
    }
}
public void RefreshMyDeck(
    List<CardDefinition> deck)
{
    if (myDeckDisplay == null)
    {
        Debug.LogError(
            "[MapManager_Multi] myDeckDisplay가 없습니다."
        );

        return;
    }

    if (!myDeckDisplay.gameObject.activeSelf)
    {
        myDeckDisplay.gameObject.SetActive(true);
    }

    myDeckDisplay.SetDeck(
        deck,
        null,
        myDeckCardSize,
        myDeckColumns
    );

    Debug.Log(
        "[MapManager_Multi] " +
        $"내 덱 갱신 | Count: {(deck != null ? deck.Count : 0)}"
    );
}
public void SetRemainingChoices(
    int remaining)
{
    if (remainingText != null)
    {
        remainingText.text =
            $"{remaining}/10";
    }
}

        public void ShowWaitingForOpponent()
    {
        _options = null;

        if (PlayerInputManager.Instance != null)
        {
            PlayerInputManager.Instance.Unload();
        }

        Debug.Log(
            "[MapManager_Multi] " +
            "내 준비 완료 → 상대 플레이어 대기"
        );
    }
}