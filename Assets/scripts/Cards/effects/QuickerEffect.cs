using System;
using System.Linq;

// 큐에 머무는 동안 자신의 큐(Continuous 카드 제외) 쿨다운이 턴 종료마다 1 대신 2씩 줄어든다.
// 여러 장이 겹쳐도 속도는 2배로 고정이다(풀이 하나로 합쳐지고, 처음 생길 때만 속도를 올린다).
[Serializable]
public class QuickerEffect : Effect
{
    public QuickerEffect(int magnitude) : base(EffectType.Quicker, magnitude) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    public override void OnEnterQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect)
    {
        CharacterManager target = cardEffect.GetTarget(subject);
        bool alreadyActive = target.GetEffects().Any(e => e is QuickerEffect && e.IsInfinite);
        base.OnEnterQueue(subject, self, cardEffect);
        if (!alreadyActive)
            target.ChangeTickSpeed(1);
    }

    // 풀이 실제로 제거될 때(기여하던 카드가 모두 큐를 떠났을 때) 한 번만 불린다.
    public override void OnExpired(CharacterManager subject)
    {
        subject.ChangeTickSpeed(-1);
    }
}
