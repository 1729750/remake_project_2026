using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// MultiPlayMode_Oracle 씬에서 RewardManager 자리(RewardPanel)에 들어가는 카드 선택 화면.
///   위: 지금까지의 내 덱 (DeckDisplay, 카드가 늘수록 작아진다)
///   아래: 남은 선택 횟수 "9 / 10", 선택지 카드 3장, 오른쪽 아래 건너뛰기 버튼
/// 선택지는 [카드 추가 / 강화 / 무작위]. 강화 선택지는 강화 후 모습의 카드를 보여주고,
/// 고르면 위쪽 덱에서 바뀔 카드가 함께 강조된다. (같은 카드 자리가 바뀐 수치로 교체된다)
/// 싱글 보상 화면처럼 ←/→ 로 고르고 선택 키로 확정한다. 카드 오른쪽 끝에서 한 번 더 →면 건너뛰기 버튼.
/// 고른 카드의 효과 설명은 그 카드 바로 아래에 뜬다.
/// 위치/크기는 카메라 중심 기준 월드 단위(화면 높이 10)라 Inspector에서 조절할 수 있다.
/// </summary>
public sealed class OracleCardPickView : MonoBehaviour
{
    [SerializeField] private PopupManager popupManager;

    [Header("Scene")]
    [SerializeField] private ButtonComponent skipButton;
    [SerializeField] private TMP_Text remainingText;

    [Header("Layout (카메라 중심 기준)")]
    [SerializeField] private Vector2 deckCenter = new Vector2(0f, 2.4f);
    [SerializeField] private Vector2 deckSize = new Vector2(13f, 3.6f);
    [SerializeField] private float optionsY = -1.6f;
    [SerializeField] private float optionHeight = 3.6f;
    [SerializeField] private float optionSpacing = 1.5f;   // 카드 폭의 몇 배 간격
    [SerializeField] private float popupGap = 0.1f;

    private GameObject cardPrefab;
    private readonly List<CardVisual> cards = new List<CardVisual>();
    private DeckDisplay deckDisplay;
    private string shownOffers;
    private string shownDeck;
    private int selectedIndex;
    private bool picked;
    private bool inputLoaded;
    private int remaining;
    private int total;
    private CardView[] offers;

    /// <summary>선택지 카드를 골랐을 때 (index).</summary>
    public event Action<int> Picked;

    /// <summary>건너뛰기를 골랐을 때.</summary>
    public event Action Skipped;

    private bool SkipSelected => selectedIndex == cards.Count;

    private CardView SelectedOffer =>
        offers != null && selectedIndex >= 0 && selectedIndex < offers.Length && selectedIndex < cards.Count ? offers[selectedIndex] : null;

    private void Awake()
    {
        skipButton?.SetAction(Confirm);
    }

    public void Show(MatchStateView state, OracleCardLibrary library)
    {
        gameObject.SetActive(true);
        Vector3 origin = CameraCenter();

        string deckKey = state.myDeck == null ? "" : string.Join(",", Array.ConvertAll(state.myDeck, c => c.name + "+" + c.level));
        if (deckKey != shownDeck)
        {
            shownDeck = deckKey;
            // 매번 새로 만든다: 기존 카드는 Destroy가 프레임 끝에 처리돼서, 재사용하면 강조할 카드 순서가 섞인다
            if (deckDisplay != null) Destroy(deckDisplay.gameObject);
            deckDisplay = library.ShowDeck(null, transform, origin + (Vector3)deckCenter, deckSize, state.myDeck);
        }

        // 라운드가 바뀌어 다른 카드가 왔을 때만 새로 놓는다
        string offerKey = state.round + ":" + (state.offers == null ? "" : string.Join(",", Array.ConvertAll(state.offers, o => o.name)));
        if (offerKey != shownOffers)
        {
            shownOffers = offerKey;
            SpawnOptions(state.offers, library, origin);
        }
        offers = state.offers;

        picked = state.picked;
        total = state.totalRounds;
        remaining = state.totalRounds - (state.round - 1) - (state.picked ? 1 : 0);
        SetTimeLeft(-1f);
        RefreshSelection();

        if (!picked) LoadInput();
        else UnloadInput();
    }

    /// <summary>남은 횟수 옆에 이번 라운드 남은 초. 음수면 초는 생략.</summary>
    public void SetTimeLeft(float seconds)
    {
        if (remainingText == null) return;
        string text = $"{remaining} / {total}";
        if (seconds >= 0f && !picked) text += $"  <size=60%>({Mathf.CeilToInt(seconds)})</size>";
        if (SelectedOffer?.offerKind == "enhance") text += "\n<size=60%>덱의 카드 강화</size>";
        if (remainingText.text != text) remainingText.text = text;
    }

    public void Hide()
    {
        UnloadInput();
        ClearOptions();
        if (deckDisplay != null) Destroy(deckDisplay.gameObject);
        deckDisplay = null;
        shownOffers = null;
        shownDeck = null;
        popupManager?.Show(null);
        gameObject.SetActive(false);
    }

    private void OnDisable() => UnloadInput();

