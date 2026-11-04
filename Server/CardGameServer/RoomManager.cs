using System.Diagnostics;
using System.Security.Cryptography;
using CardGameServer.Game;

namespace CardGameServer;

public sealed class Room
{
    public const int MaxPlayers = 2;

    public string Code { get; }
    public List<PlayerConnection> Players { get; } = new();
    public Match? Match { get; set; }
    public CancellationTokenSource? MatchLoop { get; set; }

    public Room(string code)
    {
        Code = code;
    }

    public void Broadcast(ServerMessage message, PlayerConnection? except = null)
    {
        foreach (PlayerConnection player in Players)
        {
            if (player != except)
            {
                player.Send(message);
            }
        }
    }
}

/// <summary>
/// 방 생성 / 참가 / 나가기와, 방이 차면 Match(게임 진행)를 시작하는 일.
/// 방 목록 변경은 하나의 lock 안에서 처리한다. (1v1 카드게임이라 방 수가 적어서 전역 lock으로 충분하다.)
/// Match 안의 게임 상태는 Match 자신의 lock으로 보호한다.
/// </summary>
public sealed class RoomManager
{
    // 헷갈리는 문자(0/O, 1/I/L) 제외
    private const string CodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);

    private readonly object gate = new();
    private readonly Dictionary<string, Room> rooms = new();
    private readonly CardLibrary library;
    private readonly ILogger<RoomManager> logger;

    public RoomManager(CardLibrary library, ILogger<RoomManager> logger)
    {
        this.library = library;
        this.logger = logger;
    }

    public int RoomCount
    {
        get { lock (gate) return rooms.Count; }
    }

    public void CreateRoom(PlayerConnection player)
    {
        lock (gate)
        {
            if (player.Room != null)
            {
                player.Send(new ServerMessage("error", Message: "이미 방에 들어가 있습니다."));
                return;
            }

            string code;
            do
            {
                code = RandomNumberGenerator.GetString(CodeChars, CodeLength);
            }
            while (rooms.ContainsKey(code));

            var room = new Room(code);
            rooms.Add(code, room);
            AddPlayer(room, player);

            logger.LogInformation("Room {Code} created by {Player}", code, player.PlayerId);
        }
    }

    public void JoinRoom(PlayerConnection player, string? rawCode)
    {
        string code = rawCode?.Trim().ToUpperInvariant() ?? string.Empty;

        lock (gate)
        {
            if (player.Room != null)
            {
                player.Send(new ServerMessage("error", Message: "이미 방에 들어가 있습니다."));
                return;
            }

            if (!rooms.TryGetValue(code, out Room? room))
            {
                player.Send(new ServerMessage("error", Message: "방을 찾을 수 없습니다."));
                return;
            }

            if (room.Players.Count >= Room.MaxPlayers)
            {
                player.Send(new ServerMessage("error", Message: "방이 가득 찼습니다."));
                return;
            }

            AddPlayer(room, player);

            logger.LogInformation("Room {Code} joined by {Player}", code, player.PlayerId);

            if (room.Players.Count == Room.MaxPlayers)
            {
                room.Broadcast(new ServerMessage("room_ready", Code: room.Code));
                StartMatch(room);
            }
        }
    }

    public void LeaveRoom(PlayerConnection player)
    {
        lock (gate)
        {
            Room? room = player.Room;
            if (room == null)
            {
                return;
            }

            room.Match?.Forfeit(player);
            StopMatchLoop(room);

            room.Players.Remove(player);
            player.Room = null;
            player.Send(new ServerMessage("room_left"));
            room.Broadcast(new ServerMessage("player_left", PlayerId: player.PlayerId));

            if (room.Players.Count == 0)
            {
                rooms.Remove(room.Code);
                logger.LogInformation("Room {Code} closed", room.Code);
            }
        }
    }

    // =========================================================
    // Game Input
    // =========================================================

    public void Pick(PlayerConnection player, int index) => CurrentMatch(player)?.HandlePick(player, index);

    public void Skip(PlayerConnection player) => CurrentMatch(player)?.HandleSkip(player);

    public void Play(PlayerConnection player, int index) => CurrentMatch(player)?.HandlePlay(player, index);

    public void Rematch(PlayerConnection player)
    {
        Match? match = CurrentMatch(player);
        if (match == null || !match.HandleRematch(player))
        {
            return;
        }

        lock (gate)
        {
            Room? room = player.Room;
            if (room != null && room.Match == match && room.Players.Count == Room.MaxPlayers)
            {
                StartMatch(room);
            }
        }
    }

    private Match? CurrentMatch(PlayerConnection player)
    {
        lock (gate)
        {
            return player.Room?.Match;
        }
    }

    // =========================================================
    // Match
    // =========================================================

    private void StartMatch(Room room)
    {
        StopMatchLoop(room);

        var match = new Match(library, room.Players[0], room.Players[1]);
        var loop = new CancellationTokenSource();
        room.Match = match;
        room.MatchLoop = loop;

        match.Start();
        _ = RunMatchLoopAsync(room.Code, match, loop.Token);

        logger.LogInformation("Room {Code} match started", room.Code);
    }

    private static void StopMatchLoop(Room room)
    {
        room.MatchLoop?.Cancel();
        room.MatchLoop?.Dispose();
        room.MatchLoop = null;
    }

    private async Task RunMatchLoopAsync(string code, Match match, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TickInterval);
        var clock = Stopwatch.StartNew();
        double last = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                double now = clock.Elapsed.TotalSeconds;
                match.Tick(now - last);
                last = now;

                if (match.IsFinished)
                {
                    logger.LogInformation("Room {Code} match finished", code);
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            // 규칙 코드에서 예외가 나도 서버 전체가 죽지 않게 이 방 게임만 끝낸다
            logger.LogError(exception, "Room {Code} match crashed", code);
            match.Abort("서버 오류로 게임이 중단되었습니다.");
        }
    }

    private static void AddPlayer(Room room, PlayerConnection player)
    {
        room.Players.Add(player);
        player.Room = room;

        player.Send(new ServerMessage(
            "room_joined",
            Code: room.Code,
            Players: room.Players.Select(p => p.Info).ToList()));

        room.Broadcast(new ServerMessage("player_joined", Player: player.Info), except: player);
    }
}
