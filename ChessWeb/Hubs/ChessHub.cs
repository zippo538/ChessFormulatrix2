using Microsoft.AspNetCore.SignalR;

namespace ChessAPI.Profiles.Hubs;

public class ChessHub : Hub
{
    public async Task JoinGame(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            throw new HubException("GameId wajib diisi.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGroupName(gameId));
    }

    public async Task LeaveGame(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetGroupName(gameId));
    }

    private static string GetGroupName(string gameId)
    {
        return $"game:{gameId}";
    }
}