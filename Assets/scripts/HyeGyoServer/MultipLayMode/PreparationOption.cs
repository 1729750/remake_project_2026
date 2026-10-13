using System;

[Serializable]
public struct PreparationOption
{
    public PreparationOptionType Type;

    // 카드 추가 옵션일 때 사용
    public CardDefinition Card;

    // 강화 옵션일 때 대상 카드 index
    public int TargetCardIndex;

    // 강화 종류 식별용
    public int EnhanceId;
}