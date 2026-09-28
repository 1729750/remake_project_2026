using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DivideCooldown_Multi : Effect_Multi
{
    public DivideCooldown_Multi(int magnitude)
        : base(
            EffectType.DivideCooldown,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Positive;

    public override void OnUse(
        CharacterManager_Multi subject,
        CardInstance_Multi self,
        bool actualUse)
    {
        if (!actualUse ||
            subject == null ||
            self == null)
        {
            return;
        }

        List<CardInstance_Multi> cards =
            new List<CardInstance_Multi>
            {
                self
            };

        foreach (
            CardInstance_Multi queued
            in subject.GetQueue())
        {
            if (queued != null)
                cards.Add(queued);
        }

        int sum = 0;

        foreach (
            CardInstance_Multi card
            in cards)
        {
            sum +=
                card.GetCooldownLeft();
        }

        int averaged =
            Mathf.RoundToInt(
                (float)sum /
                cards.Count);

        foreach (
            CardInstance_Multi card
            in cards)
        {
            card.SetCooldownLeft(
                averaged);
        }
    }
}
