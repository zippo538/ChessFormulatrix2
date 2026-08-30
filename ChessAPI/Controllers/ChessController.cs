using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using ChessWeb.DTO;
using ChessConsole.Model;
using System.Collections.Generic;
using AutoMapper;
using ChessConsole;
using ChessConsole.Enums;

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
        if (string.IsNullOrWhiteSpace(request.WhiteName))
            return BadRequest("Nama pemain Putih wajib diisi.");

        if (!request.VsBot && string.IsNullOrWhiteSpace(request.BlackName))
            return BadRequest("Nama pemain Hitam wajib diisi untuk mode player vs player.");

        // Membuat instance game menggunakan model asli dari ChessConsole
        var whitePlayer = new Player(request.WhiteName, Color.White);
        var blackName = request.VsBot ? "AI Bot" : request.BlackName!;
        var blackPlayer = new Player(blackName, Color.Black);

        var game = new Game(whitePlayer, blackPlayer, request.VsBot);
        game.StartGame();

        var gameId = Guid.NewGuid().ToString();
        _activeGames.TryAdd(gameId, game);

        return Ok(new { GameId = gameId, Message = "Game started", VsBot = request.VsBot });
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
        var position = new Postition { row = row, Column = col };

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

        // Validasi dan parsing PromotionType
        PieceType promotion = PieceType.Queen;
        if (!string.IsNullOrEmpty(moveDto.PromotionType))
        {
            Enum.TryParse(moveDto.PromotionType, out promotion);
        }

        var fromPosition = new Postition { row = moveDto.From.Row, Column = moveDto.From.Column };
        var toPosition = new Postition { row = moveDto.To.Row, Column = moveDto.To.Column };

        // WAJIB: SelectPiece dulu sebelum MakeMove sesuai dengan alur di Program.cs Anda
        if (!game.TrySelectPiece(fromPosition))
        {
            return BadRequest("Invalid selection.");
        }

        var availableMoves = game.GetAvailableMovesForSelectedPiece();
        
        // Memastikan tujuan legal
        if (availableMoves.Exists(m => m.row == toPosition.row && m.Column == toPosition.Column))
        {
            var move = new Move
            {
                From = fromPosition,
                To = toPosition,
                Type = MoveType.Normal,
                PromptionType = promotion // Sesuai nama property asli Anda
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

            // Mode vs Bot: giliran Hitam otomatis dibalas oleh Bot memakai
            // engine Minimax + Alpha-Beta di Game.cs (lihat GetBestMove/MakeBotMove).
            // Mode PvP (VsBot = false): dilewati, giliran Hitam menunggu input pemain.
            object? botMoveInfo = null;
            if (game.VsBot && game.GetColorsPlayer() == Color.Black)
            {
                var botMove = game.MakeBotMove(Game.DefaultBotDepth);
                if (botMove.HasValue)
                {
                    botMoveInfo = new
                    {
                        From = new { botMove.Value.From.row, botMove.Value.From.Column },
                        To = new { botMove.Value.To.row, botMove.Value.To.Column }
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

        var position = new Postition { row = row, Column = col };
        
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

        // Batasi depth agar pencarian Minimax tidak membebani server secara berlebihan
        var safeDepth = Math.Clamp(depth, 1, 4);
        var botColor = game.CurrentPlayer.PlayerColor;

        var botMove = game.MakeBotMove(safeDepth);
        if (botMove == null)
            return BadRequest("Bot tidak memiliki langkah yang tersedia.");

        var moveInfo = new
        {
            From = new { botMove.Value.From.row, botMove.Value.From.Column },
            To = new { botMove.Value.To.row, botMove.Value.To.Column },
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
    // 5. GAME ACTIONS
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