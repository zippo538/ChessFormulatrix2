using ChessConsole;
using ChessConsole.Enums;
using ChessConsole.Helper;
using ChessConsole.Interface;
using ChessConsole.Model;
using System.Diagnostics;
using Serilog;

namespace ChessConsole2;

public class Game : IGame
{
    Board _board;
    public Player? WhitePlayer;
    public Player? BlackPlayer;
    public Player CurrentPlayer = null!;
    public ChessTimer WhiteTimer = null!;
    public ChessTimer BlackTimer = null!;
    private Pieces? _selectedPiece;
    private Postition _selectedPostition;
    private List<Postition>? _availableMoves;
    private Move _lastMove;
    private readonly List<MoveHistory> _moveHistory = new();
    private readonly object _clockSync = new();
    private readonly TimeSpan _initialPlayerTime;
    private readonly bool _clockEnabled;
    private TimeSpan _whiteRemaining;
    private TimeSpan _blackRemaining;
    private DateTimeOffset _turnStartedAt;
    private bool _clockRunning;
    private readonly ILogger _logger;
    public string GameId { get; }
    public event Action? OnGameOver;
    public GameOverResult GameResult;


    public bool VsBot { get; private set; }

    public Game(
        Player? whitePlayer,
        Player? blackPlayer,
        bool vsBot = true,
        int initialMinutes = 10,
        bool clockEnabled = true,
        ILogger? logger = null,
        string? gameId = null)
    {
        GameId = string.IsNullOrWhiteSpace(gameId)
            ? Guid.NewGuid().ToString()
            : gameId;

        _logger = (logger ?? Log.Logger)
            .ForContext<Game>()
            .ForContext("GameId", GameId);

        WhitePlayer = whitePlayer;
        BlackPlayer = blackPlayer;
        VsBot = vsBot;

        _initialPlayerTime = TimeSpan.FromMinutes(
            Math.Clamp(initialMinutes, 1, 180));

        _clockEnabled = clockEnabled;
        _whiteRemaining = _initialPlayerTime;
        _blackRemaining = _initialPlayerTime;

        _board = new Board();
        InitializeBoard();

        _logger.Debug(
            "Chess game object created. WhitePlayer: {WhitePlayer}, " +
            "BlackPlayer: {BlackPlayer}, VsBot: {VsBot}, " +
            "InitialMinutes: {InitialMinutes}, ClockEnabled: {ClockEnabled}",
            WhitePlayer?.Name,
            BlackPlayer?.Name,
            VsBot,
            _initialPlayerTime.TotalMinutes,
            _clockEnabled);
    }

    private void InitializeBoard()
    {
        _board.Tiles[0 * 8 + 0].ChessPiece = new Pieces(Color.White, PieceType.Rook);
        _board.Tiles[0 * 8 + 1].ChessPiece = new Pieces(Color.White, PieceType.Knight);
        _board.Tiles[0 * 8 + 2].ChessPiece = new Pieces(Color.White, PieceType.Bishop);
        _board.Tiles[0 * 8 + 3].ChessPiece = new Pieces(Color.White, PieceType.Queen);
        _board.Tiles[0 * 8 + 4].ChessPiece = new Pieces(Color.White, PieceType.King);
        _board.Tiles[0 * 8 + 5].ChessPiece = new Pieces(Color.White, PieceType.Bishop);
        _board.Tiles[0 * 8 + 6].ChessPiece = new Pieces(Color.White, PieceType.Knight);
        _board.Tiles[0 * 8 + 7].ChessPiece = new Pieces(Color.White, PieceType.Rook);
        for (int i = 0; i < 8; i++)
            _board.Tiles[1 * 8 + i].ChessPiece = new Pieces(Color.White, PieceType.Pawn);

        for (int i = 0; i < 8; i++)
            _board.Tiles[6 * 8 + i].ChessPiece = new Pieces(Color.Black, PieceType.Pawn);
        _board.Tiles[7 * 8 + 0].ChessPiece = new Pieces(Color.Black, PieceType.Rook);
        _board.Tiles[7 * 8 + 1].ChessPiece = new Pieces(Color.Black, PieceType.Knight);
        _board.Tiles[7 * 8 + 2].ChessPiece = new Pieces(Color.Black, PieceType.Bishop);
        _board.Tiles[7 * 8 + 3].ChessPiece = new Pieces(Color.Black, PieceType.Queen);
        _board.Tiles[7 * 8 + 4].ChessPiece = new Pieces(Color.Black, PieceType.King);
        _board.Tiles[7 * 8 + 5].ChessPiece = new Pieces(Color.Black, PieceType.Bishop);
        _board.Tiles[7 * 8 + 6].ChessPiece = new Pieces(Color.Black, PieceType.Knight);
        _board.Tiles[7 * 8 + 7].ChessPiece = new Pieces(Color.Black, PieceType.Rook);
    }

    private void OnWhiteTimeOut()
    {
        DeclareTimeout(Color.White);
    }   

    private void OnBlackTimeOut()
    {
        DeclareTimeout(Color.Black);
    }

