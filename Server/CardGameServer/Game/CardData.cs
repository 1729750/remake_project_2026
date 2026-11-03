using System.Text.Json;
using System.Text.Json.Serialization;

namespace CardGameServer.Game;

// enum 순서/이름은 Unity 쪽 Assets/scripts/Cards 와 같다. (cards.json에는 이름 문자열로 들어간다)
public enum EffectType
{
    Attack, Defend, Weak, Vulnerable, Strength, Harden, Guard, PoseBreak, EnergyHeal, AddDump, Burning,
    CostToCooldown, CooldownToCost, DivideCooldown, TimeSkip, Quicker, Preserve, Disposable, DefenseToCooldown,
    EnergyDrain,
}

public enum EffectTarget { User, Opponent }

public enum CardType { Instant, Continuous, Mix }

[Flags]
public enum EffectCategory
{
    Instant = 1 << 0,
    Continuous = 1 << 1,
}

public sealed record CardEffectDef(EffectType Type, int Magnitude, EffectTarget Target, EffectCategory AppliedCategory);

public sealed record CardDef(string Name, int Cost, int Cooldown, CardType CardType, IReadOnlyList<CardEffectDef> Effects);

/// <summary>
/// Data/cards.json (Server/tools/export-cards.mjs가 Unity 에셋에서 만든다)
/// </summary>
public sealed class CardLibrary
{
    public int MaxHealth { get; }
    public IReadOnlyList<CardDef> StartDeck { get; }
    public IReadOnlyList<CardDef> Cards { get; }

    private readonly Dictionary<string, CardDef> byName;

    private CardLibrary(int maxHealth, IReadOnlyList<CardDef> startDeck, IReadOnlyList<CardDef> cards)
    {
        MaxHealth = maxHealth;
        StartDeck = startDeck;
        Cards = cards;
        byName = cards.ToDictionary(c => c.Name);
    }

    public CardDef Get(string name) => byName[name];

    public static CardLibrary Load(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        var file = JsonSerializer.Deserialize<CardsFile>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"{path}를 읽을 수 없습니다.");

        var cards = file.Cards.Select(c => new CardDef(c.Name, c.Cost, c.Cooldown, c.CardType,
            c.Effects.Select(e => new CardEffectDef(e.Type, e.Magnitude, e.Target, (EffectCategory)e.AppliedCategory)).ToList()))
            .ToList();

        var library = new CardLibrary(file.MaxHealth, Array.Empty<CardDef>(), cards);
        return new CardLibrary(file.MaxHealth, file.StartDeck.Select(library.Get).ToList(), cards);
    }

    private sealed record CardsFile(int MaxHealth, List<string> StartDeck, List<CardFile> Cards);
    private sealed record CardFile(string Name, int Cost, int Cooldown, CardType CardType, List<EffectFile> Effects);
    private sealed record EffectFile(EffectType Type, int Magnitude, EffectTarget Target, int AppliedCategory);
}
