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
    public IActionResult CreateGame([FromBody] PlayerDTO request)
    {
        // Membuat instance game menggunakan model asli dari ChessConsole
        var whitePlayer = new Player(request.Name, Color.White);
        var blackPlayer = new Player("AI Bot", Color.Black); // Karena single player
        
        var game = new Game(whitePlayer, blackPlayer);
        game.StartGame();

        var gameId = Guid.NewGuid().ToString();
        _activeGames.TryAdd(gameId, game);

        return Ok(new { GameId = gameId, Message = "Game started" });
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

            // Cek kondisi akhir permainan setelah move
            if (game.GameResult.Type != GameOverType.None)
            {
                return Ok(new { 
                    GameOver = true, 
                    Reason = game.GameResult.Type.ToString(), 
                    Winner = game.GameResult.Winner.ToString() 
                });
            }

            return Ok(new { GameOver = false, CurrentTurn = game.GetColorsPlayer().ToString() });
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
    // 4. GAME ACTIONS
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