    public void StartGame()
    {
        CurrentPlayer = WhitePlayer!;
        WhiteTimer = new ChessTimer(_initialPlayerTime);
        BlackTimer = new ChessTimer(_initialPlayerTime);

        WhiteTimer.OnTimeOut += OnWhiteTimeOut;
        BlackTimer.OnTimeOut += OnBlackTimeOut;

        lock (_clockSync)
        {
            _whiteRemaining = _initialPlayerTime;
            _blackRemaining = _initialPlayerTime;
            _turnStartedAt = DateTimeOffset.UtcNow;
            _clockRunning = _clockEnabled;
        }
        _logger.Information(
            "Chess game started. WhitePlayer: {WhitePlayer}, " +
            "BlackPlayer: {BlackPlayer}, CurrentTurn: {CurrentTurn}, " +
            "VsBot: {VsBot}, ClockEnabled: {ClockEnabled}, " +
            "InitialMinutes: {InitialMinutes}",
            WhitePlayer?.Name,
            BlackPlayer?.Name,
            CurrentPlayer.PlayerColor,
            VsBot,
            _clockEnabled,
            _initialPlayerTime.TotalMinutes);
    }

    public void Resign(Player player)
    {
        GameResult = (player == WhitePlayer)
            ? new GameOverResult { Type = GameOverType.Resigned, Winner = Color.Black }
            : new GameOverResult { Type = GameOverType.Resigned, Winner = Color.White };
        _logger.Information(
            "Player resigned. ResignedPlayer: {ResignedPlayer}, " +
            "Winner: {Winner}",
            player.PlayerColor,
            GameResult.Winner);
        EndGame();
    }

    

    public bool MakeMove(Move move)
    {
        _logger.Debug(
            "Move requested by {PlayerColor}. " +
            "From: ({FromRow}, {FromColumn}), " +
            "To: ({ToRow}, {ToColumn})",
            CurrentPlayer.PlayerColor,
            move.From.Row,
            move.From.Column,
            move.To.Row,
            move.To.Column);

        if (CheckTimeStatus().IsExpired)
        {
            _logger.Warning(
                "Move rejected because game time has expired. " +
                "Player: {PlayerColor}",
                CurrentPlayer.PlayerColor);
            return false;
        }
        if (!IsInsideBoard(move.From) || !IsInsideBoard(move.To))
        {
            _logger.Warning(
                "Move rejected because coordinate is outside board. " +
                "From: ({FromRow}, {FromColumn}), " +
                "To: ({ToRow}, {ToColumn})",
                move.From.Row,
                move.From.Column,
                move.To.Row,
                move.To.Column);
            return false;
        }
        
        var legalMoves = GetLegalMoves(move.From);

        if (_selectedPiece == null ||
            _availableMoves == null ||
            move.From.Row != _selectedPostition.Row ||
            move.From.Column != _selectedPostition.Column ||
            _selectedPiece.PieceColor != CurrentPlayer.PlayerColor ||
            !legalMoves.Any(position =>
                position.Row == move.To.Row &&
                position.Column == move.To.Column)
            )
        {
            _logger.Debug(
                "Move rejected because selection or destination is illegal. " +
                "Player: {PlayerColor}, From: ({FromRow}, {FromColumn}), " +
                "To: ({ToRow}, {ToColumn})",
                CurrentPlayer.PlayerColor,
                move.From.Row,
                move.From.Column,
                move.To.Row,
                move.To.Column);
            return false;
        }

        Tile fromTile =
            _board.Tiles[move.From.Row * 8 + move.From.Column];

        Tile toTile =
            _board.Tiles[move.To.Row * 8 + move.To.Column];

        if (!ReferenceEquals(fromTile.ChessPiece, _selectedPiece))
        {
            _logger.Warning(
                "Move rejected because selected piece no longer matches board state. " +
                "From: ({FromRow}, {FromColumn})",
                move.From.Row,
                move.From.Column);
            ClearSelection();
            return false;
        }

        var movedPieceType = _selectedPiece.Piece;
        var movedPieceColor = _selectedPiece.PieceColor;
        var capturedPieceType = toTile.ChessPiece?.Piece;

        var isEnPassant =
            movedPieceType == PieceType.Pawn &&
            move.From.Column != move.To.Column &&
            toTile.ChessPiece == null;

        if (isEnPassant)
        {
            capturedPieceType = PieceType.Pawn;
            move.Type = MoveType.EnPassent;
        }

        bool isCastling =
            _selectedPiece.Piece == PieceType.King &&
            Math.Abs(move.To.Column - move.From.Column) == 2;

        if (isCastling)
            move.Type = MoveType.Castling;

        RemoveEnPassantPawn(move);
        MoveCastleRook(move);

        toTile.ChessPiece = _selectedPiece;
        fromTile.ChessPiece = null;
        _selectedPiece.HasMoved = true;

        MoveAndPromote(move);

        _lastMove = move;

        _moveHistory.Add(new MoveHistory(
            move,
            movedPieceType,
            movedPieceColor,
            capturedPieceType));
        
        _logger.Information(
            "Move completed. Player: {PlayerColor}, Piece: {PieceType}, " +
            "From: ({FromRow}, {FromColumn}), " +
            "To: ({ToRow}, {ToColumn}), MoveType: {MoveType}, " +
            "CapturedPiece: {CapturedPiece}, MoveNumber: {MoveNumber}",
            movedPieceColor,
            movedPieceType,
            move.From.Row,
            move.From.Column,
            move.To.Row,
            move.To.Column,
            move.Type,
            capturedPieceType?.ToString(),
            _moveHistory.Count);

        CheckMoveResult();

        if (GameResult.Type == GameOverType.None)
            SwitchTurn();

        ClearSelection();
        return true;
    }


