using System.Collections.Generic;
using UnityEngine;

public sealed class CardOptionGenerator : MonoBehaviour
{
    [SerializeField] private List<CardDefinition> cardPool;

    public bool ValidateConfiguration(out string reason)
    {
        reason = "Configure the complete 29-card reward pool and effect prices.";
        if (cardPool == null || cardPool.Count != 29 || cardPool.Exists(card => card == null ||
            !card.TryGetValidatedEffects(out _) || card.GetCost() < 0 || card.GetCooldown() < 1 ||
            !System.Enum.IsDefined(typeof(CardType), card.GetCardType()))) return false;
        foreach (EffectType type in RewardManager.EnhanceableEffectTypes)
        {
            EffectPriceInfo info = GameManager.GetEffectPriceInfo(type);
            if (info.magnitudeUnit < 1 || info.magnitudeMax < info.magnitudeMin ||
                float.IsNaN(info.price) || float.IsInfinity(info.price)) return false;
        }
        reason = string.Empty;
        return true;
    }

    public PreparationOption[] GenerateOptions(PlayerPreparationData player, int round)
    {
        if (!ValidateConfiguration(out string reason)) throw new System.InvalidOperationException(reason);
        var options = new PreparationOption[3];
        try
        {
            options[0] = CreateAddCardOption();
            options[1] = CreateEnhanceOption(player);
            bool enhance = Random.Range(0, 2) == 1;
            options[2] = enhance ? CreateEnhanceOption(player) : CreateAddCardOption();
            options[2].RandomEnhance = enhance;
            for (int i = 0; i < options.Length; i++) options[i].OfferedSlot = i;
            return options;
        }
        catch
        {
            DisposeOptions(options);
            throw;
        }
    }

    private PreparationOption CreateAddCardOption()
    {
        int index = Random.Range(0, cardPool.Count);
        return new PreparationOption
        {
            Type = PreparationOptionType.AddCard,
            Card = CopyCard(cardPool[index]),
            CardPoolIndex = index,
            TargetCardIndex = -1,
            EnhanceId = -1
        };
    }

    private PreparationOption CreateEnhanceOption(PlayerPreparationData player)
    {
        var targets = new List<int>();
        var descriptors = new List<List<CardUpgrade>>();
        for (int i = 0; i < player.FinalDeck.Count; i++)
        {
            var feasible = new List<CardUpgrade>();
            foreach (EffectType type in RewardManager.EnhanceableEffectTypes)
            {
                Effect effect = Effect.Create(type, 0);
                foreach (EffectCategory category in new[] { EffectCategory.Instant, EffectCategory.Continuous })
                {
                    if ((effect.SupportedCategories & category) == 0) continue;
                    var entry = new CardEffect(Effect.Create(type, 0), EffectTarget.User);
                    entry.SetAppliedCategory(category);
                    var descriptor = new CardUpgrade(entry, 0, 0);
                    if (RewardManager.CanEnhance(player.FinalDeck[i], descriptor)) feasible.Add(descriptor);
                }
            }
            if (feasible.Count == 0) continue;
            targets.Add(i);
            descriptors.Add(feasible);
        }
        if (targets.Count == 0)
        {
            PreparationOption fallback = CreateAddCardOption();
            fallback.NoEnhanceTarget = true;
            return fallback;
        }
        int selected = Random.Range(0, targets.Count);
        int target = targets[selected];
        List<CardUpgrade> choices = descriptors[selected];
        CardUpgrade prototype = choices[Random.Range(0, choices.Count)];
        CardUpgrade upgrade = RollUpgrade(prototype.effect.GetEffect().GetEffectType(), prototype.effect.GetAppliedCategory());
        if (!RewardManager.CanEnhance(player.FinalDeck[target], upgrade))
            throw new System.InvalidOperationException("Generated upgrade is incompatible with its owner target.");
        CardDefinition before = CopyCard(player.FinalDeck[target]);
        CardDefinition after = CopyCard(before);
        after.ApplyUpgrade(CopyUpgrade(upgrade));
        return new PreparationOption
        {
            Type = PreparationOptionType.EnhanceCard,
            CardPoolIndex = player.FinalDeckSourceIndices[target],
            TargetCardIndex = target,
            EnhanceId = selected,
            Upgrade = upgrade,
            Card = before,
            AfterPreview = after
        };
    }

