using System;

// 카드 강화 보상 하나를 표현한다: 카드에 적용할 effect와, 그와 함께 부여되는 cost/cooldown 변화량(delta).
[Serializable]
public class CardUpgrade
{
    public CardEffect effect;
    public int costDelta;
    public int cooldownDelta;

    public CardUpgrade(CardEffect effect, int costDelta, int cooldownDelta)
    {
        this.effect = effect;
        this.costDelta = costDelta;
        this.cooldownDelta = cooldownDelta;
    }
}
