using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using ChessWeb.DTO;
using ChessConsole.Model;
using System.Collections.Generic;
using AutoMapper;
using ChessConsole;
using ChessConsole.Enums;
using ChessConsole2;

namespace ChessAPI.Controller;
[ApiController]
[Route("api/[controller]")]
public class ChessController : ControllerBase
{
    // Penyimpanan sesi game di memori (untuk production, gunakan Redis atau Database)
    private static readonly ConcurrentDictionary<string, Game> _activeGames = new();
    
    private readonly IMapper _mapper;

    public ChessController(IMapper mapper)
    {
         _mapper = mapper;
        
    }

    // ==========================================
    // 1. CREATE & GET GAME
    // ==========================================

    [HttpPost]
    public IActionResult CreateGame([FromBody] CreateGameDTO request)
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
            request.ClockEnabled);
        game.StartGame();

        var gameId = Guid.NewGuid().ToString();
        _activeGames.TryAdd(gameId, game);

        return Ok(new
        {
            GameId = gameId,
            Message = "Game started",
            VsBot = request.VsBot,
            Minutes = minutes,
            ClockEnabled = request.ClockEnabled
        });
    }

    [HttpGet("{gameId}/board")]
    public ActionResult<BoardDTO> GetBoard(string gameId)
    {
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var board = game.GetCurrentBoard();
        var boardDto = _mapper.Map<BoardDTO>(board);

        return Ok(boardDto);
    }

    // ==========================================
    // 2. MOVEMENT LOGIC
    // ==========================================

    [HttpGet("{gameId}/valid-moves")]
    public ActionResult<List<PosititionDTO>> GetValidMoves(string gameId, [FromQuery] int row, [FromQuery] int col)
    {
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        // Membentuk struct Postition asli sesuai typo pada model awal
        var position = new Postition { Row = row, Column = col };

        // Memanggil TrySelectPiece bawaan Game.cs
        if (game.TrySelectPiece(position))
        {
            var moves = game.GetAvailableMovesForSelectedPiece();
            var movesDto = _mapper.Map<List<PosititionDTO>>(moves);
            return Ok(movesDto);
        }

        return BadRequest("Tidak ada bidak milik pemain saat ini di koordinat tersebut.");
    }

    [HttpPost("{gameId}/moves")]
    public IActionResult MakeMove(string gameId, [FromBody] MoveDTO moveDto)
    {
        
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var timeStatus = game.CheckTimeStatus();
        if (timeStatus.IsExpired)
        {
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
        if (!string.IsNullOrEmpty(moveDto.PromotionType))
        {
            Enum.TryParse(moveDto.PromotionType, out promotion);
        }

        var fromPosition = new Postition { Row = moveDto.From.Row, Column = moveDto.From.Column };
        var toPosition = new Postition { Row = moveDto.To.Row, Column = moveDto.To.Column };

        // WAJIB: SelectPiece dulu sebelum MakeMove sesuai dengan alur di Program.cs Anda
        if (!game.TrySelectPiece(fromPosition))
        {
            return BadRequest("Invalid selection.");
        }

        var availableMoves = game.GetAvailableMovesForSelectedPiece();
        
        // Memastikan tujuan legal
        if (availableMoves.Exists(m => m.Row == toPosition.Row && m.Column == toPosition.Column))
        {
            var move = new Move
            {
                From = fromPosition,
                To = toPosition,
                Type = MoveType.Normal,
                PromotionType = promotion // Sesuai nama property asli Anda
            };

            game.MakeMove(move);

            // Cek kondisi akhir permainan setelah move pemain
            if (game.GameResult.Type != GameOverType.None)
            {
                return Ok(new { 
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
                var botMove = game.MakeBotMove(Game.DefaultBotDepth);
                if (botMove.HasValue)
                {
                    botMoveInfo = new
                    {
                        From = new { row = botMove.Value.From.Row, botMove.Value.From.Column },
                        To = new { row = botMove.Value.To.Row, botMove.Value.To.Column }
                    };
                }
            }

            if (game.GameResult.Type != GameOverType.None)
            {
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

        return BadRequest("Illegal move.");
    }

    // ==========================================
    // 3. ENGINE SUGGESTION
    // ==========================================

    [HttpGet("{gameId}/suggestion")]
    public ActionResult<MoveDTO> GetSuggestion(string gameId, [FromQuery] int row, [FromQuery] int col)
    {
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var position = new Postition { Row = row, Column = col };
        
        // Panggil TrySelectPiece dulu, baru GetSuggestedMoveForPiece
        if (game.TrySelectPiece(position))
        {
            var suggestedMove = game.GetSuggestedMoveForPiece(position);
            
            if (suggestedMove.HasValue)
            {
                var suggestionDto = _mapper.Map<MoveDTO>(suggestedMove.Value);
                return Ok(suggestionDto);
            }
        }

        return NotFound("Tidak ada saran langkah.");
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
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        if (game.GameResult.Type != GameOverType.None)
            return BadRequest("Game sudah berakhir.");

        if (!game.VsBot)
            return BadRequest("Game ini adalah mode player vs player, tidak ada Bot untuk dijalankan.");

        var timeStatus = game.CheckTimeStatus();
        if (timeStatus.IsExpired)
        {
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

        var botMove = game.MakeBotMove(safeDepth);
        if (botMove == null)
            return BadRequest("Bot tidak memiliki langkah yang tersedia.");

        var moveInfo = new
        {
            From = new { row = botMove.Value.From.Row, botMove.Value.From.Column },
            To = new { row = botMove.Value.To.Row, botMove.Value.To.Column },
            Color = botColor.ToString()
        };

        if (game.GameResult.Type != GameOverType.None)
        {
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

    // ==========================================
    // 5. TIME STATUS
    // ==========================================

    [HttpGet("{gameId}/time-status")]
    public IActionResult GetTimeStatus(string gameId)
    {
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var status = game.CheckTimeStatus();
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
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        game.Resign(game.CurrentPlayer);

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
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var history = game.GetMoveHistory();
        var historyDto = _mapper.Map<List<MoveHistoryDTO>>(history);
        
        return Ok(historyDto);
    }
}