    private void SwitchTurn()
    {
        
        var timeStatus = CheckTimeStatus();
        if (timeStatus.IsExpired) return;
        var previousPlayer = CurrentPlayer.PlayerColor;

        lock (_clockSync)
        {
            CurrentPlayer = CurrentPlayer == WhitePlayer
                ? BlackPlayer!
                : WhitePlayer!;
            _turnStartedAt = DateTimeOffset.UtcNow;
        }
        _logger.Debug(
            "Turn switched from {PreviousPlayer} to {CurrentPlayer}",
            previousPlayer,
            CurrentPlayer.PlayerColor);
    }

    void EndGame()
    {
        lock (_clockSync)
        {
            _clockRunning = false;
        }
        WhiteTimer.StopTimer();
        BlackTimer.StopTimer();
        object? winner = GameResult.Type == GameOverType.Stalemate
            ? null
            : GameResult.Winner.ToString();
        _logger.Information(
            "Chess game ended. Result: {GameResult}, Winner: {Winner}, " +
            "TotalMoves: {MoveCount}, WhiteRemainingSeconds: {WhiteTime}, " +
            "BlackRemainingSeconds: {BlackTime}",
            GameResult.Type,
            winner,
            _moveHistory.Count,
            Math.Max(0, _whiteRemaining.TotalSeconds),
            Math.Max(0, _blackRemaining.TotalSeconds));
        OnGameOver?.Invoke();
    }

    private void UpdateActiveClockLocked(DateTimeOffset now)
    {
        if (!_clockEnabled || !_clockRunning) return;

        var elapsed = now - _turnStartedAt;
        if (elapsed <= TimeSpan.Zero) return;

        if (CurrentPlayer == WhitePlayer)
            _whiteRemaining = _whiteRemaining > elapsed ? _whiteRemaining - elapsed : TimeSpan.Zero;
        else
            _blackRemaining = _blackRemaining > elapsed ? _blackRemaining - elapsed : TimeSpan.Zero;

        _turnStartedAt = now;
    }

    private void DeclareTimeout(Color timedOutColor)
    {
        bool shouldEndGame;
        lock (_clockSync)
        {
            if (GameResult.Type != GameOverType.None) return;

            if (timedOutColor == Color.White)
                _whiteRemaining = TimeSpan.Zero;
            else
                _blackRemaining = TimeSpan.Zero;

            _clockRunning = false;
            GameResult = new GameOverResult
            {
                Type = GameOverType.Timeout,
                Winner = timedOutColor == Color.White ? Color.Black : Color.White
            };
            shouldEndGame = true;
        }

        if (shouldEndGame)
        {
            _logger.Warning(
                "Player time expired. TimedOutPlayer: {TimedOutPlayer}, " +
                "Winner: {Winner}",
                timedOutColor,
                GameResult.Winner);
            EndGame();
        }
    }

    
    public (TimeSpan WhiteRemaining, TimeSpan BlackRemaining, bool IsExpired, Color? TimedOutColor)
        CheckTimeStatus()
    {
        Color? newlyExpiredColor = null;
        lock (_clockSync)
        {
            UpdateActiveClockLocked(DateTimeOffset.UtcNow);
            if (GameResult.Type == GameOverType.None)
            {
                if (_whiteRemaining <= TimeSpan.Zero)
                    newlyExpiredColor = Color.White;
                else if (_blackRemaining <= TimeSpan.Zero)
                    newlyExpiredColor = Color.Black;
            }
        }

        if (newlyExpiredColor.HasValue)
            DeclareTimeout(newlyExpiredColor.Value);

        lock (_clockSync)
        {
            Color? timedOutColor = null;
            if (GameResult.Type == GameOverType.Timeout)
                timedOutColor = GameResult.Winner == Color.White ? Color.Black : Color.White;

            return (
                _whiteRemaining,
                _blackRemaining,
                GameResult.Type == GameOverType.Timeout,
                timedOutColor);
        }
    }