    // Multi chooses a feasible category first; magnitude/target/budget follow RewardManager's rules.
    private static CardUpgrade RollUpgrade(EffectType type, EffectCategory category)
    {
        EffectPriceInfo info = GameManager.GetEffectPriceInfo(type);
        int magnitude = info.magnitudeMin + info.magnitudeUnit * Random.Range(0,
            (info.magnitudeMax - info.magnitudeMin) / info.magnitudeUnit + 1);
        Effect effect = Effect.Create(type, magnitude);
        EffectTarget target;
        switch (effect.TargetPolarity)
        {
            case EffectTargetPolarity.Positive: target = magnitude > 0 ? EffectTarget.User : EffectTarget.Opponent; break;
            case EffectTargetPolarity.Negative: target = magnitude > 0 ? EffectTarget.Opponent : EffectTarget.User; break;
            default: target = Random.Range(0, 2) == 0 ? EffectTarget.User : EffectTarget.Opponent; break;
        }
        var cardEffect = new CardEffect(effect, target);
        cardEffect.SetAppliedCategory(category);
        int budget = Mathf.FloorToInt(info.price * (effect.DoesntUseMagnitude ? 1 : magnitude));
        if (category == EffectCategory.Continuous) budget *= 5;
        int sign = budget < 0 ? -1 : 1;
        int cost = 0, cooldown = 0;
        for (int i = 0; i < Mathf.Abs(budget); i++)
            if (Random.value < 2f / 3f) cooldown += sign;
            else cost += sign;
        return new CardUpgrade(cardEffect, cost, cooldown);
    }

    public static CardUpgrade CopyUpgrade(CardUpgrade upgrade)
    {
        var effect = new CardEffect(Effect.Create(upgrade.effect.GetEffect().GetEffectType(),
            upgrade.effect.GetEffect().GetMagnitude()), upgrade.effect.GetEffectTarget());
        effect.SetAppliedCategory(upgrade.effect.GetAppliedCategory());
        return new CardUpgrade(effect, upgrade.costDelta, upgrade.cooldownDelta);
    }

