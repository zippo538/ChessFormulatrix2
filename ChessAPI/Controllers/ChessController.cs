using System.Collections.Concurrent;
using AutoMapper;
using ChessConsole.Enums;
using ChessConsole.Model;
using ChessConsole2;
using ChessWeb.DTO;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace ChessAPI.Controllers;
[ApiController]
[Route("api/[controller]")]
public class ChessController : ControllerBase
{
    // Penyimpanan sesi game di memori (untuk production, gunakan Redis atau Database)
    private static readonly ConcurrentDictionary<string, Game> ActiveGames = new();
    
    private readonly IMapper _mapper;
    private readonly Serilog.ILogger _logger;

    public ChessController(IMapper mapper)
    {
         _mapper = mapper;
         _logger = Log.ForContext<ChessController>();
    }
    
    private Serilog.ILogger ForGame(string gameId) =>
        _logger.ForContext("GameId", gameId);

    private bool TryGetGame(string gameId, out Game game)
    {
        if (ActiveGames.TryGetValue(gameId, out game!))
            return true;

        ForGame(gameId).Warning("Chess game session was not found");
        return false;
    }


    // ==========================================
    // 1. CREATE & GET GAME
    // ==========================================

    [HttpPost]
    public IActionResult CreateGame([FromBody] CreateGameDTO request)
    {
        var gameId = Guid.NewGuid().ToString();
        var gameLogger = ForGame(gameId);
        try
        {
            // Nama Putih boleh kosong (mis. mode Latihan yang tidak menampilkan
            // input nama sama sekali) -> fallback ke "Anda".
            var whiteName = string.IsNullOrWhiteSpace(request.WhiteName) ? "Anda" : request.WhiteName;

            // Membuat instance game menggunakan model asli dari ChessConsole
            var whitePlayer = new Player(whiteName, Color.White);

            // VsBot=true  -> Hitam dikendalikan bot Negamax internal.
            // VsBot=false -> dipakai untuk PvP maupun Latihan (sandbox dua sisi).
            //   Nama Hitam boleh kosong (mode Latihan tidak menampilkannya) -> fallback "Pemain Hitam".
            var blackName = request.VsBot
                ? "AI Bot"
                : (string.IsNullOrWhiteSpace(request.BlackName) ? "Pemain Hitam" : request.BlackName);
            var blackPlayer = new Player(blackName, Color.Black);

            var minutes = Math.Clamp(request.Minutes, 1, 180);
            var game = new Game(
                whitePlayer,
                blackPlayer,
                request.VsBot,
                minutes,
                request.ClockEnabled,
                logger: gameLogger,
                gameId: gameId);
            game.StartGame();

            if (!ActiveGames.TryAdd(gameId, game))
            {
                gameLogger.Error("Failed to register chess game session in active game storage");
                return StatusCode(500, "Gagal membuat sesi permainan.");
            }

            gameLogger.Information(
                "Chess game created. WhitePlayer: {WhitePlayer}, BlackPlayer: {BlackPlayer}, " +
                "VsBot: {VsBot}, Minutes: {Minutes}, ClockEnabled: {ClockEnabled}",
                whiteName,
                blackName,
                request.VsBot,
                minutes,
                request.ClockEnabled);

            return Ok(new
            {
                GameId = gameId,
                Message = "Game started",
                request.VsBot,
                Minutes = minutes,
                request.ClockEnabled
            });
        }
        catch (Exception exception)
        {
            gameLogger.Error(exception, "Unexpected error while creating chess game");
            return StatusCode(500, "Terjadi kesalahan saat membuat permainan.");
        }
    }

    [HttpGet("{gameId}/board")]
    public ActionResult<BoardDTO> GetBoard(string gameId)
    {
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var board = game.GetCurrentBoard();
        var boardDto = _mapper.Map<BoardDTO>(board);
        ForGame(gameId).Debug("Current chess board returned");

        return Ok(boardDto);
    }


    [HttpGet("{gameId}/valid-moves")]
    public ActionResult<List<PosititionDTO>> GetValidMoves(string gameId, [FromQuery] int row, [FromQuery] int col)
    {
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var gameLogger = ForGame(gameId);
        var position = new Postition { Row = row, Column = col };

        if (game.TrySelectPiece(position))
        {
            var moves = game.GetAvailableMovesForSelectedPiece();
            var movesDto = _mapper.Map<List<PosititionDTO>>(moves);
            gameLogger.Debug(
                "Valid moves returned for position ({Row}, {Column}). MoveCount: {MoveCount}",
                row,
                col,
                moves.Count);
            return Ok(movesDto);
        }
        gameLogger.Warning(
            "Valid-move request rejected for position ({Row}, {Column})",
            row,
            col);
        return BadRequest("Tidak ada bidak milik pemain saat ini di koordinat tersebut.");
    }

