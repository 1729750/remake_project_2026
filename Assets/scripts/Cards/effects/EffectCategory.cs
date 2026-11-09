using System;

// Instant와 Continuous는 플래그상 서로 배타적이지 않지만, CardDefinition은 한 카드의 모든 효과가
// 같은 category를 쓰도록 강제한다(Mix 카드 자체는 더 이상 없음) — 그래서 실제로는 카드 하나가
// 즉발 파이프라인과 지속 파이프라인을 동시에 타는 일은 없다.
[Flags]
public enum EffectCategory
{
    Instant = 1 << 0,
    Continuous = 1 << 1,
}
