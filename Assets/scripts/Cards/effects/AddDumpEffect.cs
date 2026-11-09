using System;
using System.Collections.Generic;

// 큐에 머무는 동안 대상의 덱에 아무 효과도 없는 쓰레기 카드(코스트 1, 쿨다운 magnitude)를 한 장 섞어 둔다.
// 카드가 큐를 떠날 때 그 쓰레기가 아직 덱에 남아 있으면 회수한다 — 이미 뽑혔다면 손패에 남아 계속
// 자리를 차지한다(그게 이 효과의 방해다).
[Serializable]
public class AddDumpEffect : Effect
{
    // OnExitQueue는 OnEnterQueue를 받은 인스턴스가 아니라 새로 만든 인스턴스에서 불리므로(CardInstance.ExitQueue),
    // 어떤 쓰레기를 회수할지는 큐에 들어간 카드(CardInstance) 기준으로 따로 기억한다.
    private static readonly Dictionary<CardInstance, List<(CharacterManager owner, CardInstance junk)>> Injected =
        new Dictionary<CardInstance, List<(CharacterManager, CardInstance)>>();

    public AddDumpEffect(int magnitude) : base(EffectType.AddDump, magnitude) { }

    // 전투가 끝날 때 큐는 ExitQueue 없이 그냥 비워지므로(CharacterManager.ClearHandAndQueue) 새 전투 시작 시 비운다.
    public static void ResetTracking() => Injected.Clear();

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override void OnEnterQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect)
    {
        CharacterManager target = cardEffect.GetTarget(subject);
        CardDefinition dumpDefinition = CardDefinition.Create(Math.Max(_magnitude, 1), 1, Array.Empty<CardEffect>(), null);
        var junk = new CardInstance(dumpDefinition, target);
        target.InsertIntoDeckRandomly(junk);

        if (!Injected.TryGetValue(self, out var list))
            Injected[self] = list = new List<(CharacterManager, CardInstance)>();
        list.Add((target, junk));
        PlayApplySound();
    }

    public override void OnExitQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect, int grantedMagnitude)
    {
        if (!Injected.TryGetValue(self, out var list)) return;
        foreach (var (owner, junk) in list)
            owner.RemoveFromDeck(junk);
        Injected.Remove(self);
    }
}
