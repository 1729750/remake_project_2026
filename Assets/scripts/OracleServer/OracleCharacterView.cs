using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// MultiPlayMode_Oracle 씬에서 CharacterManager 자리에 들어가는 표시 전용 컴포넌트.
/// 필드 이름이 CharacterManager와 같아서 SinglePlayMode 씬의 연결(체력바, 코스트, 손패/큐 슬롯 등)을 그대로 쓴다.
/// 게임 규칙은 서버가 처리하고, 여기서는 서버가 보낸 CombatantView를 싱글과 같은 방식으로 그리기만 한다.
/// </summary>
public sealed class OracleCharacterView : MonoBehaviour
{
    private const int MaxCost = 15;

    [SerializeField] private GameObject queueRoots;
    [SerializeField] private bool isHandVisualized;
    [SerializeField] private GameObject handsRoot;
    [SerializeField] private PopupManager handPopupManager;
    [SerializeField] private Transform hpBar;
    [SerializeField] private TextMeshPro hpText;
    [SerializeField] private bool shrinkRight = true;
    [SerializeField] private TextMeshPro costText;
    [SerializeField] private RectTransform effectList;
    [SerializeField] private TextMeshPro defenseText;
    [SerializeField] private GameObject defenseIndicator;
    [SerializeField] private Transform[] defenseIndicatorRestPosition;
    [SerializeField] private float defenseIndicatorMoveDuration = 0.3f;

    private Transform[] handSlots;
    private Transform[] queueSlots;
    private GameObjectPool cardPool;
    private readonly Dictionary<int, CardVisual> cards = new Dictionary<int, CardVisual>();
    private readonly List<EffectDisplay> effectDisplays = new List<EffectDisplay>();
    private string shownEffects;
    private bool shownGuard;
    private Coroutine defenseIndicatorCoroutine;

    private void Awake()
    {
        handSlots = ChildrenOf(handsRoot);
        queueSlots = ChildrenOf(queueRoots);

        Transform deckRoot = new GameObject("Deck").transform;
        deckRoot.SetParent(transform, false);
        cardPool = new GameObjectPool(Resources.Load<GameObject>("Prefabs/Card"), deckRoot);

        ResetDefenseIndicator();
    }

    private static Transform[] ChildrenOf(GameObject root)
    {
        if (root == null) return new Transform[0];
        var result = new List<Transform>();
        foreach (Transform child in root.transform) result.Add(child);
        return result.ToArray();
    }

    // =========================================================
    // Render
    // =========================================================

    public void Render(CombatantView view, OracleCardLibrary library, bool isMe)
    {
        UpdateHpBar(view.hp, view.maxHp);
        if (costText != null) costText.text = $"{view.cost}/{MaxCost}";
        UpdateDefense(view.defense);
        UpdateEffectList(view.effects);
        SetDefenseIndicatorActive(view.guard);

        RenderCards(view, library, isMe);
    }

    /// <summary>전투 전/후: 손패와 큐를 비운다. (체력바 등은 마지막 값 그대로)</summary>
    public void ClearCards()
    {
        foreach (CardVisual visual in cards.Values)
            cardPool.Release(visual.gameObject);
        cards.Clear();
    }

    private void RenderCards(CombatantView view, OracleCardLibrary library, bool isMe)
    {
        var present = new HashSet<int>();
        if (view.hand != null) foreach (CardView c in view.hand) if (!c.IsEmpty) present.Add(c.id);
        if (view.queue != null) foreach (CardView c in view.queue) if (!c.IsEmpty) present.Add(c.id);

        // 사라진 카드(사용되어 덱으로 돌아간 카드 등)는 풀에 반납
        var gone = new List<int>();
        foreach (int id in cards.Keys) if (!present.Contains(id)) gone.Add(id);
        foreach (int id in gone)
        {
            cardPool.Release(cards[id].gameObject);
            cards.Remove(id);
        }

        // 손패: HandManager.CreateCardVisual 과 같은 방식
        for (int i = 0; view.hand != null && i < view.hand.Length && i < handSlots.Length; i++)
        {
            CardView card = view.hand[i];
            if (card.IsEmpty) continue;

            CardVisual visual = GetOrCreate(card.id, handSlots[i], out bool created);
            if (!created) MoveToSlot(visual, handSlots[i]);

            bool faceUp = isHandVisualized && !card.IsHidden;
            visual.SetFace(faceUp);
            if (faceUp)
                library.Apply(visual, card, selected: isMe && view.selectedIndex == i, unplayable: isMe && !card.playable);
        }

        // 큐: QueueManager.AddCard 처럼 앞면으로 슬롯에 놓는다
        for (int i = 0; view.queue != null && i < view.queue.Length && i < queueSlots.Length; i++)
        {
            CardView card = view.queue[i];
            CardVisual visual = GetOrCreate(card.id, queueSlots[i], out bool created);
            if (!created) MoveToSlot(visual, queueSlots[i]);

            visual.SetFace(true);
            library.Apply(visual, card, selected: false, unplayable: false);
        }
    }

