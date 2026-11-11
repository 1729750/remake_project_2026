using System;

/// <summary>
/// 오라클 서버(Server/CardGameServer)와 주고받는 JSON 메시지.
/// 필드 이름은 서버 JSON과 같아야 하므로 camelCase를 그대로 쓴다. (JsonUtility는 필드 이름 = JSON 키)
/// 메시지 종류 목록은 Server/CardGameServer/Protocol.cs, 상태 구조는 Server/CardGameServer/Game/Views.cs 참고.
/// </summary>
[Serializable]
public sealed class ClientMessage
{
    public string type;
    public string name;
    public string code;
    public int index;
    public string message;

    public static ClientMessage Hello(string name) => new ClientMessage { type = "hello", name = name };
    public static ClientMessage CreateRoom() => new ClientMessage { type = "create_room" };
    public static ClientMessage JoinRoom(string code) => new ClientMessage { type = "join_room", code = code };
    public static ClientMessage LeaveRoom() => new ClientMessage { type = "leave_room" };
    public static ClientMessage Ping() => new ClientMessage { type = "ping" };

    /// <summary>카드 선택 단계: 제시된 3장 중 index번.</summary>
    public static ClientMessage Pick(int index) => new ClientMessage { type = "pick", index = index };

    /// <summary>카드 선택 단계: 이번 라운드는 받지 않는다.</summary>
    public static ClientMessage Skip() => new ClientMessage { type = "skip" };

    /// <summary>전투: 0~3 손패 카드, -2 리드로우, -1 가드 토글.</summary>
    public static ClientMessage Play(int index) => new ClientMessage { type = "play", index = index };

    public static ClientMessage Rematch() => new ClientMessage { type = "rematch" };

    /// <summary>지난번 네트워크 오류 내용을 서버 로그에 남긴다 (진단용, 서버는 기록만 한다).</summary>
    public static ClientMessage ClientError(string report) => new ClientMessage { type = "client_error", message = report };
}

[Serializable]
public sealed class ServerMessage
{
    public string type;
    public string playerId;
    public string code;
    public string message;
    public PlayerInfo player;
    public PlayerInfo[] players;
    public MatchStateView state;

    /// <summary>이 메시지를 소켓에서 받은 시각 (OracleServerClient.Now 기준). JSON에는 없다.</summary>
    [NonSerialized] public double receivedAt;
}

[Serializable]
public sealed class PlayerInfo
{
    public string playerId;
    public string name;
}

[Serializable]
public sealed class MatchStateView
{
    public string phase;            // picking | countdown | battle | finished
    public string opponentName;
    public float timeLeft;

    public int round;
    public int totalRounds;
    public CardView[] offers;
    public bool picked;
    public bool opponentPicked;
    public CardView[] myDeck;       // 강화된 카드는 level > 0

    public int turn;
    public float turnDuration;
    public CombatantView me;
    public CombatantView opponent;

    public string result;           // win | lose | draw
    public string reason;
    public bool rematchRequested;
    public bool opponentRematchRequested;

    public EventView[] events;      // 지난 state 이후 일어난 일 (효과음/연출용)
}

/// <summary>
/// kind: use(카드 사용 시도, ok=성공) | play(큐에서 발동, ok=공격 카드) | damage(피격, guarded=가드 중)
///       | shield(방어도로 막음) | effect(효과 획득, effect=종류) | turnEnd
/// mine: 내 쪽에서 일어난 일인지
/// </summary>
[Serializable]
public sealed class EventView
{
    public string kind;
    public bool mine;
    public string card;
    public int amount;
    public string effect;
    public bool guarded;
    public bool ok;
}

[Serializable]
public sealed class CombatantView
{
    public string name;
    public int hp;
    public int maxHp;
    public int defense;
    public int cost;
    public int maxCost;
    public bool guard;
    public int selectedIndex;
    public int deckCount;
    public int handCount;
    public EffectView[] effects;
    public CardView[] hand;         // 빈 칸은 id == 0
    public CardView[] queue;
}

[Serializable]
public sealed class EffectView
{
    public string type;
    public int magnitude;
    public bool infinite;
}

[Serializable]
public sealed class CardView
{
    public int id;                  // 0이면 빈 칸. 상대 손패는 id만 있다 (뒷면)
    public string name;
    public int level;               // 강화 횟수 (그림은 name 그대로, 수치/효과만 바뀜)
    public string offerKind;        // 카드 선택지일 때: add | enhance
    public int target;              // enhance일 때 바뀌는 내 덱 카드 번호
    public string cardType;
    public int cost;
    public int cooldown;
    public int cooldownLeft;
    public bool playable;
    public CardEffectView[] effects;

    public bool IsEmpty => id == 0;
    public bool IsHidden => id != 0 && string.IsNullOrEmpty(name);
}

[Serializable]
public sealed class CardEffectView
{
    public string type;
    public int magnitude;
    public string target;           // User | Opponent
    public string category;         // Instant | Continuous | Mix
}
