using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서버가 보내는 카드(이름 + 수치)를 싱글과 같은 Card 프리팹(CardVisual)에 그린다.
/// 서버 카드 이름 = CardDefinition 에셋 이름. 그림은 에셋 것을 쓰고, 코스트/쿨다운/효과는 서버 값으로
/// 만든 런타임 정의를 쓴다. 그래서 강화된 카드도 그림은 그대로, 기능(효과 설명 팝업, 카드 종류 배경 포함)만 바뀐다.
/// </summary>
public sealed class OracleCardLibrary
{
    private readonly Dictionary<string, CardDefinition> byName = new Dictionary<string, CardDefinition>();
    private readonly Dictionary<int, CardDefinition> dumpCards = new Dictionary<int, CardDefinition>();
    private readonly Dictionary<string, CardDefinition> runtimeCards = new Dictionary<string, CardDefinition>();

    public OracleCardLibrary(IEnumerable<CardDefinition> cards)
    {
        if (cards == null) return;
        foreach (CardDefinition card in cards)
        {
            if (card != null) byName[card.name] = card;
        }
    }

    public CardDefinition Find(CardView card)
    {
        if (card == null || string.IsNullOrEmpty(card.name)) return null;
        if (byName.TryGetValue(card.name, out CardDefinition definition)) return definition;

        // AddDump가 만드는 쓰레기 카드처럼 에셋이 없는 카드는 싱글과 똑같이 빈 정의를 만들어 쓴다
        if (!dumpCards.TryGetValue(card.cooldown, out definition))
        {
            definition = CardDefinition.Create(card.cooldown, card.cost, Array.Empty<CardEffect>(), null);
            definition.name = card.name;
            dumpCards[card.cooldown] = definition;
        }
        return definition;
    }

    /// <summary>
    /// 서버 카드 그대로의 정의: 에셋 그림 + 서버의 코스트/쿨다운/카드 종류/효과.
    /// 같은 내용이면 하나를 재사용한다.
    /// </summary>
    public CardDefinition DefinitionFor(CardView card)
    {
        CardDefinition asset = Find(card);
        if (asset == null || card.effects == null) return asset;

        string key = card.name + "|" + card.cost + "|" + card.cooldown + "|" + card.cardType;
        foreach (CardEffectView e in card.effects) key += "|" + e.type + e.magnitude + e.target + e.category;
        if (runtimeCards.TryGetValue(key, out CardDefinition cached)) return cached;

        if (!Enum.TryParse(card.cardType, out CardType cardType)) cardType = asset.GetCardType();
        CardDefinition runtime = CardDefinition.CreateRuntime(card.cooldown, card.cost, cardType, asset.GetSprite(),
            ToCardEffects(card.effects, withCategory: true).ToArray());
        runtime.name = card.level > 0 ? $"{card.name}+{card.level}" : card.name;
        runtimeCards[key] = runtime;
        return runtime;
    }

    /// <summary>
    /// 앞면 카드 하나를 서버 값으로 채운다. (CardInstance.RefreshDisplay가 하던 일)
    /// </summary>
    public void Apply(CardVisual visual, CardView card, bool selected, bool unplayable)
    {
        CardDefinition definition = DefinitionFor(card);
        if (definition != null) visual.SetCardDefinition(definition);

        visual.SetCostText(card.cost.ToString());
        visual.SetCooldownText(card.cooldownLeft.ToString());
        visual.RefreshEffectDisplays(ToCardEffects(card.effects, withCategory: false));
        visual.SetUnplayable(unplayable);
        visual.SetSelected(selected);
    }

    // =========================================================
    // Deck (DeckDisplay 프리팹)
    // =========================================================

    private static Vector2 nativeCardSize;

    /// <summary>Card 프리팹 한 장의 기본 크기 (월드 단위, 스케일 1).</summary>
    public static Vector2 NativeCardSize()
    {
        if (nativeCardSize != Vector2.zero) return nativeCardSize;

        GameObject prefab = Resources.Load<GameObject>("Prefabs/Card");
        if (prefab == null) return Vector2.one;

        GameObject probe = UnityEngine.Object.Instantiate(prefab, new Vector3(0f, -1000f, 0f), Quaternion.identity);
        nativeCardSize = probe.GetComponent<CardVisual>().GetBackgroundSize();
        UnityEngine.Object.Destroy(probe);
        return nativeCardSize;
    }

    /// <summary>
    /// 덱 카드 목록을 DeckDisplay에 띄운다 (강화된 카드는 강화된 수치로). 처음이면 Resources/Prefabs/DeckDisplay를 만든다.
    /// center/size(월드 단위) 안에 다 들어가도록 카드가 많을수록 칸을 늘려 작게 놓는다.
    /// </summary>
    public DeckDisplay ShowDeck(DeckDisplay display, Transform parent, Vector3 center, Vector2 size, CardView[] deck)
    {
        if (display == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/DeckDisplay");
            if (prefab == null) return null;
            display = UnityEngine.Object.Instantiate(prefab, parent).GetComponent<DeckDisplay>();
        }

        display.transform.position = center;
        display.SetSize(size);

        var cards = new List<CardDefinition>();
        if (deck != null)
        {
            foreach (CardView card in deck)
            {
                CardDefinition definition = DefinitionFor(card);
                if (definition != null) cards.Add(definition);
            }
        }

        (int columns, float cardSize) = FitGrid(cards.Count, size);
        display.SetDeck(cards, null, cardSize, columns);
        return display;
    }

    // 몇 줄로 놓아야 카드가 가장 크게 보이는지
    private static (int columns, float cardSize) FitGrid(int count, Vector2 area)
    {
        Vector2 card = NativeCardSize();
        if (count <= 0 || card.x <= 0f || card.y <= 0f) return (1, 1f);

        int bestColumns = count;
        float bestSize = 0f;
        for (int rows = 1; rows <= count; rows++)
        {
            int columns = Mathf.CeilToInt((float)count / rows);
            float size = Mathf.Min(area.x / columns * 0.9f / card.x, area.y / rows * 0.9f / card.y);
            if (size > bestSize)
            {
                bestSize = size;
                bestColumns = columns;
            }
        }
        return (Mathf.Max(bestColumns, 2), bestSize);
    }

    private static List<CardEffect> ToCardEffects(CardEffectView[] effects, bool withCategory)
    {
        var result = new List<CardEffect>();
        if (effects == null) return result;

        foreach (CardEffectView view in effects)
        {
            if (!Enum.TryParse(view.type, out EffectType type)) continue;
            EffectTarget target = view.target == "User" ? EffectTarget.User : EffectTarget.Opponent;
            var cardEffect = new CardEffect(new Effect(type, view.magnitude), target);
            if (withCategory) cardEffect.SetAppliedCategory(CategoryOf(view.category));
            result.Add(cardEffect);
        }
        return result;
    }

    private static EffectCategory CategoryOf(string category) =>
        category == "Continuous" ? EffectCategory.Continuous
        : category == "Mix" ? EffectCategory.Instant | EffectCategory.Continuous
        : EffectCategory.Instant;
}
