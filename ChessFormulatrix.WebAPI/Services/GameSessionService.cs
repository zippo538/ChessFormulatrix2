using System.Collections.Concurrent;
using ChessFormularix.Console;

namespace ChessFormulatrix.WebAPI.Services;

/// <summary>
/// Shared in-memory store for active chess games.
/// Registered as singleton so both controllers and SignalR hubs share the same dictionary.
/// </summary>
public class GameSessionService
{
    private readonly ConcurrentDictionary<string, Game> _games = new();

    public bool TryAdd(string gameId, Game game) => _games.TryAdd(gameId, game);

    public bool TryGet(string gameId, out Game? game) => _games.TryGetValue(gameId, out game);

    public bool TryRemove(string gameId) => _games.TryRemove(gameId, out _);

    public Game? Get(string gameId) => _games.GetValueOrDefault(gameId);
}
