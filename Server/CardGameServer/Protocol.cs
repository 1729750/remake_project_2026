using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGameServer.Game;

namespace CardGameServer;

/// <summary>
/// 클라이언트 ↔ 서버 메시지. 모든 메시지는 JSON 텍스트 한 덩어리이고 "type" 필드로 구분한다.
///
/// Client → Server
///   { "type": "hello", "name": "희교" }
///   { "type": "create_room" }
///   { "type": "join_room", "code": "ABC123" }
///   { "type": "leave_room" }
///   { "type": "ping" }
///   { "type": "pick", "index": 0 }        // 카드 선택 단계: 제시된 3장 중 하나
///   { "type": "skip" }                    // 카드 선택 단계: 이번 라운드 건너뛰기
///   { "type": "play", "index": 0 }        // 전투: 0~3 손패 카드, -2 리드로우, -1 가드 토글
///   { "type": "rematch" }                 // 결과 화면에서 다시 하기 (둘 다 누르면 새 게임)
///
/// Server → Client
///   { "type": "welcome", "playerId": "..." }
///   { "type": "room_joined", "code": "...", "players": [...] }   // 방 생성/참가 성공 모두 이것
///   { "type": "player_joined", "player": {...} }
///   { "type": "player_left", "playerId": "..." }
///   { "type": "room_ready", "code": "..." }                      // 2명이 모이면
///   { "type": "room_left" }
///   { "type": "error", "message": "..." }
///   { "type": "pong" }
///   { "type": "state", "state": {...} }                         // 게임 진행 중 바뀔 때마다 (Game/Views.cs)
/// </summary>
public static class Protocol
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 한글을 \uXXXX로 바꾸지 않고 그대로 보낸다
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

public sealed record ClientMessage(string? Type, string? Name, string? Code, int Index);

public sealed record PlayerInfo(string PlayerId, string Name);

public sealed record ServerMessage(
    string Type,
    string? PlayerId = null,
    string? Code = null,
    string? Message = null,
    PlayerInfo? Player = null,
    IReadOnlyList<PlayerInfo>? Players = null,
    StateView? State = null);