    public void CheckMoveResult()
    {
        var opponent = (CurrentPlayer == WhitePlayer) ? BlackPlayer! : WhitePlayer!;
        var opponentColor = opponent.PlayerColor;
        var legalMoves = GetAllLegalMoves(opponentColor);
        var kingInCheck = IsKingInCheck(opponentColor);

        if (kingInCheck)
        {
            if (GetAllLegalMoves(opponentColor).Count == 0)
            {
                GameResult = new GameOverResult
                {
                    Type = GameOverType.Checkmate,
                    Winner = CurrentPlayer.PlayerColor
                };
                _logger.Information(
                    "Checkmate detected. Winner: {Winner}, " +
                    "DefeatedPlayer: {DefeatedPlayer}, MoveCount: {MoveCount}",
                    GameResult.Winner,
                    opponentColor,
                    _moveHistory.Count);
                EndGame();
            }
            _logger.Information(
                "King is in check. CheckedPlayer: {CheckedPlayer}, " +
                "AvailableLegalMoves: {LegalMoveCount}",
                opponentColor,
                legalMoves.Count);
        }
        else if (legalMoves.Count == 0)
        {
            GameResult = new GameOverResult { Type = GameOverType.Stalemate };
            _logger.Information(
                "Stalemate detected. PlayerWithoutLegalMoves: {Player}, " +
                "MoveCount: {MoveCount}",
                opponentColor,
                _moveHistory.Count);
            EndGame();
        }
    }

    void RemoveEnPassantPawn(Move move)
    {
        if (_selectedPiece?.Piece != PieceType.Pawn) return;
        if (move.To.Column != move.From.Column && _board.Tiles[move.To.Row * 8 + move.To.Column].ChessPiece == null)
        {
            int capturedRow = move.From.Row;
            _board.Tiles[capturedRow * 8 + move.To.Column].ChessPiece = null;
        }
    }

    void MoveCastleRook(Move move)
    {
        if (_selectedPiece?.Piece != PieceType.King) return;
        if (Math.Abs(move.To.Column - move.From.Column) == 2)
        {
            if (move.To.Column > move.From.Column)
            {
                var rook = _board.Tiles[move.From.Row * 8 + 7].ChessPiece;
                _board.Tiles[move.From.Row * 8 + 5].ChessPiece = rook;
                _board.Tiles[move.From.Row * 8 + 7].ChessPiece = null;
                if (rook != null) rook.HasMoved = true;
            }
            else
            {
                var rook = _board.Tiles[move.From.Row * 8 + 0].ChessPiece;
                _board.Tiles[move.From.Row * 8 + 3].ChessPiece = rook;
                _board.Tiles[move.From.Row * 8 + 0].ChessPiece = null;
                if (rook != null) rook.HasMoved = true;
            }
        }
    }

    void MoveAndPromote(Move move)
    {
        if (_selectedPiece?.Piece != PieceType.Pawn) return;
        if (move.To.Row == 0 || move.To.Row == 7)
        {
            var promotionType = NormalizePromotionType(move.PromotionType);
            var promotedPiece = new Pieces(_selectedPiece.PieceColor, promotionType);
            promotedPiece.HasMoved = true;
            _board.Tiles[move.To.Row * 8 + move.To.Column].ChessPiece = promotedPiece;
        }
    }

    private static PieceType NormalizePromotionType(PieceType promotionType) =>
        promotionType is PieceType.Queen or PieceType.Rook or PieceType.Bishop or PieceType.Knight
            ? promotionType
            : PieceType.Queen;

