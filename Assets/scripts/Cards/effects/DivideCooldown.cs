using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class DivideCooldown : Effect
{
    public DivideCooldown(int magnitude) : base(EffectType.DivideCooldown, magnitude) { }

    public override void OnExpired(CharacterManager subject) { }
    public override void OnTurnStarted(CharacterManager subject) { }
    public override void OnTurnEnded(CharacterManager subject) { }

    // self는 아직 손패에서 큐로 넘어가기 전(ResolveUse 시점)이라 subject.GetQueue()에 포함되지
    // 않으므로 따로 합산 대상에 넣는다. self와 현재 큐의 카드들의 cooldownLeft 합을 개수로 나눠
    // 반올림한 값으로 전부(self 포함) 맞춘다.
    public override void OnUse(CharacterManager subject, CardInstance self, bool actualUse)
    {
        if (!actualUse || subject == null || self == null) return;

        List<CardInstance> cards = new List<CardInstance> { self };
        foreach (CardInstance queued in subject.GetQueue())
        {
            if (queued != null) cards.Add(queued);
        }

        int sum = 0;
        foreach (CardInstance card in cards)
            sum += card.GetCooldownLeft();

        int averaged = Mathf.RoundToInt((float)sum / cards.Count);
        foreach (CardInstance card in cards)
            card.SetCooldownLeft(averaged);
    }

    public override void OnUsingOther(CharacterManager subject, CardInstance cardInstance, bool actualUse) { }
    public override void OnTick(CharacterManager subject) { }
    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