    private CardVisual GetOrCreate(int id, Transform slot, out bool created)
    {
        if (cards.TryGetValue(id, out CardVisual existing))
        {
            created = false;
            return existing;
        }

        GameObject obj = cardPool.Get();
        obj.transform.SetParent(slot);
        obj.transform.localPosition = Vector3.zero;

        CardVisual visual = obj.GetComponent<CardVisual>();
        if (visual == null) visual = obj.AddComponent<CardVisual>();
        visual.ResetForReuse();
        visual.SetPopupManager(handPopupManager);

        cards[id] = visual;
        created = true;
        return visual;
    }

    private static void MoveToSlot(CardVisual visual, Transform slot)
    {
        if (visual.transform.parent == slot) return;
        visual.transform.SetParent(slot, true);
        visual.MoveTo(slot.position);
    }

    // =========================================================
    // HP / Defense / Effects (CharacterManager와 같은 표시)
    // =========================================================

    private void UpdateHpBar(int health, int maxHealth)
    {
        if (hpText != null)
            hpText.text = $"{Mathf.Max(health, 0)}/{maxHealth}";
        if (hpBar == null || maxHealth <= 0) return;

        float ratio = Mathf.Max((float)health / maxHealth, 0f);
        float offset = shrinkRight ? (ratio - 1f) * 0.5f : (1f - ratio) * 0.5f;
        hpBar.localScale = new Vector3(ratio, 1f, 1f);
        hpBar.localPosition = new Vector3(offset, 0f, 0f);
    }

    private void UpdateDefense(int defense)
    {
        if (defenseText == null) return;
        defenseText.transform.parent.gameObject.SetActive(defense >= 1);
        defenseText.text = defense.ToString();
    }

    private void UpdateEffectList(EffectView[] effects)
    {
        if (effectList == null) return;

        var key = new StringBuilder();
        if (effects != null) foreach (EffectView e in effects) key.Append(e.type).Append(e.magnitude).Append(';');
        if (key.ToString() == shownEffects) return;
        shownEffects = key.ToString();

        foreach (EffectDisplay display in effectDisplays)
            Destroy(display.gameObject);
        effectDisplays.Clear();
        if (effects == null) return;

        float containerHeight = effectList.sizeDelta.y;
        float edge = (shrinkRight ? 1f : -1f) * effectList.sizeDelta.x / 2f;
        float direction = shrinkRight ? -1f : 1f;
        float cursor = edge;

        foreach (EffectView effect in effects)
        {
            if (!System.Enum.TryParse(effect.type, out EffectType type)) continue;

            EffectDisplay display = EffectDisplay.Spawn(effectList);
            if (display == null) return;
            effectDisplays.Add(display);
            display.SetEffect(new Effect(type, effect.magnitude), effect.magnitude.ToString());

            Vector2 spriteSize = display.GetSpriteSize();
            float nativeHeight = spriteSize.y > 0f ? spriteSize.y : containerHeight;
            float scale = nativeHeight > 0f ? containerHeight / nativeHeight : 1f;
            display.SetScale(scale);
            float displayWidth = (spriteSize.x > 0f ? spriteSize.x : containerHeight) * scale;
            float centerX = shrinkRight ? cursor - displayWidth / 2f : cursor + displayWidth / 2f;
            display.SetLocalPosition(new Vector3(centerX, 0f, 0f));
            cursor += direction * displayWidth;
        }
    }

    private void SetDefenseIndicatorActive(bool active)
    {
        if (active == shownGuard) return;
        shownGuard = active;

        if (defenseIndicator == null || defenseIndicatorRestPosition == null) return;
        int targetIndex = active ? 1 : 0;
        if (targetIndex >= defenseIndicatorRestPosition.Length || defenseIndicatorRestPosition[targetIndex] == null) return;
        if (!active && !defenseIndicator.activeSelf) return;

        if (defenseIndicatorCoroutine != null)
            StopCoroutine(defenseIndicatorCoroutine);
        if (active)
            defenseIndicator.SetActive(true);
        if (!isActiveAndEnabled) return;

        defenseIndicatorCoroutine = StartCoroutine(MoveDefenseIndicator(defenseIndicatorRestPosition[targetIndex].position, !active));
    }

    private IEnumerator MoveDefenseIndicator(Vector3 targetPosition, bool deactivateOnComplete)
    {
        Transform indicator = defenseIndicator.transform;
        Vector3 start = indicator.position;
        float elapsed = 0f;
        while (elapsed < defenseIndicatorMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / defenseIndicatorMoveDuration));
            indicator.position = Vector3.Lerp(start, targetPosition, t);
            yield return null;
        }

        indicator.position = targetPosition;
        if (deactivateOnComplete) defenseIndicator.SetActive(false);
        defenseIndicatorCoroutine = null;
    }

    private void ResetDefenseIndicator()
    {
        shownGuard = false;
        if (defenseIndicator == null || defenseIndicatorRestPosition == null
            || defenseIndicatorRestPosition.Length == 0 || defenseIndicatorRestPosition[0] == null)
            return;

        defenseIndicator.transform.position = defenseIndicatorRestPosition[0].position;
        defenseIndicator.SetActive(false);
    }
}
