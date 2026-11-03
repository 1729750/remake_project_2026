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

    public static ClientMessage Hello(string name) => new ClientMessage { type = "hello", name = name };
    public static ClientMessage CreateRoom() => new ClientMessage { type = "create_room" };
    public static ClientMessage JoinRoom(string code) => new ClientMessage { type = "join_room", code = code };
    public static ClientMessage LeaveRoom() => new ClientMessage { type = "leave_room" };
    public static ClientMessage Ping() => new ClientMessage { type = "ping" };

    /// <summary>카드 선택 단계: 제시된 3장 중 index번.</summary>
    public static ClientMessage Pick(int index) => new ClientMessage { type = "pick", index = index };

    /// <summary>전투: 0~3 손패 카드, -2 리드로우, -1 가드 토글.</summary>
    public static ClientMessage Play(int index) => new ClientMessage { type = "play", index = index };

    public static ClientMessage Rematch() => new ClientMessage { type = "rematch" };
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
    public string[] myDeck;

    public int turn;
    public float turnDuration;
    public CombatantView me;
    public CombatantView opponent;

    public string result;           // win | lose | draw
    public string reason;
    public bool rematchRequested;
    public bool opponentRematchRequested;
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
    public CardView[] hand;         // 빈 칸은 name == ""
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
    public string name;
    public string cardType;
    public int cost;
    public int cooldown;
    public int cooldownLeft;
    public bool playable;
    public CardEffectView[] effects;

    public bool IsEmpty => string.IsNullOrEmpty(name);
}

[Serializable]
public sealed class CardEffectView
{
    public string type;
    public int magnitude;
    public string target;           // User | Opponent
    public string category;         // Instant | Continuous | Mix
}
