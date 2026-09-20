using System;

[Serializable]
public class DefendEffect : Effect
{
    // 방어도는 항상 마지막으로 소모돼야 한다(Guard처럼 공격 magnitude를 배율로 줄이는 효과가 먼저
    // 반영된 "실제" 값을 상쇄해야 하므로) — 그래서 존재 가능한 Priority 중 가장 낮은 -1을 쓴다.
    // CharacterManager.ConsumeQueuedDefense가 OnAppliedOther 정렬 순회 이후에 호출되는 것으로
    // 이 우선순위를 실제 구현한다(DefendEffect는 _effects 목록에 들어가지 않으므로 정렬 대상에
    // 직접 끼워 넣을 수는 없다).
    private const int Priority = -1;

    public DefendEffect(int magnitude) : base(EffectType.Defend, magnitude, Priority) { }

    public override EffectCategory SupportedCategories => EffectCategory.Continuous;

    // 방어도는 CharacterManager._effects에 별도 상태를 쌓지 않는다 — 카드가 큐에 머무는 동안 남은
    // magnitude 자체가 방어도이므로(CardInstance._queuedMagnitudes에 카드별로 관리된다), 여기서는
    // 사운드만 재생한다. 실제 소모(공격 데미지 상쇄)는 CharacterManager.ApplyEffect가 Attack을 받을
    // 때 자신의 큐를 순회하며 카드별로 직접 처리한다 — 그래야 한 카드가 다 소모된 뒤 새 카드로 얻은
    // 방어도를, 먼저 큐를 떠나는 카드가 자기 몫인 줄 알고 앗아가는 일이 없다.
    public override void OnEnterQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect)
    {
        PlayApplySound();
    }

    public override void OnExitQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect, int grantedMagnitude) { }
}
