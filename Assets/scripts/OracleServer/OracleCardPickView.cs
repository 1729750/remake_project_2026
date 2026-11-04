using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MultiPlayMode_Oracle 씬에서 RewardManager 자리(RewardPanel)에 들어가는 카드 선택 화면.
/// 싱글의 "카드 획득" 보상과 같은 모양: Card 프리팹 3장을 가로로 놓고 ←/→ 로 고르고 선택 키로 확정한다.
/// </summary>
public sealed class OracleCardPickView : MonoBehaviour
{
    [SerializeField] private PopupManager popupManager;

    private GameObject cardPrefab;
    private readonly List<CardVisual> cards = new List<CardVisual>();
    private string shownOffers;
    private int selectedIndex;
    private bool picked;
    private bool inputLoaded;

    /// <summary>선택 키를 눌렀을 때 (제시된 카드 index).</summary>
    public event Action<int> Picked;

    public void Show(MatchStateView state, OracleCardLibrary library)
    {
        gameObject.SetActive(true);

        // 라운드가 바뀌어 다른 카드가 왔을 때만 새로 놓는다
        string key = state.round + ":" + (state.offers == null ? "" : string.Join(",", Array.ConvertAll(state.offers, o => o.name)));
        if (key != shownOffers)
        {
            shownOffers = key;
            SpawnCards(state.offers, library);
        }

        picked = state.picked;
        RefreshSelection();

        if (!picked) LoadInput();
        else UnloadInput();
    }

    public void Hide()
    {
        UnloadInput();
        ClearCards();
        shownOffers = null;
        gameObject.SetActive(false);
    }

    private void OnDisable() => UnloadInput();

    // =========================================================
    // Cards (RewardManager.ShowRewardCard 와 같은 배치)
    // =========================================================

    private void SpawnCards(CardView[] offers, OracleCardLibrary library)
    {
        ClearCards();
        if (offers == null) return;

        if (cardPrefab == null)
            cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (cardPrefab == null) return;

        foreach (CardView offer in offers)
        {
            GameObject obj = Instantiate(cardPrefab, transform);
            CardVisual visual = obj.GetComponent<CardVisual>();
            if (visual == null) visual = obj.AddComponent<CardVisual>();
            visual.SetPopupManager(popupManager);

            CardDefinition definition = library.Find(offer);
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
                library.Apply(visual, offer, false, false);
            }

            obj.transform.localScale = Vector3.one * 4;
            cards.Add(visual);
        }

        float spacing = (cards.Count > 0 ? cards[0].GetBackgroundSize().x : 1f) * 2f;
        for (int i = 0; i < cards.Count; i++)
            cards[i].transform.localPosition = new Vector3((i - (cards.Count - 1) / 2f) * spacing, 0f, 0f);

        selectedIndex = 0;
    }

    private void ClearCards()
    {
        foreach (CardVisual card in cards)
            if (card != null) Destroy(card.gameObject);
        cards.Clear();
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].SetSelected(i == selectedIndex);
            // 고른 뒤에는 고르지 않은 카드를 어둡게
            cards[i].SetUnplayable(picked && i != selectedIndex);
        }
    }

    // =========================================================
    // Input (싱글 RewardManager와 같은 "Select" 맵: ←/→, 선택)
    // =========================================================

    private void Move(int delta)
    {
        if (picked || cards.Count == 0) return;
        int count = cards.Count;
        selectedIndex = ((selectedIndex + delta) % count + count) % count;
        RefreshSelection();
        SoundManager.Instance?.Play(EffectSound.MoveSelect1);
    }

    private void Confirm()
    {
        if (picked || cards.Count == 0) return;
        picked = true;
        RefreshSelection();
        UnloadInput();
        Picked?.Invoke(selectedIndex);
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