    private static Vector3 CameraCenter()
    {
        Camera cam = Camera.main;
        return cam != null ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f) : Vector3.zero;
    }

    // =========================================================
    // Options (RewardManager.ShowRewardCard 와 같은 카드)
    // =========================================================

    private void SpawnOptions(CardView[] offers, OracleCardLibrary library, Vector3 origin)
    {
        ClearOptions();
        if (offers == null) return;

        if (cardPrefab == null)
            cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (cardPrefab == null) return;

        Vector2 native = OracleCardLibrary.NativeCardSize();
        float scale = native.y > 0f ? optionHeight / native.y : 1f;
        float spacing = native.x * scale * optionSpacing;

        for (int i = 0; i < offers.Length; i++)
        {
            GameObject obj = Instantiate(cardPrefab, transform);
            CardVisual visual = obj.GetComponent<CardVisual>();
            if (visual == null) visual = obj.AddComponent<CardVisual>();
            visual.SetPopupManager(popupManager);

            // 강화 선택지는 강화 후 수치로 만든 정의 (그림은 같은 카드)
            CardDefinition definition = library.DefinitionFor(offers[i]);
            if (definition != null)
            {
                // 아직 소유자가 없는 카드라 owner 없이 표시 전용 CardInstance로 감싼다 (싱글과 동일)
                var instance = new CardInstance(definition, null);
                instance.SetVisual(visual);
                instance.SetFace(true);
                instance.SetLayer("UI");
            }
            else
            {
                visual.SetFace(true);
                visual.SetLayer("UI");
                library.Apply(visual, offers[i], false, false);
            }

            obj.transform.localScale = Vector3.one * scale;
            obj.transform.position = origin + new Vector3((i - (offers.Length - 1) / 2f) * spacing, optionsY, 0f);
            cards.Add(visual);
        }

        selectedIndex = 0;
    }

    private void ClearOptions()
    {
        foreach (CardVisual card in cards)
            if (card != null) Destroy(card.gameObject);
        cards.Clear();
    }

    private void RefreshSelection()
    {
        // 같은 PopupManager를 쓰므로 먼저 전부 끄고 고른 카드만 켠다
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].SetSelected(false);
            cards[i].SetUnplayable(picked && i != selectedIndex);
        }

        if (selectedIndex >= 0 && selectedIndex < cards.Count)
        {
            cards[selectedIndex].SetSelected(true);
            MovePopupBelow(cards[selectedIndex]);
        }

        skipButton?.SetSelected(SkipSelected && !picked);
        HighlightEnhanceTarget();
        SetTimeLeft(-1f);
    }

    // 강화 선택지를 고르고 있으면 위쪽 덱에서 바뀔 카드를 강조한다
    private void HighlightEnhanceTarget()
    {
        if (deckDisplay == null) return;
        CardVisual[] deckCards = deckDisplay.GetComponentsInChildren<CardVisual>(true); // 덱 순서대로 생성됨
        CardView offer = SelectedOffer;
        int target = offer != null && offer.offerKind == "enhance" ? offer.target : -1;
        for (int i = 0; i < deckCards.Length; i++)
            deckCards[i].SetSelected(i == target);
    }

    // 효과 설명 팝업을 고른 카드 바로 아래로 옮긴다
    private void MovePopupBelow(CardVisual card)
    {
        if (popupManager == null) return;

        PopupComponent popup = popupManager.GetComponentInChildren<PopupComponent>(true);
        if (popup == null) return;

        float popupHalfHeight = 0f;
        SpriteRenderer background = popup.GetComponent<SpriteRenderer>();
        if (background != null && background.sprite != null)
        {
            Vector2 size = background.drawMode == SpriteDrawMode.Simple ? (Vector2)background.sprite.bounds.size : background.size;
            popupHalfHeight = size.y * popup.transform.lossyScale.y / 2f;
        }

        Vector3 cardPosition = card.transform.position;
        float cardBottom = cardPosition.y - card.GetBackgroundSize().y / 2f;
        Vector3 target = new Vector3(cardPosition.x, cardBottom - popupGap - popupHalfHeight, popup.transform.position.z);

        popupManager.transform.position += target - popup.transform.position;
    }

    // =========================================================
    // Input (싱글 RewardManager와 같은 "Select" 맵: ←/→, 선택)
    // =========================================================

    private void Move(int delta)
    {
        if (picked || cards.Count == 0) return;
        int count = cards.Count + 1; // 마지막 칸 = 건너뛰기
        selectedIndex = ((selectedIndex + delta) % count + count) % count;
        RefreshSelection();
        SoundManager.Instance?.Play(EffectSound.MoveSelect1);
    }

    private void Confirm()
    {
        if (picked || cards.Count == 0) return;
        picked = true;
        UnloadInput();

        if (SkipSelected)
        {
            popupManager?.Show(null);
            RefreshSelection();
            Skipped?.Invoke();
        }
        else
        {
            RefreshSelection();
            Picked?.Invoke(selectedIndex);
        }
    }

    private void LoadInput()
    {
        if (inputLoaded || PlayerInputManager.Instance == null) return;
        inputLoaded = true;
        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"] = () => Move(-1),
            ["Right"] = () => Move(1),
            ["Select"] = Confirm,
        });
    }

    private void UnloadInput()
    {
        if (!inputLoaded) return;
        inputLoaded = false;
        PlayerInputManager.Instance?.Unload();
    }
}
