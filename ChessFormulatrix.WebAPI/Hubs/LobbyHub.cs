using ChessFormularix.Console;
using ChessFormularix.Console.Model;
using ChessFormulatrix.WebAPI.Models;
using ChessFormulatrix.WebAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Color = ChessFormularix.Console.Enums.Color;
using ILogger = Serilog.ILogger;

namespace ChessFormulatrix.WebAPI.Hubs;

public class LobbyHub(LobbyService lobby, GameSessionService sessions) : Hub
{
    private static readonly ILogger Logger = Log.ForContext<LobbyHub>();

    public async Task CreateRoom(string playerName, string roomName, int minutes, bool clockEnabled)
    {
        LobbyRoom room = lobby.CreateRoom(Context.ConnectionId, playerName, roomName, minutes, clockEnabled);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

        Logger.Information("Room {RoomId} created by {PlayerName}", room.RoomId, playerName);

        await Clients.Caller.SendAsync("RoomCreated", MapRoom(room));
        await Clients.All.SendAsync("RoomListUpdated", lobby.GetAvailableRooms().Select(MapRoom));
    }

    public async Task JoinRoom(string roomId, string playerName)
    {
        LobbyRoom? room = lobby.JoinRoom(roomId, Context.ConnectionId, playerName);
        if (room is null)
        {
            await Clients.Caller.SendAsync("Error", "Room not available.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        Logger.Information("Player {PlayerName} joined room {RoomId}", playerName, roomId);

        await Clients.Group(roomId).SendAsync("PlayerJoined", new
        {
            room.RoomId,
            GuestName = room.GuestName
        });

        // Auto-start game when room is full
        await StartGame(room);
    }

    public async Task LeaveRoom()
    {
        var (room, destroyed) = lobby.LeaveRoom(Context.ConnectionId);
        if (room is null) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.RoomId);

        if (destroyed)
        {
            Logger.Information("Room {RoomId} destroyed (host left)", room.RoomId);
            // Notify guest if they were in the room
            if (room.GuestConnectionId is not null)
            {
                await Clients.Client(room.GuestConnectionId).SendAsync("RoomClosed", "Host left the room.");
                await Groups.RemoveFromGroupAsync(room.GuestConnectionId, room.RoomId);
            }
        }
        else
        {
            Logger.Information("Guest left room {RoomId}", room.RoomId);
            await Clients.Group(room.RoomId).SendAsync("PlayerLeft", Context.ConnectionId);
        }

        await Clients.All.SendAsync("RoomListUpdated", lobby.GetAvailableRooms().Select(MapRoom));
    }

    public async Task GetRooms()
    {
        var rooms = lobby.GetAvailableRooms().Select(MapRoom);
        await Clients.Caller.SendAsync("RoomListUpdated", rooms);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up room on disconnect
        var (room, destroyed) = lobby.LeaveRoom(Context.ConnectionId);
        if (room is not null)
        {
            if (destroyed && room.GuestConnectionId is not null)
            {
                await Clients.Client(room.GuestConnectionId).SendAsync("RoomClosed", "Host disconnected.");
                await Groups.RemoveFromGroupAsync(room.GuestConnectionId, room.RoomId);
            }
            else if (!destroyed)
            {
                await Clients.Group(room.RoomId).SendAsync("PlayerLeft", Context.ConnectionId);
            }

            await Clients.All.SendAsync("RoomListUpdated", lobby.GetAvailableRooms().Select(MapRoom));
        }

        Logger.Debug("LobbyHub connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task StartGame(LobbyRoom room)
    {
        var gameId = Guid.NewGuid().ToString();
        var gameLogger = Logger.ForContext("GameId", gameId);

        var white = new Player(room.HostName, Color.White);
        var black = new Player(room.GuestName ?? "Guest", Color.Black);

        var game = new Game(
            white, black,
            vsBot: false,
            initialMinutes: room.Minutes,
            clockEnabled: room.ClockEnabled,
            logger: gameLogger,
            gameId: gameId);
        game.StartGame();

        if (!sessions.TryAdd(gameId, game))
        {
            await Clients.Group(room.RoomId).SendAsync("Error", "Failed to create game session.");
            return;
        }

        lobby.MarkStarted(room.RoomId, gameId);

        Logger.Information("Game {GameId} started from room {RoomId}. {White} vs {Black}",
            gameId, room.RoomId, room.HostName, room.GuestName);

        await Clients.Group(room.RoomId).SendAsync("GameStarted", new
        {
            GameId = gameId,
            WhitePlayer = room.HostName,
            BlackPlayer = room.GuestName,
            room.Minutes,
            room.ClockEnabled
        });
    }

    private static object MapRoom(LobbyRoom r) => new
    {
        r.RoomId,
        r.RoomName,
        r.HostName,
        r.GuestName,
        r.Minutes,
        r.ClockEnabled,
        r.IsFull,
        r.IsStarted,
        CreatedAt = r.CreatedAt.ToString("o")
    };
}