    public static CardDefinition CopyCard(CardDefinition source)
    {
        if (source == null || !source.TryGetValidatedEffects(out CardEffect[] original))
            throw new System.InvalidOperationException("Invalid runtime card.");
        var effects = new CardEffect[original.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i] = new CardEffect(Effect.Create(original[i].GetEffect().GetEffectType(),
                original[i].GetEffect().GetMagnitude()), original[i].GetEffectTarget());
            effects[i].SetAppliedCategory(original[i].GetAppliedCategory());
        }
        return CardDefinition.CreateRuntime(source.GetCooldown(), source.GetCost(), source.GetCardType(), source.GetSprite(), effects);
    }

    public static void DisposeOptions(PreparationOption[] options)
    {
        if (options == null) return;
        foreach (PreparationOption option in options)
        {
            if (option.Card != null) Destroy(option.Card);
            if (option.AfterPreview != null) Destroy(option.AfterPreview);
        }
    }

    public int GetCardPoolIndex(CardDefinition card) => cardPool == null ? -1 : cardPool.IndexOf(card);

    public CardDefinition GetCardByPoolIndex(int index) =>
        cardPool != null && index >= 0 && index < cardPool.Count ? cardPool[index] : null;

    [System.Serializable]
    private class CardState
    {
        public int source, cost, cooldown, type;
        public int[] effects, magnitudes, targets, categories, supported;
    }

    public string SerializeCard(CardDefinition card, int source)
    {
        if (card == null || !card.TryGetValidatedEffects(out CardEffect[] effects))
            throw new System.InvalidOperationException("Invalid card effect definition.");
        var state = new CardState
        {
            source = source, cost = card.GetCost(), cooldown = card.GetCooldown(), type = (int)card.GetCardType(),
            effects = new int[effects.Length], magnitudes = new int[effects.Length],
            targets = new int[effects.Length], categories = new int[effects.Length], supported = new int[effects.Length]
        };
        for (int i = 0; i < effects.Length; i++)
        {
            state.effects[i] = (int)effects[i].GetEffect().GetEffectType();
            state.magnitudes[i] = effects[i].GetEffect().GetMagnitude();
            state.targets[i] = (int)effects[i].GetEffectTarget();
            state.categories[i] = (int)effects[i].GetAppliedCategory();
            state.supported[i] = (int)effects[i].GetSupportedCategories();
        }
        return JsonUtility.ToJson(state);
    }

    public CardDefinition DeserializeCard(string json)
    {
        CardState state = JsonUtility.FromJson<CardState>(json);
        if (state == null || state.effects == null || state.effects.Length > 64 ||
            state.magnitudes == null || state.targets == null || state.categories == null || state.supported == null ||
            state.effects.Length != state.supported.Length ||
            state.effects.Length != state.magnitudes.Length || state.effects.Length != state.targets.Length ||
            state.effects.Length != state.categories.Length || state.cost < 0 || state.cooldown < 1 ||
            !System.Enum.IsDefined(typeof(CardType), (CardType)state.type))
            throw new System.InvalidOperationException("Invalid runtime card state.");
        CardDefinition source = GetCardByPoolIndex(state.source);
        if (source == null) throw new System.InvalidOperationException("Invalid card source.");
        var effects = new CardEffect[state.effects.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            if (!System.Enum.IsDefined(typeof(EffectType), (EffectType)state.effects[i]) ||
                !System.Enum.IsDefined(typeof(EffectTarget), (EffectTarget)state.targets[i]))
                throw new System.InvalidOperationException("Invalid effect state.");
            effects[i] = new CardEffect(Effect.Create((EffectType)state.effects[i], state.magnitudes[i]), (EffectTarget)state.targets[i]);
            effects[i].SetAppliedCategory((EffectCategory)state.categories[i]);
            if (state.supported[i] != (int)effects[i].GetSupportedCategories() ||
                effects[i].GetEffect().GetEffectType() != (EffectType)state.effects[i] ||
                effects[i].GetEffect().GetMagnitude() != state.magnitudes[i] ||
                state.categories[i] == 0 || (effects[i].GetAppliedCategory() & ~effects[i].GetSupportedCategories()) != 0)
                throw new System.InvalidOperationException("Unsupported effect category.");
        }
        return CardDefinition.CreateRuntime(state.cooldown, state.cost, (CardType)state.type, source.GetSprite(), effects);
    }

    public PreparationOptionNetData ToNetData(PreparationOption option) => new PreparationOptionNetData
    {
        Type = (byte)option.Type,
        CardPoolIndex = option.CardPoolIndex,
        TargetCardIndex = option.TargetCardIndex,
        EnhanceId = option.EnhanceId,
        OfferedSlot = option.OfferedSlot,
        RandomEnhance = option.RandomEnhance,
        NoEnhanceTarget = option.NoEnhanceTarget,
        EffectType = option.Upgrade == null ? -1 : (int)option.Upgrade.effect.GetEffect().GetEffectType(),
        Magnitude = option.Upgrade == null ? 0 : option.Upgrade.effect.GetEffect().GetMagnitude(),
        Target = option.Upgrade == null ? 0 : (int)option.Upgrade.effect.GetEffectTarget(),
        Category = option.Upgrade == null ? 0 : (int)option.Upgrade.effect.GetAppliedCategory(),
        CostDelta = option.Upgrade == null ? 0 : option.Upgrade.costDelta,
        CooldownDelta = option.Upgrade == null ? 0 : option.Upgrade.cooldownDelta
    };

    public PreparationOption FromNetData(PreparationOptionNetData data) => new PreparationOption
    {
        Type = (PreparationOptionType)data.Type,
        CardPoolIndex = data.CardPoolIndex,
        Card = data.Type == (byte)PreparationOptionType.AddCard
            ? GetCardByPoolIndex(data.CardPoolIndex)?.Clone() : null,
        TargetCardIndex = data.TargetCardIndex,
        EnhanceId = data.EnhanceId
    };
}
