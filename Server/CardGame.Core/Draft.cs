namespace CardGame.Core;

/// <summary>카드 선택 단계의 선택지. Kind = add(덱에 추가) | enhance(Target번 덱 카드를 Card로 바꿈 = 강화 후 모습)</summary>
public sealed record PickOption(string Kind, CardDef Card, int Target);

/// <summary>
/// 전투 전 카드 선택 단계의 규칙. 팀 멀티 기획(CardOptionGenerator)과 같은 구성:
/// 라운드마다 [카드 추가, 강화, 무작위(강화 또는 추가)] 중 하나를 고르거나 건너뛴다.
/// </summary>
public static class Draft
{
    public const int Rounds = 10;

    /// <summary>강화할 카드가 없으면 그 칸은 카드 추가로 바뀐다.</summary>
    public static PickOption[] RollOptions(IReadOnlyList<CardDef> deck, CardLibrary library, Random random)
    {
        PickOption AddCard() => new("add", library.Cards[random.Next(library.Cards.Count)], -1);

        PickOption EnhanceOrAdd()
        {
            var rolled = Enhance.Roll(deck, library, random);
            if (rolled == null) return AddCard();
            var (target, upgrade) = rolled.Value;
            return new PickOption("enhance", Enhance.Apply(deck[target], upgrade), target);
        }

        return [AddCard(), EnhanceOrAdd(), random.Next(2) == 0 ? EnhanceOrAdd() : AddCard()];
    }

    public static void Apply(List<CardDef> deck, PickOption option)
    {
        if (option.Kind == "enhance" && option.Target >= 0 && option.Target < deck.Count)
            deck[option.Target] = option.Card; // 같은 카드 자리에 강화된 카드
        else
            deck.Add(option.Card);
    }
}
