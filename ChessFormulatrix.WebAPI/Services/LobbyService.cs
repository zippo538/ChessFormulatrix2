using System.Collections.Concurrent;
using ChessFormulatrix.WebAPI.Models;

namespace ChessFormulatrix.WebAPI.Services;

/// <summary>
/// In-memory lobby room management. Singleton lifetime.
/// </summary>
public class LobbyService
{
    private readonly ConcurrentDictionary<string, LobbyRoom> _rooms = new();

    /// <summary>Maps connectionId → roomId for fast lookup on disconnect.</summary>
    private readonly ConcurrentDictionary<string, string> _connectionRoomMap = new();

    public LobbyRoom CreateRoom(string hostConnectionId, string hostName, string roomName, int minutes, bool clockEnabled)
    {
        var room = new LobbyRoom
        {
            HostConnectionId = hostConnectionId,
            HostName = hostName,
            RoomName = string.IsNullOrWhiteSpace(roomName) ? $"{hostName}'s Room" : roomName,
            Minutes = Math.Clamp(minutes, 1, 180),
            ClockEnabled = clockEnabled
        };
        _rooms[room.RoomId] = room;
        _connectionRoomMap[hostConnectionId] = room.RoomId;
        return room;
    }

    public LobbyRoom? JoinRoom(string roomId, string guestConnectionId, string guestName)
    {
        if (!_rooms.TryGetValue(roomId, out var room) || room.IsFull || room.IsStarted)
            return null;

        room.GuestConnectionId = guestConnectionId;
        room.GuestName = string.IsNullOrWhiteSpace(guestName) ? "Guest" : guestName;
        _connectionRoomMap[guestConnectionId] = roomId;
        return room;
    }

    public LobbyRoom? GetRoom(string roomId) => _rooms.GetValueOrDefault(roomId);

    public string? GetRoomIdByConnection(string connectionId) =>
        _connectionRoomMap.GetValueOrDefault(connectionId);

    public List<LobbyRoom> GetAvailableRooms() =>
        _rooms.Values.Where(r => !r.IsFull && !r.IsStarted).ToList();

    public bool RemoveRoom(string roomId)
    {
        if (!_rooms.TryRemove(roomId, out var room)) return false;
        _connectionRoomMap.TryRemove(room.HostConnectionId, out _);
        if (room.GuestConnectionId is not null)
            _connectionRoomMap.TryRemove(room.GuestConnectionId, out _);
        return true;
    }

    /// <summary>
    /// Removes a player from their room. Returns the room if found, null otherwise.
    /// If host leaves, room is destroyed. If guest leaves, slot is freed.
    /// </summary>
    public (LobbyRoom? Room, bool RoomDestroyed) LeaveRoom(string connectionId)
    {
        if (!_connectionRoomMap.TryRemove(connectionId, out var roomId))
            return (null, false);

        if (!_rooms.TryGetValue(roomId, out var room))
            return (null, false);

        if (room.HostConnectionId == connectionId)
        {
            // Host left — destroy room
            _rooms.TryRemove(roomId, out _);
            if (room.GuestConnectionId is not null)
                _connectionRoomMap.TryRemove(room.GuestConnectionId, out _);
            return (room, true);
        }

        // Guest left — free slot
        room.GuestConnectionId = null;
        room.GuestName = null;
        return (room, false);
    }

    public void MarkStarted(string roomId, string gameId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
            room.GameId = gameId;
    }
}
