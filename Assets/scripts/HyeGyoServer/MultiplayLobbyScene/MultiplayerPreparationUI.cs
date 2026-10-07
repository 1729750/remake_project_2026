using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single 모드의 Card/RewardDisplay/DeckDisplay prefab을 그대로 사용해
/// Lobby에서 초기 카드와 10회 준비 선택을 표시한다.
/// 서버 판정은 하지 않고 index 요청과 로컬 표시만 담당한다.
/// </summary>
public sealed class MultiplayerPreparationUI : MonoBehaviour
{
    private enum ViewMode
    {
        None,
        InitialCard,
        Preparation,
        Deck,
        Waiting
    }

    private sealed class VisualItem
    {
        public GameObject Root;
        public Action<bool> SetSelected;
    }

    private NetworkMatchBridge bridge;
    private CardOptionGenerator generator;
    private bool initialized;

    private readonly List<VisualItem> visuals = new();
    private readonly List<CardDefinition> localDeck = new();

    private GameObject visualRoot;
    private TextMeshPro instructionText;
    private DeckDisplay deckDisplay;
    private PreparationOption[] currentOptions;
    private CardDefinition[] initialCandidates;
    private int selectedIndex;
    private int remaining = MultiPreparationManager.MaxRounds;
    private ViewMode mode;

    public void Initialize(
        NetworkMatchBridge networkBridge,
        CardOptionGenerator optionGenerator)
    {
        if (initialized)
            return;

        bridge = networkBridge;
        generator = optionGenerator;

        if (bridge == null || generator == null)
        {
            Debug.LogError(
                "[MultiplayerPreparationUI] Bridge/Generator가 없습니다."
            );
            return;
        }

        initialized = true;

        bridge.InitialCardCandidatesReceived +=
            ShowInitialCandidates;
        bridge.InitialCardConfirmedReceived +=
            HandleInitialCardConfirmed;
        bridge.PreparationOptionsReceived +=
            ShowPreparationOptions;
        bridge.PreparationResultReceived +=
            HandlePreparationResult;
        bridge.PreparationSkippedReceived +=
            HandlePreparationSkipped;
        bridge.LocalMessage += HandleLocalMessage;
        bridge.MatchStateChanged += HandleMatchStateChanged;
    }

    private void OnDestroy()
    {
        if (bridge == null)
            return;

        bridge.InitialCardCandidatesReceived -=
            ShowInitialCandidates;
        bridge.InitialCardConfirmedReceived -=
            HandleInitialCardConfirmed;
        bridge.PreparationOptionsReceived -=
            ShowPreparationOptions;
        bridge.PreparationResultReceived -=
            HandlePreparationResult;
        bridge.PreparationSkippedReceived -=
            HandlePreparationSkipped;
        bridge.LocalMessage -= HandleLocalMessage;
        bridge.MatchStateChanged -= HandleMatchStateChanged;

        ClearVisuals();
        ClearLocalDeck();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!initialized || keyboard == null)
            return;

