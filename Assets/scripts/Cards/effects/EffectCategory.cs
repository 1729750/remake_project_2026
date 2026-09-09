using System;

// Instant와 Continuous는 서로 배타적이지 않다 — 한 카드 위의 한 CardEffect가 두 비트를 모두 켜면
// (Mix) 카드가 다 될 때의 즉발 파이프라인과 큐에 머무는 동안의 지속 파이프라인을 모두 탄다.
[Flags]
public enum EffectCategory
{
    Instant = 1 << 0,
    Continuous = 1 << 1,
}
