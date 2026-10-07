using System;

[Serializable]
public struct PreparationOption
{
    public PreparationOptionType Type;

    // 카드 추가 옵션일 때 사용
    public CardDefinition Card;

    // 강화 옵션일 때 서버가 고른 실제 덱 index
    public int TargetCardIndex;

    // 이전 직렬화 데이터와의 호환용 식별자
    public int EnhanceId;

    // Single 모드와 같은 CardUpgrade 값
    public CardUpgrade Upgrade;

    public static PreparationOption CreateSkip()
    {
        return new PreparationOption
        {
            Type = PreparationOptionType.Skip,
            Card = null,
            TargetCardIndex = -1,
            EnhanceId = -1,
            Upgrade = null
        };
    }
}
