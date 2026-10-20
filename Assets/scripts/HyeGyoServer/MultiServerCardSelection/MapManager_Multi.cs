using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class MapManager_Multi : MonoBehaviour
{
    public static MapManager_Multi Instance { get; private set; }
    [SerializeField] private GameObject preparationViewRoot;
    [SerializeField] private MultiPreparationManager preparationManager;
    [SerializeField] private NetworkMatchBridge networkMatchBridge;
    [SerializeField] private GameObject panelGameObject;
    [SerializeField] private DeckDisplay myDeckDisplay;
    [SerializeField] private TextMeshPro remainingText;
    [SerializeField] private float myDeckCardSize = 0.7f;
    [SerializeField] private int myDeckColumns = 6;
    private PopupManager popupManager;
    private PreparationOptionVisual_Multi[] optionVisuals;
    private PreparationOption[] options;
    private readonly List<CardDefinition> localDeck = new();
    private int selectedIndex;
    private bool ownsInput, deckOpen, pending;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panelGameObject == null) panelGameObject = transform.Find("PanelGameObject")?.gameObject;
        if (myDeckDisplay == null && panelGameObject != null)
        {
            myDeckDisplay = panelGameObject.GetComponentInChildren<DeckDisplay>(true);
            if (myDeckDisplay == null)
            {
                var display = new GameObject("MySelectedCardsDisplay", typeof(RectTransform), typeof(DeckDisplay));
                display.transform.SetParent(panelGameObject.transform, false);
                myDeckDisplay = display.GetComponent<DeckDisplay>();
            }
        }
        // Use the valid PopupComponent resource, avoiding the conflicted PopupManager prefab metadata.
        var popup = new GameObject("MySelectedCardsPopup");
        popup.SetActive(false);
        popup.transform.SetParent(transform, false);
        popup.transform.localPosition = new Vector3(5.2f, 1f, 0f);
        GameObject component = Resources.Load<GameObject>("Prefabs/PopUpComponent");
        if (component != null) Instantiate(component, popup.transform);
        popupManager = popup.AddComponent<PopupManager>();
        popup.SetActive(true);
        optionVisuals = new PreparationOptionVisual_Multi[3];
        for (int i = 0; i < 3; i++)
        {
            Transform target = transform.Find($"MapSelectPanel/Panel{i + 1}/EnemyDisplay");
            if (target == null) continue;
            optionVisuals[i] = target.GetComponent<PreparationOptionVisual_Multi>();
            if (optionVisuals[i] == null) optionVisuals[i] = target.gameObject.AddComponent<PreparationOptionVisual_Multi>();
            optionVisuals[i].SetPopupManager(popupManager);
        }
        if (remainingText == null && panelGameObject != null)
        {
            var label = new GameObject("RemainingChoices", typeof(TextMeshPro));
            label.transform.SetParent(transform, false);
            label.transform.position = panelGameObject.transform.position + new Vector3(-4f, 1.5f, 0f);
            remainingText = label.GetComponent<TextMeshPro>();
            remainingText.fontSize = 3;
            remainingText.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        }
    }

    private void Start()
    {
        if (popupManager != null && !popupManager.enabled) popupManager.enabled = true;
        if (networkMatchBridge == null) networkMatchBridge = FindFirstObjectByType<NetworkMatchBridge>();
        SetRemainingChoices(10);
        networkMatchBridge?.RequestBeginPreparation();
    }

    public void ShowOptions(PreparationOption[] newOptions)
    {
        options = newOptions;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Length - 1);
        pending = false;
        for (int i = 0; i < optionVisuals.Length; i++)
        {
            if (optionVisuals[i] == null) continue;
            optionVisuals[i].gameObject.SetActive(true);
            optionVisuals[i].SetOption(options[i]);
        }
        if (!ownsInput && PlayerInputManager.Instance != null)
        {
            PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
            {
                ["Left"] = () => MoveSelection(-1),
                ["Right"] = () => MoveSelection(1),
                ["Select"] = ConfirmLocalPreparation,
                ["ViewDeck"] = OpenDeck
            });
            ownsInput = true;
        }
        if (!deckOpen) RefreshSelection();
    }

    public void MoveSelection(int delta)
    {
        if (pending || deckOpen || options == null || options.Length == 0) return;
        selectedIndex = (selectedIndex + delta + options.Length) % options.Length;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (PreparationOptionVisual_Multi visual in optionVisuals) visual?.SetSelected(false);
        if (options != null && options.Length > selectedIndex) optionVisuals[selectedIndex]?.SetSelected(true);
    }

    public void ConfirmLocalPreparation()
    {
        if (pending || deckOpen || options == null || options.Length == 0 || networkMatchBridge == null) return;
        pending = true;
        networkMatchBridge.RequestConfirmPreparationOption(selectedIndex);
    }

    private void OpenDeck()
    {
        if (pending || deckOpen || myDeckDisplay == null || localDeck.Count == 0) return;
        deckOpen = true;
        foreach (PreparationOptionVisual_Multi visual in optionVisuals) visual?.SetSelected(false);
        myDeckDisplay.SetPopupManager(popupManager);
        RefreshMyDeck(localDeck);
        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"] = () => myDeckDisplay.MoveSelectionHorizontal(-1),
            ["Right"] = () => myDeckDisplay.MoveSelectionHorizontal(1),
            ["Up"] = () => myDeckDisplay.MoveSelectionVertical(-1),
            ["Down"] = () => myDeckDisplay.MoveSelectionVertical(1),
            ["Cancel"] = CloseDeck,
            ["ViewDeck"] = CloseDeck
        });
        myDeckDisplay.SelectFirst();
    }

    private void CloseDeck()
    {
        if (!deckOpen) return;
        PlayerInputManager.Instance?.Unload();
        deckOpen = false;
        myDeckDisplay.Deselect();
        myDeckDisplay.SetPopupManager(null);
        popupManager.Hide();
        RefreshSelection();
    }

    public void ApplyConfirmedDeckSnapshot(CardDefinition[] deck)
    {
        var previous = localDeck.ToArray();
        localDeck.Clear();
        if (deck != null)
            foreach (CardDefinition card in deck) localDeck.Add(card.Clone());
        RefreshMyDeck(localDeck);
        foreach (CardDefinition card in previous) Destroy(card);
    }

    public void RefreshMyDeck(List<CardDefinition> deck)
    {
        if (myDeckDisplay == null || panelGameObject == null) return;
        myDeckDisplay.gameObject.SetActive(true);
        Vector3 parentScale = myDeckDisplay.transform.parent.lossyScale;
        myDeckDisplay.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        SpriteRenderer panel = panelGameObject.GetComponent<SpriteRenderer>();
        Vector2 size = panel != null ? (Vector2)panel.bounds.size * 0.9f : new Vector2(8f, 2f);
        myDeckDisplay.SetSize(size);
        myDeckDisplay.SetPopupManager(deckOpen ? popupManager : null);
        int columns = Mathf.Max(1, myDeckColumns);
        // CardVisual's native sprite determines dimensions; cap the Multi scale to the panel height.
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Card");
        SpriteRenderer background = prefab != null ? prefab.transform.Find("Front/background")?.GetComponent<SpriteRenderer>() : null;
        float scale = Mathf.Max(0.01f, myDeckCardSize);
        if (background != null && background.sprite != null)
        {
            Vector2 native = background.sprite.bounds.size;
            scale = Mathf.Min(scale, size.x / columns / native.x, size.y / native.y);
        }
        myDeckDisplay.SetDeck(deck, null, scale, columns);
        if (deckOpen) myDeckDisplay.SelectFirst();
    }

    public void SetRemainingChoices(int remaining)
    {
        if (remainingText != null) remainingText.text = $"{remaining}/10";
    }

    public void ShowWaitingForOpponent()
    {
        pending = false;
        options = null;
        CloseDeck();
        if (ownsInput) PlayerInputManager.Instance?.Unload();
        ownsInput = false;
        foreach (PreparationOptionVisual_Multi visual in optionVisuals) if (visual != null) visual.gameObject.SetActive(false);
        popupManager.Hide();
    }

    public void SetPreparationViewActive(bool active)
    {
        Transform candidates = transform.Find("MapSelectPanel");
        if (candidates != null) candidates.gameObject.SetActive(active);
    }

    public void NotifyBothPlayersConfirmed()
    {
        SetPreparationViewActive(false);
        GameManager_Multi.Instance?.StartBattle();
    }

    private void OnDisable()
    {
        if (Instance != this) return;
        CloseDeck();
        if (ownsInput) PlayerInputManager.Instance?.Unload();
        ownsInput = false;
        if (popupManager != null) popupManager.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        CloseDeck();
        if (ownsInput) PlayerInputManager.Instance?.Unload();
        foreach (CardDefinition card in localDeck) if (card != null) Destroy(card);
        Instance = null;
    }
}