        if (mode == ViewMode.Deck)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame ||
                keyboard.aKey.wasPressedThisFrame)
                deckDisplay?.MoveSelectionHorizontal(-1);

            if (keyboard.rightArrowKey.wasPressedThisFrame ||
                keyboard.dKey.wasPressedThisFrame)
                deckDisplay?.MoveSelectionHorizontal(1);

            if (keyboard.upArrowKey.wasPressedThisFrame ||
                keyboard.wKey.wasPressedThisFrame)
                deckDisplay?.MoveSelectionVertical(-1);

            if (keyboard.downArrowKey.wasPressedThisFrame ||
                keyboard.sKey.wasPressedThisFrame)
                deckDisplay?.MoveSelectionVertical(1);

            if (keyboard.escapeKey.wasPressedThisFrame ||
                keyboard.tabKey.wasPressedThisFrame)
                CloseDeck();

            return;
        }

        if (mode != ViewMode.InitialCard &&
            mode != ViewMode.Preparation)
            return;

        if (keyboard.leftArrowKey.wasPressedThisFrame ||
            keyboard.aKey.wasPressedThisFrame)
            MoveSelection(-1);

        if (keyboard.rightArrowKey.wasPressedThisFrame ||
            keyboard.dKey.wasPressedThisFrame)
            MoveSelection(1);

        if (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame)
            ConfirmSelection();

        if (mode == ViewMode.Preparation &&
            keyboard.sKey.wasPressedThisFrame)
            SkipPreparation();

        if (keyboard.tabKey.wasPressedThisFrame)
            OpenDeck();
    }

    private void ShowInitialCandidates(
        CardDefinition[] candidates)
    {
        HideLobbyPanels();
        EnsureVisualRoot();
        ClearVisuals();

        initialCandidates = candidates;
        currentOptions = null;
        selectedIndex = 0;
        mode = ViewMode.InitialCard;

        if (candidates == null || candidates.Length == 0)
        {
            SetInstruction("초기 카드 후보가 없습니다.");
            return;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            AddCardVisual(candidates[i], i);
        }

        LayoutVisuals();
        RefreshSelection();
        SetInstruction(
            "초기 카드 선택  |  A/D 또는 ←/→  |  Enter 확정"
        );
    }

    private void HandleInitialCardConfirmed(
        CardDefinition selectedCard)
    {
        ClearLocalDeck();

        AddLocalCard(generator.GetBasicAttackCard());
        AddLocalCard(generator.GetBasicDefenseCard());
        AddLocalCard(selectedCard);

        mode = ViewMode.Waiting;
        ClearVisuals();
        SetInstruction("상대의 초기 카드 선택을 기다리는 중...");
    }

    private void ShowPreparationOptions(
        PreparationOption[] options)
    {
        EnsureVisualRoot();
        ClearVisuals();

        currentOptions = options;
        initialCandidates = null;
        selectedIndex = 0;
        mode = ViewMode.Preparation;

        if (options == null || options.Length == 0)
        {
            SetInstruction("준비 선택지가 없습니다.");
            return;
        }

        for (int i = 0; i < options.Length; i++)
        {
            PreparationOption option = options[i];

            if (option.Type == PreparationOptionType.AddCard)
                AddCardVisual(option.Card, i);
            else if (option.Type == PreparationOptionType.EnhanceCard)
                AddEnhanceVisual(
                    option.Upgrade,
                    option.TargetCardIndex,
                    i
                );
            else
                AddTextVisual("SKIP", i);
        }

        LayoutVisuals();
        RefreshSelection();
        SetPreparationInstruction();
    }

    private void HandlePreparationResult(
        PreparationOption selected,
        int nextRemaining,
        PreparationOption[] next)
    {
        ApplyOptionToLocalDeck(selected);
        remaining = nextRemaining;

        if (next != null)
        {
            ShowPreparationOptions(next);
            return;
        }

        ShowWaitingForOpponent();
    }

    private void HandlePreparationSkipped(
        int nextRemaining,
        PreparationOption[] next)
    {
        remaining = nextRemaining;

        if (next != null)
        {
            ShowPreparationOptions(next);
            return;
        }

        ShowWaitingForOpponent();
    }

    private void HandleLocalMessage(
        string message)
    {
        SetInstruction(message);
    }

    private void HandleMatchStateChanged()
    {
        if (bridge == null ||
            bridge.CurrentPhase != MatchPhase.Battle)
        {
            return;
        }

        mode = ViewMode.None;
        currentOptions = null;
        initialCandidates = null;
        ClearVisuals();

        if (visualRoot != null)
        {
            Destroy(visualRoot);
            visualRoot = null;
            instructionText = null;
        }
    }

    private void MoveSelection(
        int delta)
    {
        if (visuals.Count == 0)
            return;

        selectedIndex =
            ((selectedIndex + delta) % visuals.Count +
             visuals.Count) % visuals.Count;

        RefreshSelection();
    }

    private void ConfirmSelection()
    {
        if (visuals.Count == 0)
            return;

        if (mode == ViewMode.InitialCard)
        {
            bridge.RequestChooseCard(selectedIndex);
            SetInstruction("초기 카드 선택을 서버가 확인하는 중...");
            return;
        }

        if (mode == ViewMode.Preparation)
        {
            bridge.RequestConfirmPreparationOption(selectedIndex);
            SetInstruction("선택을 서버가 확인하는 중...");
        }
    }

    private void SkipPreparation()
    {
        if (mode != ViewMode.Preparation)
            return;

        bridge.RequestSkipPreparation();
        SetInstruction("이번 선택을 건너뛰는 중...");
    }

    private void OpenDeck()
    {
        if (localDeck.Count == 0 ||
            mode == ViewMode.Deck)
            return;

        EnsureVisualRoot();
        SetVisualsActive(false);

        GameObject prefab =
            Resources.Load<GameObject>(
                "Prefabs/DeckDisplay"
            );

        if (prefab == null)
            return;

        GameObject obj =
            Instantiate(prefab, visualRoot.transform);

        obj.transform.position = Vector3.zero;
        deckDisplay = obj.GetComponent<DeckDisplay>();

        if (deckDisplay == null)
        {
            Destroy(obj);
            return;
        }

        Camera camera = Camera.main;
        float height = camera != null
            ? camera.orthographicSize * 1.6f
            : 8f;
        float width = camera != null
            ? height * camera.aspect
            : 12f;

        deckDisplay.SetSize(new Vector2(width, height));
        deckDisplay.SetDeck(localDeck, null, 0.8f, 5);
        deckDisplay.SelectFirst();

        mode = ViewMode.Deck;
        SetInstruction("내 덱  |  방향키 이동  |  Tab/Esc 닫기");
    }

    private void CloseDeck()
    {
        if (deckDisplay != null)
        {
            Destroy(deckDisplay.gameObject);
            deckDisplay = null;
        }

        SetVisualsActive(true);

        mode = initialCandidates != null
            ? ViewMode.InitialCard
            : currentOptions != null
                ? ViewMode.Preparation
                : ViewMode.Waiting;

        if (mode == ViewMode.Preparation)
            SetPreparationInstruction();
        else if (mode == ViewMode.InitialCard)
            SetInstruction("초기 카드 선택  |  Enter 확정");
        else
            SetInstruction("상대 플레이어를 기다리는 중...");
    }

    private void ShowWaitingForOpponent()
    {
        currentOptions = null;
        initialCandidates = null;
        mode = ViewMode.Waiting;
        ClearVisuals();
        SetInstruction("내 준비 완료  |  상대 플레이어 대기 중...");
    }

    private void ApplyOptionToLocalDeck(
        PreparationOption option)
    {
        if (option.Type == PreparationOptionType.AddCard)
        {
            AddLocalCard(option.Card);
            return;
        }

        if (option.Type == PreparationOptionType.EnhanceCard &&
            option.Upgrade != null &&
            option.TargetCardIndex >= 0 &&
            option.TargetCardIndex < localDeck.Count)
        {
            localDeck[option.TargetCardIndex]
                ?.ApplyUpgrade(option.Upgrade);
        }
    }

    private void AddLocalCard(
        CardDefinition source)
    {
        if (source != null)
            localDeck.Add(source.Clone());
    }

    private void ClearLocalDeck()
    {
        foreach (CardDefinition card in localDeck)
        {
            if (card != null)
                Destroy(card);
        }

        localDeck.Clear();
    }

    private void AddCardVisual(
        CardDefinition card,
        int index)
    {
        if (card == null)
        {
            AddTextVisual("CARD ERROR", index);
            return;
        }

        GameObject prefab =
            Resources.Load<GameObject>("Prefabs/Card");

        if (prefab == null)
            return;

        GameObject obj =
            Instantiate(prefab, visualRoot.transform);

        CardVisual visual =
            obj.GetComponent<CardVisual>();

        if (visual == null)
            visual = obj.AddComponent<CardVisual>();

        CardInstance instance =
            new CardInstance(card, null);

        instance.SetVisual(visual);
        instance.SetFace(true);
        instance.SetLayer("UI");
        obj.transform.localScale = Vector3.one * 2.7f;

        AddClickProxy(obj, index);

        visuals.Add(new VisualItem
        {
            Root = obj,
            SetSelected = instance.SetSelected
        });
    }

    private void AddEnhanceVisual(
        CardUpgrade upgrade,
        int targetCardIndex,
        int index)
    {
        GameObject prefab =
            Resources.Load<GameObject>(
                "Prefabs/RewardDisplay"
            );

        if (prefab == null || upgrade == null)
        {
            AddTextVisual("UP ERROR", index);
            return;
        }

        GameObject obj =
            Instantiate(prefab, visualRoot.transform);

        RewardDisplay display =
            obj.GetComponent<RewardDisplay>();

        display.Init("", null);
        display.SetUpgrade(upgrade);
        obj.transform.localScale = Vector3.one * 1.2f;

        GameObject targetLabelObject =
            new GameObject("EnhanceTargetLabel");
        targetLabelObject.transform.SetParent(obj.transform);
        targetLabelObject.transform.localPosition =
            new Vector3(0f, -2.2f, 0f);

        TextMeshPro targetLabel =
            targetLabelObject.AddComponent<TextMeshPro>();
        targetLabel.alignment = TextAlignmentOptions.Center;
        targetLabel.fontSize = 2.2f;
        targetLabel.GetComponent<Renderer>().sortingLayerID =
            SortingLayer.NameToID("UI");
        targetLabel.text =
            targetCardIndex >= 0 &&
            targetCardIndex < localDeck.Count &&
            localDeck[targetCardIndex] != null
                ? $"UP → {localDeck[targetCardIndex].name}"
                : $"UP → 카드 {targetCardIndex + 1}";

        AddClickProxy(obj, index);

        visuals.Add(new VisualItem
        {
            Root = obj,
            SetSelected = display.SetSelected
        });
    }

    private void AddTextVisual(
        string text,
        int index)
    {
        GameObject obj = new(text);
        obj.transform.SetParent(visualRoot.transform);

        TextMeshPro label =
            obj.AddComponent<TextMeshPro>();

        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 5;
        label.GetComponent<Renderer>().sortingLayerID =
            SortingLayer.NameToID("UI");

        AddClickProxy(obj, index);

        visuals.Add(new VisualItem
        {
            Root = obj,
            SetSelected = selected =>
                label.color = selected
                    ? Color.yellow
                    : Color.white
        });
    }

    private void AddClickProxy(
        GameObject root,
        int index)
    {
        MultiplayerPreparationClickProxy proxy =
            root.AddComponent<MultiplayerPreparationClickProxy>();

        proxy.Initialize(() =>
        {
            selectedIndex = index;
            RefreshSelection();
        });
    }

    private void LayoutVisuals()
    {
        const float spacing = 4.3f;

        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i].Root != null)
            {
                visuals[i].Root.transform.position =
                    new Vector3(
                        (i - (visuals.Count - 1) / 2f) * spacing,
                        0f,
                        0f
                    );
            }
        }
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            visuals[i].SetSelected?.Invoke(
                i == selectedIndex
            );
        }
    }

    private void SetVisualsActive(
        bool active)
    {
        foreach (VisualItem item in visuals)
        {
            item.Root?.SetActive(active);
        }
    }

    private void ClearVisuals()
    {
        if (deckDisplay != null)
        {
            Destroy(deckDisplay.gameObject);
            deckDisplay = null;
        }

        foreach (VisualItem item in visuals)
        {
            if (item.Root != null)
                Destroy(item.Root);
        }

        visuals.Clear();
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot != null)
            return;

        visualRoot =
            new GameObject("MultiplayerPreparationView");

        visualRoot.transform.position = Vector3.zero;

        GameObject textObject =
            new GameObject("PreparationInstruction");

        textObject.transform.SetParent(
            visualRoot.transform
        );
        textObject.transform.position =
            new Vector3(0f, 4.2f, 0f);

        instructionText =
            textObject.AddComponent<TextMeshPro>();

        instructionText.alignment =
            TextAlignmentOptions.Center;
        instructionText.fontSize = 3.5f;
        instructionText.GetComponent<Renderer>().sortingLayerID =
            SortingLayer.NameToID("UI");
    }

    private void SetInstruction(
        string message)
    {
        EnsureVisualRoot();

        if (instructionText != null)
            instructionText.text = message;

        Debug.Log(
            $"[MultiplayerPreparationUI] {message}"
        );
    }

    private void SetPreparationInstruction()
    {
        SetInstruction(
            $"준비 선택 {remaining}/10  |  Enter 확정  |  S 건너뛰기  |  Tab 내 덱"
        );
    }

    private static void HideLobbyPanels()
    {
        LobbyEntryFormUI form =
            FindFirstObjectByType<LobbyEntryFormUI>();

        form?.HideForPreparation();
    }
}

/// <summary>
/// SpriteRenderer 기반 Single 카드 프리팹의 마우스 선택 지원.
/// 키보드 확정과 함께 사용할 수 있다.
/// </summary>
public sealed class MultiplayerPreparationClickProxy : MonoBehaviour
{
    private Action clicked;

    public void Initialize(Action onClick)
    {
        clicked = onClick;

        BoxCollider2D collider =
            gameObject.AddComponent<BoxCollider2D>();

        Bounds bounds = new(transform.position, Vector3.one);
        bool foundRenderer = false;

        foreach (Renderer renderer in
                 GetComponentsInChildren<Renderer>(true))
        {
            if (!foundRenderer)
            {
                bounds = renderer.bounds;
                foundRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (foundRenderer)
        {
            Vector3 scale = transform.lossyScale;
            collider.size = new Vector2(
                scale.x != 0f
                    ? bounds.size.x / Mathf.Abs(scale.x)
                    : bounds.size.x,
                scale.y != 0f
                    ? bounds.size.y / Mathf.Abs(scale.y)
                    : bounds.size.y
            );

            collider.offset = transform.InverseTransformPoint(
                bounds.center
            );
        }
    }

    private void OnMouseDown()
    {
        clicked?.Invoke();
    }
}
