using System.Collections.Generic;
using UnityEngine;

public sealed class CardOptionGenerator : MonoBehaviour
{
    [SerializeField] private List<CardDefinition> cardPool;

    public PreparationOption[] GenerateOptions(PlayerPreparationData player, int round)
    {
        if (cardPool == null || cardPool.Count == 0 || cardPool.Exists(card => card == null || !card.TryGetValidatedEffects(out _)))
        {
            Debug.LogError("[CardOptionGenerator] Configure the complete reward card pool.");
            return null;
        }

        return new[]
        {
            round > 0 ? CreateEnhanceOption(player) : CreateAddCardOption(),
            CreateAddCardOption(),
            round > 0 && Random.value < 0.5f ? CreateEnhanceOption(player) : CreateAddCardOption()
        };
    }

    private PreparationOption CreateAddCardOption()
    {
        int index = Random.Range(0, cardPool.Count);
        return new PreparationOption
        {
            Type = PreparationOptionType.AddCard,
            Card = cardPool[index].Clone(),
            CardPoolIndex = index,
            TargetCardIndex = -1,
            EnhanceId = -1
        };
    }

    private PreparationOption CreateEnhanceOption(PlayerPreparationData player)
    {
        // Reuse Single's upgrade producer and eligibility rules.
        for (int attempt = 0; attempt < 64; attempt++)
        {
            CardUpgrade upgrade = RewardManager.RollEnhanceOption();
            var targets = new List<int>();
            for (int i = 0; i < player.FinalDeck.Count; i++)
                if (player.FinalDeck[i] != null && RewardManager.CanEnhance(player.FinalDeck[i], upgrade))
                    targets.Add(i);
            if (targets.Count == 0) continue;
            int target = targets[Random.Range(0, targets.Count)];
            return new PreparationOption
            {
                Type = PreparationOptionType.EnhanceCard,
                CardPoolIndex = -1,
                TargetCardIndex = target,
                EnhanceId = attempt,
                Upgrade = upgrade,
                Card = player.FinalDeck[target].Clone()
            };
        }
        return CreateAddCardOption();
    }

    public int GetCardPoolIndex(CardDefinition card) => cardPool == null ? -1 : cardPool.IndexOf(card);

    public CardDefinition GetCardByPoolIndex(int index) =>
        cardPool != null && index >= 0 && index < cardPool.Count ? cardPool[index] : null;

    [System.Serializable]
    private class CardState
    {
        public int source, cost, cooldown, type;
        public int[] effects, magnitudes, targets, categories;
    }

    public string SerializeCard(CardDefinition card, int source)
    {
        if (!card.TryGetValidatedEffects(out CardEffect[] effects))
            throw new System.InvalidOperationException("Invalid card effect definition.");
        var state = new CardState
        {
            source = source, cost = card.GetCost(), cooldown = card.GetCooldown(), type = (int)card.GetCardType(),
            effects = new int[effects.Length], magnitudes = new int[effects.Length],
            targets = new int[effects.Length], categories = new int[effects.Length]
        };
        for (int i = 0; i < effects.Length; i++)
        {
            state.effects[i] = (int)effects[i].GetEffect().GetEffectType();
            state.magnitudes[i] = effects[i].GetEffect().GetMagnitude();
            state.targets[i] = (int)effects[i].GetEffectTarget();
            state.categories[i] = (int)effects[i].GetAppliedCategory();
        }
        return JsonUtility.ToJson(state);
    }

    public CardDefinition DeserializeCard(string json)
    {
        CardState state = JsonUtility.FromJson<CardState>(json);
        if (state == null || state.effects == null || state.effects.Length > 64 ||
            state.magnitudes == null || state.targets == null || state.categories == null ||
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
            if (state.categories[i] == 0 || (effects[i].GetAppliedCategory() & ~effects[i].GetSupportedCategories()) != 0)
                throw new System.InvalidOperationException("Unsupported effect category.");
        }
        return CardDefinition.CreateRuntime(state.cooldown, state.cost, (CardType)state.type, source.GetSprite(), effects);
    }

    public PreparationOptionNetData ToNetData(PreparationOption option) => new PreparationOptionNetData
    {
        Type = (byte)option.Type,
        CardPoolIndex = option.CardPoolIndex,
        TargetCardIndex = option.TargetCardIndex,
        EnhanceId = option.EnhanceId
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
