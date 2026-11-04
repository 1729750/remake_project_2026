using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서버가 보내는 카드(이름 + 수치)를 싱글과 같은 Card 프리팹(CardVisual)에 그린다.
/// 서버 카드 이름 = CardDefinition 에셋 이름.
/// </summary>
public sealed class OracleCardLibrary
{
    private readonly Dictionary<string, CardDefinition> byName = new Dictionary<string, CardDefinition>();
    private readonly Dictionary<int, CardDefinition> dumpCards = new Dictionary<int, CardDefinition>();

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
    /// 앞면 카드 하나를 서버 값으로 채운다. (CardInstance.RefreshDisplay가 하던 일)
    /// </summary>
    public void Apply(CardVisual visual, CardView card, bool selected, bool unplayable)
    {
        CardDefinition definition = Find(card);
        if (definition != null) visual.SetCardDefinition(definition);

        visual.SetCostText(card.cost.ToString());
        visual.SetCooldownText(card.cooldownLeft.ToString());
        visual.RefreshEffectDisplays(ToCardEffects(card.effects));
        visual.SetUnplayable(unplayable);
        visual.SetSelected(selected);
    }

    private static List<CardEffect> ToCardEffects(CardEffectView[] effects)
    {
        var result = new List<CardEffect>();
        if (effects == null) return result;

        foreach (CardEffectView view in effects)
        {
            if (!Enum.TryParse(view.type, out EffectType type)) continue;
            EffectTarget target = view.target == "User" ? EffectTarget.User : EffectTarget.Opponent;
            result.Add(new CardEffect(new Effect(type, view.magnitude), target));
        }
        return result;
    }
}