    void AddSlidingMoves(Pieces pieces, Postition position, List<Postition> moves)
    {
        int[][] directions = pieces.Piece switch
        {
            PieceType.Rook => new[] { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } },
            PieceType.Bishop => new[] { new[] { -1, -1 }, new[] { -1, 1 }, new[] { 1, -1 }, new[] { 1, 1 } },
            PieceType.Queen => new[] { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 }, new[] { -1, -1 }, new[] { -1, 1 }, new[] { 1, -1 }, new[] { 1, 1 } },
            _ => Array.Empty<int[]>()
        };

        foreach (var dir in directions)
        {
            for (int i = 1; i < 8; i++)
            {
                int r = position.Row + dir[0] * i;
                int c = position.Column + dir[1] * i;
                if (r < 0 || r >= 8 || c < 0 || c >= 8) break;

                var tile = _board.Tiles[r * 8 + c];
                if (tile.ChessPiece == null)
                {
                    moves.Add(new Postition { Row = r, Column = c });
                }
                else
                {
                    if (tile.ChessPiece.PieceColor != pieces.PieceColor)
                        moves.Add(new Postition { Row = r, Column = c });
                    break;
                }
            }
        }
    }

    void AddPawnMoves(Pieces pieces, Postition position, List<Postition> moves)
    {
        int dir = pieces.PieceColor == Color.White ? 1 : -1;
        int startRow = pieces.PieceColor == Color.White ? 1 : 6;

        int r1 = position.Row + dir;
        if (r1 >= 0 && r1 < 8 && _board.Tiles[r1 * 8 + position.Column].ChessPiece == null)
        {
            moves.Add(new Postition { Row = r1, Column = position.Column });
            int r2 = position.Row + dir * 2;
            if (position.Row == startRow && _board.Tiles[r2 * 8 + position.Column].ChessPiece == null)
                moves.Add(new Postition { Row = r2, Column = position.Column });
        }

        for (int dc = -1; dc <= 1; dc += 2)
        {
            int c = position.Column + dc;
            if (r1 >= 0 && r1 < 8 && c >= 0 && c < 8)
            {
                var tile = _board.Tiles[r1 * 8 + c];
                if (tile.ChessPiece != null && tile.ChessPiece.PieceColor != pieces.PieceColor)
                    moves.Add(new Postition { Row = r1, Column = c });
            }
        }
    }

    void AddFixedMoves(Pieces pieces, Postition position, List<Postition> moves)
    {
        int[][] offsets = pieces.Piece switch
        {
            PieceType.Knight => new[]
            {
                new[] { -2, -1 }, new[] { -2, 1 }, new[] { -1, -2 }, new[] { -1, 2 },
                new[] { 1, -2 }, new[] { 1, 2 }, new[] { 2, -1 }, new[] { 2, 1 }
            },
            PieceType.King => new[]
            {
                new[] { -1, -1 }, new[] { -1, 0 }, new[] { -1, 1 },
                new[] { 0, -1 }, new[] { 0, 1 },
                new[] { 1, -1 }, new[] { 1, 0 }, new[] { 1, 1 }
            },
            _ => Array.Empty<int[]>()
        };

        foreach (var offset in offsets)
        {
            int r = position.Row + offset[0];
            int c = position.Column + offset[1];
            if (r < 0 || r >= 8 || c < 0 || c >= 8) continue;

            var tile = _board.Tiles[r * 8 + c];
            if (tile.ChessPiece == null || tile.ChessPiece.PieceColor != pieces.PieceColor)
                moves.Add(new Postition { Row = r, Column = c });
        }
    }

    bool CanAttack(Postition from, Postition to, Color attackerColor)
    {
        var piece = _board.Tiles[from.Row * 8 + from.Column].ChessPiece;
        if (piece == null || piece.PieceColor != attackerColor) return false;

        if (piece.Piece == PieceType.Pawn)
        {
            int direction = attackerColor == Color.White ? 1 : -1;
            return to.Row == from.Row + direction &&
                   Math.Abs(to.Column - from.Column) == 1;
        }

        var moves = new List<Postition>();
        switch (piece.Piece)
        {
            case PieceType.Rook:
            case PieceType.Bishop:
            case PieceType.Queen:
                AddSlidingMoves(piece, from, moves);
                break;
            case PieceType.Knight:
            case PieceType.King:
                AddFixedMoves(piece, from, moves);
                break;
        }

        return moves.Any(move => move.Row == to.Row && move.Column == to.Column);
    }
    // suggestion move piece
    public Move? GetSuggestedMoveForPiece(Postition piecePosition)
    {
        if (!IsInsideBoard(piecePosition)) return null;

        var tile = _board.Tiles[piecePosition.Row * 8 + piecePosition.Column];
        if (tile.ChessPiece == null || tile.ChessPiece.PieceColor != CurrentPlayer.PlayerColor)
            return null;

        var legalMoves = GetLegalMoves(piecePosition);
        if (legalMoves.Count == 0) return null;

        Move? bestMove = null;
        int bestScore = CurrentPlayer.PlayerColor == Color.White ? int.MinValue : int.MaxValue;

        foreach (var targetPosition in legalMoves)
        {
            var candidate = new Move
            {
                From = piecePosition,
                To = targetPosition,
                Type = MoveType.Normal,
                PromotionType = PieceType.Queen
            };

            Board originalBoard = _board;
            int currentScore;
            try
            {
                _board = BoardHelper.Copy(originalBoard);
                ApplyMoveForSimulation(candidate);
                currentScore = EvaluateBoard(_board);
            }
            finally
            {
                _board = originalBoard;
            }

            bool isBetter = CurrentPlayer.PlayerColor == Color.White
                ? currentScore > bestScore
                : currentScore < bestScore;
            if (!isBetter) continue;

            bestScore = currentScore;
            bestMove = candidate;
        }

        return bestMove;
    }

    bool IsKingInCheck(Color kingColor)
    {
        var kingPosition = GetKingPosition(kingColor);
        return !IsInsideBoard(kingPosition) || IsSquareAttacked(kingPosition, kingColor);
    }

    private List<Postition> GetPseudoLegalMoves(Postition position, bool includeSpecialMoves = true)
    {
        var moves = new List<Postition>();
        if (!IsInsideBoard(position)) return moves;

        var piece = _board.Tiles[position.Row * 8 + position.Column].ChessPiece;
        if (piece == null) return moves;

        switch (piece.Piece)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, position, moves);
                if (includeSpecialMoves) moves.AddRange(GetEnPassantMoves(position));
                break;
            case PieceType.Rook:
            case PieceType.Bishop:
            case PieceType.Queen:
                AddSlidingMoves(piece, position, moves);
                break;
            case PieceType.Knight:
                AddFixedMoves(piece, position, moves);
                break;
            case PieceType.King:
                AddFixedMoves(piece, position, moves);
                if (includeSpecialMoves) moves.AddRange(GetCastleMoves(position, piece));
                break;
        }

        return moves;
    }

    
    List<Postition> GetLegalMoves(Postition position)
    {
        if (!IsInsideBoard(position)) return new List<Postition>();

        var piece = _board.Tiles[position.Row * 8 + position.Column].ChessPiece;
        if (piece == null) return new List<Postition>();

        return GetPseudoLegalMoves(position)
            .Where(target =>
            {
                var targetPiece = _board.Tiles[target.Row * 8 + target.Column].ChessPiece;
                return targetPiece?.Piece != PieceType.King &&
                       IsMoveLegal(position, target, piece.PieceColor);
            })
            .GroupBy(target => (target.Row, target.Column))
            .Select(group => group.First())
            .ToList();
    }

    private bool IsMoveLegal(Postition from, Postition to, Color movingColor)
    {
        Board originalBoard = _board;

        try
        {
            _board = BoardHelper.Copy(originalBoard);

            ApplyMoveForSimulation(new Move
            {
                From = from,
                To = to,
                Type = MoveType.Normal,
                PromotionType = PieceType.Queen
            });

            return !IsKingInCheck(movingColor);
        }
        finally
        {
            _board = originalBoard;
        }
    }

    List<Postition> GetAllLegalMoves(Color color)
    {
        return GetAllLegalMoveObjects(color).Select(move => move.To).ToList();
    }

    private List<Move> GetAllLegalMoveObjects(Color color)
    {
        var moves = new List<Move>();
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                var from = new Postition { Row = row, Column = column };
                var piece = _board.Tiles[row * 8 + column].ChessPiece;
                if (piece == null || piece.PieceColor != color) continue;

                foreach (var to in GetLegalMoves(from))
                {
                    moves.Add(new Move
                    {
                        From = from,
                        To = to,
                        Type = piece.Piece == PieceType.King && Math.Abs(to.Column - column) == 2
                            ? MoveType.Castling
                            : piece.Piece == PieceType.Pawn && to.Column != column &&
                              _board.Tiles[to.Row * 8 + to.Column].ChessPiece == null
                                ? MoveType.EnPassent
                                : MoveType.Normal,
                        PromotionType = piece.Piece == PieceType.Pawn && (to.Row == 0 || to.Row == 7)
                            ? PieceType.Queen
                            : PieceType.None
                    });
                }
            }
        }
        return moves;
    }

    private static Color OppositeColor(Color color) =>
        color == Color.White ? Color.Black : Color.White;

    private void ApplyMoveForSimulation(Move move)
    {
        var fromTile = _board.Tiles[move.From.Row * 8 + move.From.Column];
        var toTile = _board.Tiles[move.To.Row * 8 + move.To.Column];
        var piece = fromTile.ChessPiece;
        if (piece == null) return;

        bool isEnPassant = piece.Piece == PieceType.Pawn &&
                           move.From.Column != move.To.Column &&
                           toTile.ChessPiece == null;
        if (isEnPassant)
            _board.Tiles[move.From.Row * 8 + move.To.Column].ChessPiece = null;

        bool isCastling = piece.Piece == PieceType.King &&
                          Math.Abs(move.To.Column - move.From.Column) == 2;
        if (isCastling)
        {
            int rookFromColumn = move.To.Column > move.From.Column ? 7 : 0;
            int rookToColumn = move.To.Column > move.From.Column ? 5 : 3;
            var rook = _board.Tiles[move.From.Row * 8 + rookFromColumn].ChessPiece;
            _board.Tiles[move.From.Row * 8 + rookToColumn].ChessPiece = rook;
            _board.Tiles[move.From.Row * 8 + rookFromColumn].ChessPiece = null;
            if (rook != null) rook.HasMoved = true;
        }

        toTile.ChessPiece = piece;
        fromTile.ChessPiece = null;
        piece.HasMoved = true;

        if (piece.Piece == PieceType.Pawn && (move.To.Row == 0 || move.To.Row == 7))
        {
            var promotedPiece = new Pieces(piece.PieceColor, NormalizePromotionType(move.PromotionType));
            promotedPiece.HasMoved = true;
            toTile.ChessPiece = promotedPiece;
        }
    }

    List<Postition> GetCastleMoves(Postition kingPos, Pieces king)
    {
        var moves = new List<Postition>();
        int homeRow = king.PieceColor == Color.White ? 0 : 7;
        if (king.HasMoved || kingPos.Row != homeRow || kingPos.Column != 4 ||
            IsKingInCheck(king.PieceColor)) return moves;
        
        
        int row = kingPos.Row;
        var kingSideRook = _board.Tiles[row * 8 + 7].ChessPiece;
        if (kingSideRook != null && kingSideRook.Piece == PieceType.Rook &&
            kingSideRook.PieceColor == king.PieceColor && !kingSideRook.HasMoved)
        {
            if (_board.Tiles[row * 8 + 5].ChessPiece == null &&
                _board.Tiles[row * 8 + 6].ChessPiece == null)
            {
                if (!IsSquareAttacked(new Postition { Row = row, Column = 5 }, king.PieceColor) &&
                    !IsSquareAttacked(new Postition { Row = row, Column = 6 }, king.PieceColor))
                {
                    moves.Add(new Postition { Row = row, Column = 6 });
                }
            }
        }
        var queenSideRook = _board.Tiles[row * 8 + 0].ChessPiece;
        if (queenSideRook != null && queenSideRook.Piece == PieceType.Rook &&
            queenSideRook.PieceColor == king.PieceColor && !queenSideRook.HasMoved)
        {
            if (_board.Tiles[row * 8 + 1].ChessPiece == null &&
                _board.Tiles[row * 8 + 2].ChessPiece == null &&
                _board.Tiles[row * 8 + 3].ChessPiece == null)
            {
                if (!IsSquareAttacked(new Postition { Row = row, Column = 2 }, king.PieceColor) &&
                    !IsSquareAttacked(new Postition { Row = row, Column = 3 }, king.PieceColor))
                {
                    moves.Add(new Postition { Row = row, Column = 2 });
                }
            }
        }
        return moves;
    }

    private bool IsSquareAttacked(Postition targetPos, Color defendingColor)
    {
        var attackerColor = (defendingColor == Color.White) ? Color.Black : Color.White;
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[r * 8 + c].ChessPiece;
                if (piece != null && piece.PieceColor == attackerColor)
                {
                    if (piece.Piece == PieceType.Pawn)
                    {
                        int dir = (attackerColor == Color.White) ? 1 : -1;
                        if (r + dir == targetPos.Row && (c - 1 == targetPos.Column || c + 1 == targetPos.Column))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (CanAttack(new Postition { Row = r, Column = c }, targetPos, attackerColor))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    List<Postition> GetEnPassantMoves(Postition pawnPos)
    {
        var moves = new List<Postition>();
        if (_lastMove.Equals(default(Move))) return moves;

        var pawn = _board.Tiles[pawnPos.Row * 8 + pawnPos.Column].ChessPiece;
        if (pawn == null || pawn.Piece != PieceType.Pawn) return moves;

        int requiredPawnRow = pawn.PieceColor == Color.White ? 4 : 3;
        if (pawnPos.Row != requiredPawnRow) return moves;

        int lastMoveRowDiff = Math.Abs(_lastMove.To.Row - _lastMove.From.Row);

        if (lastMoveRowDiff == 2)
        {
            var lastMovedPiece = _board.Tiles[_lastMove.To.Row * 8 + _lastMove.To.Column].ChessPiece;

            if (lastMovedPiece != null &&
                lastMovedPiece.Piece == PieceType.Pawn &&
                lastMovedPiece.PieceColor != pawn.PieceColor &&
                _lastMove.From.Column == _lastMove.To.Column &&
                _lastMove.From.Row == (lastMovedPiece.PieceColor == Color.White ? 1 : 6))
            {
                if (_lastMove.To.Row == pawnPos.Row &&
                    Math.Abs(_lastMove.To.Column - pawnPos.Column) == 1)
                {
                    int captureRow = pawn.PieceColor == Color.White ? pawnPos.Row + 1 : pawnPos.Row - 1;
                    int captureCol = _lastMove.To.Column;

                    if (captureRow >= 0 && captureRow < 8 && captureCol >= 0 && captureCol < 8 &&
                        _board.Tiles[captureRow * 8 + captureCol].ChessPiece == null)
                    {
                        moves.Add(new Postition { Row = captureRow, Column = captureCol });
                    }
                }
            }
        }

        return moves;
    }


    public MoveResult GetMoveResult()
    {
        var opponent = (CurrentPlayer == WhitePlayer) ? BlackPlayer : WhitePlayer;
        if (IsKingInCheck(opponent!.PlayerColor))
        {
            if (GetAllLegalMoves(opponent.PlayerColor).Count == 0)
                return MoveResult.Checkmate;
            return MoveResult.Checked;
        }
        if (GetAllLegalMoves(opponent.PlayerColor).Count == 0)
            return MoveResult.Stalemate;
        return MoveResult.Normal;
    }

    public Color GetColorsPlayer() => CurrentPlayer.PlayerColor;

    Postition GetKingPosition(Color kingColor)
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[r * 8 + c].ChessPiece;
                if (piece != null && piece.Piece == PieceType.King && piece.PieceColor == kingColor)
                {
                    return new Postition { Row = r, Column = c };
                }
            }
        }
        return new Postition { Row = -1, Column = -1 };
    }

    public Board GetCurrentBoard() { return _board; }
    
    public void SelectPiece(Postition position)
    {
        ClearSelection();

        if (!IsInsideBoard(position)) return;

        var tile = _board.Tiles[position.Row * 8 + position.Column];
        if (tile.ChessPiece != null && tile.ChessPiece.PieceColor == CurrentPlayer.PlayerColor)
        {
            _selectedPiece = tile.ChessPiece;
            _selectedPostition = position;
            _availableMoves = GetLegalMoves(position);
        }
    }
    // move history
    public IReadOnlyList<MoveHistory> GetMoveHistory() => _moveHistory.AsReadOnly();
    
    public List<Postition> GetAvailableMovesForSelectedPiece()
    {
        return _availableMoves ?? new List<Postition>();
    }
    public bool TrySelectPiece(Postition position)
    {
        SelectPiece(position);
        return _selectedPiece != null;
    }
    
    public void SelectPieceAndGetSuggestion(Postition postition)
    {
        TrySelectPiece(postition); 
    
        if (_selectedPiece != null)
        {
            Move? suggestedMove = GetSuggestedMoveForPiece(_selectedPostition);
            
            if (suggestedMove.HasValue)
            {
                Console.WriteLine($"Saran Langkah: Ke baris {suggestedMove.Value.To.Row}, kolom {suggestedMove.Value.To.Column}");
            }
        }
    }
    private static bool IsInsideBoard(Postition position) =>  position.Row >= 0 && 
                                                              position.Row < 8 && position.Column >= 0 && position.Column < 8;

    private void ClearSelection()
    {
        _selectedPiece = null;
        _availableMoves = null;
        _selectedPostition = new Postition { Row = -1, Column = -1 };
    }
    private int EvaluateBoard(Board board)
    {
        int score = 0;
        for (int i = 0; i < 64; i++)
        {
            var piece = board.Tiles[i].ChessPiece;
            if (piece != null)
            {
                int pieceValue = piece.Piece switch
                {
                    PieceType.Pawn => 10,
                    PieceType.Knight => 30,
                    PieceType.Bishop => 30,
                    PieceType.Rook => 50,
                    PieceType.Queen => 90,
                    PieceType.King => 900,
                    _ => 0
                };
    
                score += (piece.PieceColor == Color.White) ? pieceValue : -pieceValue;
            }
        }
        return score;
    }


    public const int DefaultBotDepth = 3;

    private int Negamax(Color colorToMove, int depth, int alpha, int beta)
    {
        var legalMoves = GetAllLegalMoveObjects(colorToMove);
        if (legalMoves.Count == 0)
            return IsKingInCheck(colorToMove) ? -100000 - depth : 0;

        if (depth <= 0)
        {
            int perspective = colorToMove == Color.White ? 1 : -1;
            return perspective * EvaluateBoard(_board);
        }

        int best = int.MinValue;
        foreach (var move in legalMoves)
        {
            Board originalBoard = _board;
            Move originalLastMove = _lastMove;
            int score;
            try
            {
                _board = BoardHelper.Copy(originalBoard);
                ApplyMoveForSimulation(move);
                _lastMove = move;
                score = -Negamax(OppositeColor(colorToMove), depth - 1, -beta, -alpha);
            }
            finally
            {
                _board = originalBoard;
                _lastMove = originalLastMove;
            }

            if (score > best) best = score;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }

        return best;
    }

    public Move? GetBestMove(Color color, int depth = DefaultBotDepth)
    {
        depth = Math.Clamp(depth, 1, 4);
        var legalMoves = GetAllLegalMoveObjects(color);
        if (legalMoves.Count == 0) return null;

        Move? bestMove = null;
        int bestScore = int.MinValue;
        int alpha = int.MinValue;
        int beta = int.MaxValue;

        foreach (var move in legalMoves)
        {
            Board originalBoard = _board;
            Move originalLastMove = _lastMove;
            int score;
            try
            {
                _board = BoardHelper.Copy(originalBoard);
                ApplyMoveForSimulation(move);
                _lastMove = move;
                score = -Negamax(OppositeColor(color), depth - 1, -beta, -alpha);
            }
            finally
            {
                _board = originalBoard;
                _lastMove = originalLastMove;
            }

            if (!bestMove.HasValue || score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
            if (score > alpha) alpha = score;
        }

        return bestMove;
    }

    public Move? MakeBotMove(int depth = DefaultBotDepth)
    {
        if (CheckTimeStatus().IsExpired)
        {
            _logger.Warning(
                "Bot move cancelled because game time has expired.");
            return null;
        }

        var color = CurrentPlayer.PlayerColor;
        var safeDepth = Math.Clamp(depth, 1, 4);
        var bestMove = GetBestMove(color, depth);
        var stopwatch = Stopwatch.StartNew();
        
        _logger.Information(
            "Bot search started. Color: {BotColor}, Depth: {Depth}",
            color,
            safeDepth);
        if (bestMove == null)
        {
            _logger.Warning(
                "Bot search completed without available move. " +
                "Color: {BotColor}, Depth: {Depth}, ElapsedMs: {ElapsedMs}",
                color,
                safeDepth,
                stopwatch.ElapsedMilliseconds);
            return null;
        }

        if (CheckTimeStatus().IsExpired)
        {
            _logger.Warning(
                "Bot move cancelled because time expired during search. " +
                "ElapsedMs: {ElapsedMs}",
                stopwatch.ElapsedMilliseconds);
            return null;
        }

        if (!TrySelectPiece(bestMove.Value.From))
        {
            _logger.Error(
                "Bot Cant Select Piece. " +
                "ElapsedMs: {ElapsedMs}",
                stopwatch.ElapsedMilliseconds);
            return null;
        }

        var success = MakeMove(bestMove.Value);
        _logger.Information(
            "Bot Success Select Piece " +
            "ElapsedMs: {ElapsedMs}",
            stopwatch.ElapsedMilliseconds);
        return success ? bestMove : null;
    }
}
