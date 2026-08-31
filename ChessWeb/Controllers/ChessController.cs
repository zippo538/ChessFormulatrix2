using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using ChessWeb.DTO;
using ChessConsole.Model;
using AutoMapper;
using ChessAPI.Profiles.Hubs;
using ChessConsole;
using ChessConsole.Enums;
using Microsoft.AspNetCore.SignalR;

namespace ChessAPI.Controller;
[ApiController]
[Route("api/[controller]")]
public class ChessController : ControllerBase
{
    // Penyimpanan sesi game di memori (untuk production, gunakan Redis atau Database)
    private static readonly ConcurrentDictionary<string, Game> _activeGames = new();
    
    private readonly IMapper _mapper;
    private readonly IHubContext<ChessHub> _hubContext;

    public ChessController(IMapper mapper, IHubContext<ChessHub> hubContext)
    {
         _mapper = mapper;
         _hubContext = hubContext;
    }

    // ==========================================
    // 1. CREATE & GET GAME
    // ==========================================

    [HttpPost]
    public IActionResult CreateGame(
        [FromBody] List<PlayerDTO> players,
        [FromQuery] int minutes = 10)
    {
        if (players is null || players.Count != 2)
        {
            return BadRequest(
                "Game harus memiliki tepat dua pemain."
            );
        }

        var whiteRequest = players.SingleOrDefault(
            player => player.PlayerColor == Color.White
        );

        var blackRequest = players.SingleOrDefault(
            player => player.PlayerColor == Color.Black
        );

        if (whiteRequest is null || blackRequest is null)
        {
            return BadRequest(
                "Harus terdapat satu pemain White dan satu pemain Black."
            );
        }

        if (string.IsNullOrWhiteSpace(whiteRequest.Name) ||
            string.IsNullOrWhiteSpace(blackRequest.Name))
        {
            return BadRequest("Nama kedua pemain wajib diisi.");
        }

        var whitePlayer = new Player(
            whiteRequest.Name.Trim(),
            Color.White
        );

        var blackPlayer = new Player(
            blackRequest.Name.Trim(),
            Color.Black
        );

        var game = new Game(
            whitePlayer,
            blackPlayer
        );

        game.StartGame();

        var gameId = Guid.NewGuid().ToString();

        if (!_activeGames.TryAdd(gameId, game))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Gagal membuat sesi game."
            );
        }

        return Ok(new
        {
            GameId = gameId,
            WhitePlayer = whitePlayer.Name,
            BlackPlayer = blackPlayer.Name,
            CurrentTurn = game.GetColorsPlayer().ToString(),
            Message = "Player vs Player game started"
        });
    }

    [HttpGet("{gameId}/board")]
    public IActionResult GetBoard(string gameId)
    {
        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var board = game.GetCurrentBoard();

        var modelPiece = board.Tiles[0].ChessPiece;
        var boardDto = _mapper.Map<BoardDTO>(board);
        var dtoPiece = boardDto.Tiles.First().ChessPiece;

        return Ok(new
        {
            EnumWhiteValue = (int)Color.White,
            EnumBlackValue = (int)Color.Black,

            ModelColor = modelPiece?.PieceColor.ToString(),
            ModelColorValue = modelPiece is null
                ? (int?)null
                : (int)modelPiece.PieceColor,

            DtoColor = dtoPiece?.PieceColor.ToString(),
            DtoColorValue = dtoPiece is null
                ? (string?)null
                : dtoPiece.PieceColor,

            Board = boardDto
        });
    }

    // ==========================================
    // 2. MOVEMENT LOGIC
    // ==========================================

    [HttpGet("{gameId}/valid-moves")]
    public ActionResult<List<PosititionDTO>> GetValidMoves(string gameId, [FromQuery] int row, [FromQuery] int col)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return BadRequest("GameId wajib diisi.");

        if (!IsValidPosition(row, col))
            return BadRequest("Row dan col harus berada pada rentang 0 sampai 7.");

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
    public async Task<IActionResult> MakeMove(string gameId, [FromBody] MoveDTO moveDto)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return BadRequest("GameId wajib diisi.");

        if (moveDto is null)
            return BadRequest("Request langkah tidak boleh kosong.");

        if (moveDto.From is null || moveDto.To is null)
            return BadRequest("Posisi From dan To wajib diisi.");

        if (!IsValidPosition(moveDto.From.Row, moveDto.From.Column) ||
            !IsValidPosition(moveDto.To.Row, moveDto.To.Column))
        {
            return BadRequest("Koordinat From dan To harus berada pada rentang 0 sampai 7.");
        }

        if (moveDto.From.Row == moveDto.To.Row && moveDto.From.Column == moveDto.To.Column)
            return BadRequest("Posisi tujuan harus berbeda dari posisi asal.");

        if (moveDto.PromotionType.HasValue && !IsValidPromotion(moveDto.PromotionType.Value))
            return BadRequest("PromotionType hanya boleh Queen, Rook, Bishop, atau Knight.");

        if (moveDto.Type.HasValue && !Enum.IsDefined(typeof(MoveType), moveDto.Type.Value))
            return BadRequest("Type langkah tidak valid.");

        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

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
            var move = _mapper.Map<Move>(moveDto);
            if (!game.MakeMove(move))
            {
                return BadRequest(new
                {
                    message = "Langkah ditolak oleh game.",
                    from = moveDto.From,
                    to = moveDto.To
                });
            }

            var boardDto = _mapper.Map<BoardDTO>(game.GetCurrentBoard());
            await _hubContext.Clients
                .Group($"game:{gameId}")
                .SendAsync("BoardUpdated", new
                {
                    GameId = gameId,
                    Board = boardDto,
                    CurrentTurn = game.GetColorsPlayer().ToString(),
                    LastMove = moveDto
                });

            // Cek kondisi akhir permainan setelah move
            if (game.GameResult.Type != GameOverType.None)
            {
                await _hubContext.Clients
                    .Group($"game:{gameId}")
                    .SendAsync("GameOver", new
                    {
                        Reason = game.GameResult.Type,
                        Winner = game.GameResult.Winner
                    });

                return Ok(new
                {
                    GameOver = true,
                    Reason = game.GameResult.Type,
                    Winner = game.GameResult.Winner
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
        if (string.IsNullOrWhiteSpace(gameId))
            return BadRequest("GameId wajib diisi.");

        if (!IsValidPosition(row, col))
            return BadRequest("Row dan col harus berada pada rentang 0 sampai 7.");

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
    // 4. GAME ACTIONS
    // ==========================================

    [HttpPost("{gameId}/resign")]
    public IActionResult Resign(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return BadRequest("GameId wajib diisi.");

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
        if (string.IsNullOrWhiteSpace(gameId))
            return BadRequest("GameId wajib diisi.");

        if (!_activeGames.TryGetValue(gameId, out var game))
            return NotFound("Game session not found.");

        var history = game.GetMoveHistory();
        var historyDto = _mapper.Map<List<MoveHistoryDTO>>(history);
        
        return Ok(historyDto);
    }

    
    
    private static bool IsValidPosition(int row, int column) =>
        row is >= 0 and < 8 &&
        column is >= 0 and < 8;
    private static bool IsValidPromotion(PieceType pieceType)
    {
        return pieceType is
            PieceType.Queen or
            PieceType.Rook or
            PieceType.Bishop or
            PieceType.Knight;
    }
}