    [HttpPost("{gameId}/moves")]
    public IActionResult MakeMove(string gameId, [FromBody] MoveDTO moveDto)
    {
        
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");
        var gameLogger = ForGame(gameId);
        try
        {
            
        var timeStatus = game.CheckTimeStatus();
            if (timeStatus.IsExpired)
            {
                gameLogger.Warning(
                    "Move rejected because game time expired. TimedOutPlayer: {TimedOutPlayer}",
                    timeStatus.TimedOutColor);
                return Conflict(new
                {
                    GameOver = true,
                    Reason = GameOverType.Timeout.ToString(),
                    Winner = game.GameResult.Winner.ToString(),
                    TimedOutPlayer = timeStatus.TimedOutColor?.ToString()
                });
            }

            // Validasi dan parsing PromotionType
            PieceType promotion = PieceType.None;
            if (!string.IsNullOrEmpty(moveDto.PromotionType) &&
                !Enum.TryParse(moveDto.PromotionType, out promotion))
            {
                gameLogger.Warning(
                    "Unknown promotion type received; PieceType.None will be used. PromotionType: {PromotionType}",
                    moveDto.PromotionType);
            }

            var fromPosition = new Postition { Row = moveDto.From.Row, Column = moveDto.From.Column };
            var toPosition = new Postition { Row = moveDto.To.Row, Column = moveDto.To.Column };

            gameLogger.Debug(
                "Move request received. From: ({FromRow}, {FromColumn}), " +
                "To: ({ToRow}, {ToColumn}), PromotionType: {PromotionType}",
                fromPosition.Row,
                fromPosition.Column,
                toPosition.Row,
                toPosition.Column,
                promotion);

            // WAJIB: SelectPiece dulu sebelum MakeMove sesuai dengan alur di Program.cs Anda
            if (!game.TrySelectPiece(fromPosition))
            {
                gameLogger.Warning(
                    "Move rejected because piece selection is invalid. From: ({FromRow}, {FromColumn})",
                    fromPosition.Row,
                    fromPosition.Column);
                return BadRequest("Invalid selection.");
            }

            var availableMoves = game.GetAvailableMovesForSelectedPiece();

            // Memastikan tujuan legal
            if (!availableMoves.Exists(m =>
                    m.Row == toPosition.Row && m.Column == toPosition.Column))
            {
                gameLogger.Warning(
                    "Move rejected because destination is illegal. From: ({FromRow}, {FromColumn}), " +
                    "To: ({ToRow}, {ToColumn})",
                    fromPosition.Row,
                    fromPosition.Column,
                    toPosition.Row,
                    toPosition.Column);
                return BadRequest("Illegal move.");
            }

            var move = new Move
            {
                From = fromPosition,
                To = toPosition,
                Type = MoveType.Normal,
                PromotionType = promotion
            };

            var moveApplied = game.MakeMove(move);
            if (!moveApplied)
            {
                gameLogger.Warning(
                    "Move rejected by chess engine. From: ({FromRow}, {FromColumn}), " +
                    "To: ({ToRow}, {ToColumn})",
                    fromPosition.Row,
                    fromPosition.Column,
                    toPosition.Row,
                    toPosition.Column);
            }
            else
            {
                gameLogger.Information(
                    "Player move completed. From: ({FromRow}, {FromColumn}), " +
                    "To: ({ToRow}, {ToColumn}), PromotionType: {PromotionType}",
                    fromPosition.Row,
                    fromPosition.Column,
                    toPosition.Row,
                    toPosition.Column,
                    promotion);
            }

            // Cek kondisi akhir permainan setelah move pemain
            if (game.GameResult.Type != GameOverType.None)
            {
                gameLogger.Information(
                    "Chess game ended after player move. Reason: {Reason}, Winner: {Winner}",
                    game.GameResult.Type,
                    game.GameResult.Winner);
                return Ok(new
                {
                    GameOver = true,
                    Reason = game.GameResult.Type.ToString(),
                    Winner = game.GameResult.Winner.ToString()
                });
            }

            // Mode vs Bot: giliran Hitam otomatis dibalas oleh Negamax
            // internal dengan validasi ChessDotNet di Game.cs.
            // Mode PvP (VsBot = false): dilewati, giliran Hitam menunggu input pemain.
            object? botMoveInfo = null;
            if (game.VsBot && game.GetColorsPlayer() == Color.Black)
            {
                var botMove = game.MakeBotMove();
                if (botMove.HasValue)
                {
                    botMoveInfo = new
                    {
                        From = new { row = botMove.Value.From.Row, botMove.Value.From.Column },
                        To = new { row = botMove.Value.To.Row, botMove.Value.To.Column }
                    };

                    gameLogger.Information(
                        "Automatic bot move completed. From: ({FromRow}, {FromColumn}), " +
                        "To: ({ToRow}, {ToColumn}), Depth: {Depth}",
                        botMove.Value.From.Row,
                        botMove.Value.From.Column,
                        botMove.Value.To.Row,
                        botMove.Value.To.Column,
                        Game.DefaultBotDepth);
                }
                else
                {
                    gameLogger.Warning("Bot did not return a move after player move");
                }
            }

            if (game.GameResult.Type != GameOverType.None)
            {
                gameLogger.Information(
                    "Chess game ended after bot move. Reason: {Reason}, Winner: {Winner}",
                    game.GameResult.Type,
                    game.GameResult.Winner);
                return Ok(new
                {
                    GameOver = true,
                    Reason = game.GameResult.Type.ToString(),
                    Winner = game.GameResult.Winner.ToString(),
                    BotMove = botMoveInfo
                });
            }

            return Ok(new
            {
                GameOver = false,
                CurrentTurn = game.GetColorsPlayer().ToString(),
                BotMove = botMoveInfo
            });
        }
        catch (Exception exception)
        {
            gameLogger.Error(
                exception,
                "Unexpected error while processing player move");
            return StatusCode(500, "Terjadi kesalahan saat memproses langkah.");
        }
    }

