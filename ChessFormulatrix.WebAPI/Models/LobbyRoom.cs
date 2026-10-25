namespace ChessFormulatrix.WebAPI.Models;

public class LobbyRoom
{
    public string RoomId { get; set; } = Guid.NewGuid().ToString();
    public string RoomName { get; set; } = string.Empty;
    public int Minutes { get; set; } = 10;
    public bool ClockEnabled { get; set; } = true;

    // Host info
    public string HostConnectionId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;

    // Guest info (null until someone joins)
    public string? GuestConnectionId { get; set; }
    public string? GuestName { get; set; }

    // Linked game ID once match starts
    public string? GameId { get; set; }

    public bool IsFull => GuestConnectionId is not null;
    public bool IsStarted => GameId is not null;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
