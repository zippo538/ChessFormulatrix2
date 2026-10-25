using AutoMapper;
using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Model;
using ChessFormulatrix.WebAPI.DTO;
using ChessFormulatrix.WebAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using ILogger = Serilog.ILogger;

namespace ChessFormulatrix.WebAPI.Hubs;

public class GameHub(GameSessionService sessions, IMapper mapper) : Hub
{
    private static readonly ILogger Logger = Log.ForContext<GameHub>();

    /// <summary>Add caller to a SignalR group for the given game so they receive broadcasts.</summary>
    public async Task JoinGame(string gameId)
    {
        if (sessions.Get(gameId) is null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        Logger.Information("Connection {ConnectionId} joined game {GameId}", Context.ConnectionId, gameId);

        // Send current board to the joining player
        var game = sessions.Get(gameId)!;
        Board board = game.GetCurrentBoard();
        BoardDto boardDto = mapper.Map<BoardDto>(board);
        await Clients.Caller.SendAsync("BoardUpdated", boardDto);
    }

    public async Task GetBoard(string gameId)
    {
        var game = sessions.Get(gameId);
        if (game is null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        Board board = game.GetCurrentBoard();
        BoardDto boardDto = mapper.Map<BoardDto>(board);
        await Clients.Caller.SendAsync("BoardUpdated", boardDto);
    }

    public async Task MakeMove(string gameId, PosititionDto from, PosititionDto to, string? promotionType)
    {
        var game = sessions.Get(gameId);
        if (game is null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        // Time check
        var timeStatus = game.CheckTimeStatus();
        if (timeStatus.IsExpired)
        {
            await Clients.Group(gameId).SendAsync("GameOver", new
            {
                Reason = nameof(GameOverType.Timeout),
                Winner = game.GameResult.Winner.ToString(),
                TimedOutPlayer = timeStatus.TimedOutColor?.ToString()
            });
            return;
        }

        PieceType promotion = PieceType.None;
        if (!string.IsNullOrEmpty(promotionType))
            Enum.TryParse(promotionType, out promotion);

        var fromPos = new Postition { Row = from.Row, Column = from.Column };
        var toPos = new Postition { Row = to.Row, Column = to.Column };

        if (!game.TrySelectPiece(fromPos))
        {
            await Clients.Caller.SendAsync("MoveRejected", "Invalid selection.");
            return;
        }

        List<Postition> available = game.GetAvailableMovesForSelectedPiece();
        if (!available.Exists(m => m.Row == toPos.Row && m.Column == toPos.Column))
        {
            await Clients.Caller.SendAsync("MoveRejected", "Illegal move.");
            return;
        }

        var move = new Move
        {
            From = fromPos,
            To = toPos,
            Type = MoveType.Normal,
            PromotionType = promotion
        };

        bool applied = game.MakeMove(move);
        if (!applied)
        {
            await Clients.Caller.SendAsync("MoveRejected", "Move rejected by engine.");
            return;
        }

        Logger.Information("Move in game {GameId}: ({FR},{FC})→({TR},{TC})",
            gameId, fromPos.Row, fromPos.Column, toPos.Row, toPos.Column);

        var moveInfo = new
        {
            From = new { from.Row, from.Column },
            To = new { to.Row, to.Column },
            PromotionType = promotion == PieceType.None ? null : promotion.ToString(),
            CurrentTurn = game.GetColorsPlayer().ToString()
        };

        // Check game over
        if (game.GameResult.Type != GameOverType.None)
        {
            await Clients.Group(gameId).SendAsync("MoveMade", moveInfo);
            await Clients.Group(gameId).SendAsync("GameOver", new
            {
                Reason = game.GameResult.Type.ToString(),
                Winner = game.GameResult.Winner.ToString()
            });
            return;
        }

        await Clients.Group(gameId).SendAsync("MoveMade", moveInfo);

        // Send updated board to both players
        Board board = game.GetCurrentBoard();
        BoardDto boardDto = mapper.Map<BoardDto>(board);
        await Clients.Group(gameId).SendAsync("BoardUpdated", boardDto);
    }

    public async Task GetValidMoves(string gameId, PosititionDto position)
    {
        var game = sessions.Get(gameId);
        if (game is null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        var pos = new Postition { Row = position.Row, Column = position.Column };
        if (game.TrySelectPiece(pos))
        {
            List<Postition> moves = game.GetAvailableMovesForSelectedPiece();
            List<PosititionDto> movesDto = mapper.Map<List<PosititionDto>>(moves);
            await Clients.Caller.SendAsync("ValidMoves", movesDto);
        }
        else
        {
            await Clients.Caller.SendAsync("ValidMoves", Array.Empty<PosititionDto>());
        }
    }

    public async Task Resign(string gameId)
    {
        var game = sessions.Get(gameId);
        if (game is null) return;

        game.Resign(game.CurrentPlayer);
        Logger.Information("Player resigned in game {GameId}. Winner: {Winner}", gameId, game.GameResult.Winner);

        await Clients.Group(gameId).SendAsync("GameOver", new
        {
            Reason = game.GameResult.Type.ToString(),
            Winner = game.GameResult.Winner.ToString()
        });
    }

    public async Task GetTimeStatus(string gameId)
    {
        var game = sessions.Get(gameId);
        if (game is null) return;

        var status = game.CheckTimeStatus();
        await Clients.Caller.SendAsync("TimeUpdate", new
        {
            WhiteRemainingSeconds = Math.Max(0, Math.Ceiling(status.WhiteRemaining.TotalSeconds)),
            BlackRemainingSeconds = Math.Max(0, Math.Ceiling(status.BlackRemaining.TotalSeconds)),
            CurrentTurn = game.GetColorsPlayer().ToString(),
            TimeExpired = status.IsExpired
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Logger.Debug("GameHub connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
