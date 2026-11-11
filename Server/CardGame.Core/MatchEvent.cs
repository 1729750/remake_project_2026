namespace CardGame.Core;

/// <summary>
/// 전투 중 일어난 일 하나. 게임 서버는 클라이언트 효과음에, BalanceSim은 지표 계산에 쓴다.
/// Kind: use(카드 사용 시도, Ok=성공) | play(큐에서 발동, Ok=공격 카드) | damage(피격, Guarded=가드 중)
///       | shield(방어도로 막음) | effect(효과 획득, Effect=종류) | turnEnd
/// </summary>
public sealed record MatchEvent(string Kind, string PlayerId, string? Card, int Amount, string? Effect, bool Guarded, bool Ok);