    // ==========================================
    // 3. ENGINE SUGGESTION
    // ==========================================

    [HttpGet("{gameId}/suggestion")]
    public ActionResult<MoveDTO> GetSuggestion(string gameId, [FromQuery] int row, [FromQuery] int col)
    {
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");
        var gameLogger = ForGame(gameId);

        try
        {
            var position = new Postition { Row = row, Column = col };

            gameLogger.Debug(
                "Move suggestion requested for position ({Row}, {Column})",
                row,
                col);

            // Panggil TrySelectPiece dulu, baru GetSuggestedMoveForPiece
            if (game.TrySelectPiece(position))
            {
                var suggestedMove = game.GetSuggestedMoveForPiece(position);

                if (suggestedMove.HasValue)
                {
                    gameLogger.Information(
                        "Move suggestion generated. From: ({FromRow}, {FromColumn}), " +
                        "To: ({ToRow}, {ToColumn})",
                        suggestedMove.Value.From.Row,
                        suggestedMove.Value.From.Column,
                        suggestedMove.Value.To.Row,
                        suggestedMove.Value.To.Column);
                    var suggestionDto = _mapper.Map<MoveDTO>(suggestedMove.Value);
                    return Ok(suggestionDto);
                }
            }

            gameLogger.Warning(
                "No move suggestion available for position ({Row}, {Column})",
                row,
                col);
            return NotFound("Tidak ada saran langkah.");
        }
        catch (Exception exception)
        {
            gameLogger.Error(
                exception,
                "Unexpected error while generating move suggestion for position ({Row}, {Column})",
                row,
                col);
            return StatusCode(500, "Terjadi kesalahan saat membuat saran langkah.");
        }
    }

    // ==========================================
    // 4. BOT (AI) MOVE
    // ==========================================

