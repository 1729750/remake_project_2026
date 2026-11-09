using System;
using UnityEngine;

// 화상(스킬, Continuous): 이 카드가 큐에 머무는 동안 대상은 매 턴 종료마다 magnitude의 1/5(올림)만큼
// 방어를 무시하는 피해를 받는다. 카드가 큐를 떠나면 꺼진다 — "방어를 무시하는 대신 일시적인" 지속딜.
// 공격 계열이 아니므로 Strength/Vulnerable 같은 공격 보정은 받지 않는다.
[Serializable]
public class BurningEffect : Effect
{
    public BurningEffect(int magnitude) : base(EffectType.Burning, magnitude) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public static int DamagePerTurn(int magnitude) => magnitude > 0 ? Mathf.CeilToInt(magnitude / 5f) : 0;

    public override void OnTurnEnded(CharacterManager subject)
    {
        subject.TakeDamage(DamagePerTurn(_magnitude));
    }
}
