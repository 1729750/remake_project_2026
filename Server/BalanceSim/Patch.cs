using System.Text.Json;
using System.Text.Json.Serialization;

namespace BalanceSim;

/// <summary>
/// 패치 파일 (patches/*.json): 시뮬레이터에서만 덮어쓸 수치. Unity 에셋은 건드리지 않는다.
/// {
///   "name": "attack-big 너프",
///   "cards": { "attack-big": { "cost": 9, "cooldown": 8, "effects": { "0": { "magnitude": 35 } } } },
///   "effectPrices": { "TimeSkip": { "price": 2, "magnitudeMin": 4, "magnitudeMax": 8 } }
/// }
/// 적지 않은 값은 그대로다. effects의 키는 카드 효과 순서(0부터).
/// </summary>
public sealed class Patch
{
    public string Name { get; set; } = "patch";
    public Dictionary<string, CardChange> Cards { get; set; } = new();
    public Dictionary<string, PriceChange> EffectPrices { get; set; } = new();

    public sealed class CardChange
    {
        public int? Cost { get; set; }
        public int? Cooldown { get; set; }
        public Dictionary<string, EffectChange> Effects { get; set; } = new();
    }

    public sealed class EffectChange
    {
        public int? Magnitude { get; set; }
    }

    public sealed class PriceChange
    {
        public float? Price { get; set; }
        public int? MagnitudeMin { get; set; }
        public int? MagnitudeMax { get; set; }
        public int? MagnitudeUnit { get; set; }
    }

    public static Patch Load(string path) =>
        JsonSerializer.Deserialize<Patch>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() },
        }) ?? throw new InvalidDataException($"{path}를 읽을 수 없습니다.");

    public CardLibrary ApplyTo(CardLibrary library)
    {
        foreach (string name in Cards.Keys)
            library.Get(name); // 없는 카드 이름이면 여기서 KeyNotFoundException

        return library.WithChanges(
            card =>
            {
                if (!Cards.TryGetValue(card.Name, out CardChange? change)) return card;
                var effects = card.Effects.Select((e, i) =>
                    change.Effects.TryGetValue(i.ToString(), out EffectChange? ec) && ec.Magnitude is int m ? e with { Magnitude = m } : e).ToList();
                return card with
                {
                    Cost = change.Cost ?? card.Cost,
                    Cooldown = change.Cooldown ?? card.Cooldown,
                    Effects = effects,
                };
            },
            (type, price) =>
            {
                if (!EffectPrices.TryGetValue(type.ToString(), out PriceChange? change)) return price;
                return new EffectPrice(change.Price ?? price.Price, change.MagnitudeMin ?? price.MagnitudeMin,
                    change.MagnitudeMax ?? price.MagnitudeMax, change.MagnitudeUnit ?? price.MagnitudeUnit);
            });
    }
}