    // Endpoint manual untuk memicu langkah Bot secara eksplisit.
    // Berguna jika Anda ingin memisahkan "giliran pemain" dan "giliran bot"
    // di sisi client (misal menampilkan animasi "Bot sedang berpikir...").
    // Jika Anda memakai auto-reply di MakeMove di atas, endpoint ini opsional.
    [HttpPost("{gameId}/bot-move")]
    public IActionResult MakeBotMove(string gameId, [FromQuery] int depth = Game.DefaultBotDepth)
    {
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");
        var gameLogger = ForGame(gameId);

        if (game.GameResult.Type != GameOverType.None)
        {
            gameLogger.Warning(
                "Manual bot move rejected because game has ended. Reason: {Reason}",
                game.GameResult.Type);
            return BadRequest("Game sudah berakhir.");
        }

        if (!game.VsBot)
        {
            gameLogger.Warning("Manual bot move rejected because game mode is player versus player");
            return BadRequest("Game ini adalah mode player vs player, tidak ada Bot untuk dijalankan.");
        }

        var timeStatus = game.CheckTimeStatus();
        if (timeStatus.IsExpired)
        {
            gameLogger.Warning(
                "Manual bot move rejected because game time expired. TimedOutPlayer: {TimedOutPlayer}",
                timeStatus.TimedOutColor);
            return Conflict(new
            {
                GameOver = true,
                Reason = GameOverType.Timeout.ToString(),
                Winner = game.GameResult.Winner.ToString(),
                TimedOutPlayer = timeStatus.TimedOutColor?.ToString()
            });
        }

        // Batasi depth agar pencarian Negamax tidak membebani server.
        var safeDepth = Math.Clamp(depth, 1, 4);
        var botColor = game.CurrentPlayer.PlayerColor;
        gameLogger.Debug(
            "Manual bot move requested. RequestedDepth: {RequestedDepth}, SafeDepth: {SafeDepth}, " +
            "BotColor: {BotColor}",
            depth,
            safeDepth,
            botColor);

        try
        {
            var botMove = game.MakeBotMove(safeDepth);
            if (botMove == null)
            {
                gameLogger.Warning("Bot did not return an available move");
                return BadRequest("Bot tidak memiliki langkah yang tersedia.");
            }

            var moveInfo = new
            {
                From = new { row = botMove.Value.From.Row, botMove.Value.From.Column },
                To = new { row = botMove.Value.To.Row, botMove.Value.To.Column },
                Color = botColor.ToString()
            };

            gameLogger.Information(
                "Manual bot move completed. From: ({FromRow}, {FromColumn}), " +
                "To: ({ToRow}, {ToColumn}), Color: {BotColor}, Depth: {Depth}",
                botMove.Value.From.Row,
                botMove.Value.From.Column,
                botMove.Value.To.Row,
                botMove.Value.To.Column,
                botColor,
                safeDepth);

            if (game.GameResult.Type != GameOverType.None)
            {
                gameLogger.Information(
                    "Chess game ended after manual bot move. Reason: {Reason}, Winner: {Winner}",
                    game.GameResult.Type,
                    game.GameResult.Winner);
                return Ok(new
                {
                    GameOver = true,
                    Reason = game.GameResult.Type.ToString(),
                    Winner = game.GameResult.Winner.ToString(),
                    BotMove = moveInfo
                });
            }

            return Ok(new
            {
                GameOver = false,
                CurrentTurn = game.GetColorsPlayer().ToString(),
                BotMove = moveInfo
            });
        }
        catch (Exception exception)
        {
            gameLogger.Error(
                exception,
                "Unexpected error while processing manual bot move. Depth: {Depth}",
                safeDepth);
            return StatusCode(500, "Terjadi kesalahan saat memproses langkah bot.");
        }
    }

    // ==========================================
    // 5. TIME STATUS
    // ==========================================

    [HttpGet("{gameId}/time-status")]
    public IActionResult GetTimeStatus(string gameId)
    {
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");
        

        var status = game.CheckTimeStatus();
        ForGame(gameId).Debug(
            "Time status returned. CurrentTurn: {CurrentTurn}, TimeExpired: {TimeExpired}, " +
            "WhiteRemainingSeconds: {WhiteRemainingSeconds}, BlackRemainingSeconds: {BlackRemainingSeconds}",
            game.GetColorsPlayer(),
            status.IsExpired,
            Math.Max(0, Math.Ceiling(status.WhiteRemaining.TotalSeconds)),
            Math.Max(0, Math.Ceiling(status.BlackRemaining.TotalSeconds)));
        return Ok(new
        {
            WhiteRemainingSeconds = Math.Max(0, Math.Ceiling(status.WhiteRemaining.TotalSeconds)),
            BlackRemainingSeconds = Math.Max(0, Math.Ceiling(status.BlackRemaining.TotalSeconds)),
            CurrentTurn = game.GetColorsPlayer().ToString(),
            TimeExpired = status.IsExpired,
            TimedOutPlayer = status.TimedOutColor?.ToString(),
            GameOver = game.GameResult.Type != GameOverType.None,
            Reason = game.GameResult.Type.ToString(),
            Winner = game.GameResult.Type == GameOverType.None
                ? null
                : game.GameResult.Winner.ToString()
        });
    }

    // ==========================================
    // 6. GAME ACTIONS
    // ==========================================

    [HttpPost("{gameId}/resign")]
    public IActionResult Resign(string gameId)
    {
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var resignedPlayer = game.CurrentPlayer.PlayerColor;
        game.Resign(game.CurrentPlayer);
        
        ForGame(gameId).Information(
            "Player resigned through API. ResignedPlayer: {ResignedPlayer}, Winner: {Winner}",
            resignedPlayer,
            game.GameResult.Winner);
        return Ok(new 
        { 
            GameOver = true, 
            Reason = game.GameResult.Type.ToString(), 
            Winner = game.GameResult.Winner.ToString() 
        });
    }

    [HttpGet("{gameId}/history")]
    public ActionResult<List<MoveHistoryDTO>> GetMoveHistory(string gameId)
    {
        if (!ActiveGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var history = game.GetMoveHistory();
        var historyDto = _mapper.Map<List<MoveHistoryDTO>>(history);
        ForGame(gameId).Debug(
            "Move history returned. MoveCount: {MoveCount}",
            history.Count);
        
        return Ok(historyDto);
    }
}
