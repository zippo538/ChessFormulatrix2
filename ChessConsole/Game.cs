using ChessConsole.Enums;
using ChessConsole.Helper;
using ChessConsole.Interface;
using ChessConsole.Model;
using System.Text;
using ChessRules = ChessDotNet;

namespace ChessConsole;

public class Game : IGame
{
    Board _board;
    public Player? WhitePlayer;
    public Player? BlackPlayer;
    public Player CurrentPlayer = null!;
    public ChessTimer WhiteTimer = null!;
    public ChessTimer BlackTimer = null!;
    private Piece? _selectedPiece;
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
    public event Action? OnGameOver;
    public GameOverResult GameResult;

    // true jika Hitam dikendalikan oleh Negamax + Alpha-Beta internal.
    // Validasi aturan dan simulasi posisi bot memakai ChessDotNet 1.0.0.
    // false untuk player vs player (kedua sisi manusia). Default true agar
    // pemakaian lama (mis. Program.cs versi single-player) tetap berjalan.
    public bool VsBot { get; private set; }

    public Game(
        Player whitePlayer,
        Player blackPlayer,
        bool vsBot = true,
        int initialMinutes = 10,
        bool clockEnabled = true)
    {
        WhitePlayer = whitePlayer;
        BlackPlayer = blackPlayer;
        VsBot = vsBot;
        _initialPlayerTime = TimeSpan.FromMinutes(Math.Clamp(initialMinutes, 1, 180));
        _clockEnabled = clockEnabled;
        _whiteRemaining = _initialPlayerTime;
        _blackRemaining = _initialPlayerTime;
        _board = new Board();
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        // White pieces (row 0-1)
        _board.Tiles[0 * 8 + 0].ChessPiece = new Piece(Color.White, PieceType.Rook);
        _board.Tiles[0 * 8 + 1].ChessPiece = new Piece(Color.White, PieceType.Knight);
        _board.Tiles[0 * 8 + 2].ChessPiece = new Piece(Color.White, PieceType.Bishop);
        _board.Tiles[0 * 8 + 3].ChessPiece = new Piece(Color.White, PieceType.Queen);
        _board.Tiles[0 * 8 + 4].ChessPiece = new Piece(Color.White, PieceType.King);
        _board.Tiles[0 * 8 + 5].ChessPiece = new Piece(Color.White, PieceType.Bishop);
        _board.Tiles[0 * 8 + 6].ChessPiece = new Piece(Color.White, PieceType.Knight);
        _board.Tiles[0 * 8 + 7].ChessPiece = new Piece(Color.White, PieceType.Rook);
        for (int i = 0; i < 8; i++)
            _board.Tiles[1 * 8 + i].ChessPiece = new Piece(Color.White, PieceType.Pawn);

        // Black pieces (row 6-7)
        for (int i = 0; i < 8; i++)
            _board.Tiles[6 * 8 + i].ChessPiece = new Piece(Color.Black, PieceType.Pawn);
        _board.Tiles[7 * 8 + 0].ChessPiece = new Piece(Color.Black, PieceType.Rook);
        _board.Tiles[7 * 8 + 1].ChessPiece = new Piece(Color.Black, PieceType.Knight);
        _board.Tiles[7 * 8 + 2].ChessPiece = new Piece(Color.Black, PieceType.Bishop);
        _board.Tiles[7 * 8 + 3].ChessPiece = new Piece(Color.Black, PieceType.Queen);
        _board.Tiles[7 * 8 + 4].ChessPiece = new Piece(Color.Black, PieceType.King);
        _board.Tiles[7 * 8 + 5].ChessPiece = new Piece(Color.Black, PieceType.Bishop);
        _board.Tiles[7 * 8 + 6].ChessPiece = new Piece(Color.Black, PieceType.Knight);
        _board.Tiles[7 * 8 + 7].ChessPiece = new Piece(Color.Black, PieceType.Rook);
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
    }

    public void Resign(Player player)
    {
        GameResult = (player == WhitePlayer)
            ? new GameOverResult { Type = GameOverType.Resigned, Winner = Color.Black }
            : new GameOverResult { Type = GameOverType.Resigned, Winner = Color.White };
        EndGame(GameResult);
    }

    

    public bool MakeMove(Move move)
    {
        if (CheckTimeStatus().IsExpired)
            return false;

        if (_selectedPiece == null ||
            _availableMoves == null ||
            !IsInsideBoard(move.From) ||
            !IsInsideBoard(move.To) ||
            move.From.Row != _selectedPostition.Row ||
            move.From.Column != _selectedPostition.Column ||
            _selectedPiece.PieceColor != CurrentPlayer.PlayerColor ||
            !_availableMoves.Any(position =>
                position.Row == move.To.Row &&
                position.Column == move.To.Column)
            )
        {
            return false;
        }

        Tile fromTile =
            _board.Tiles[move.From.Row * 8 + move.From.Column];

        Tile toTile =
            _board.Tiles[move.To.Row * 8 + move.To.Column];

        if (!ReferenceEquals(fromTile.ChessPiece, _selectedPiece))
        {
            ClearSelection();
            return false;
        }

        var movedPieceType = _selectedPiece.Type;
        var movedPieceColor = _selectedPiece.PieceColor;
        var capturedPieceType = toTile.ChessPiece?.Type;

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
            _selectedPiece.Type == PieceType.King &&
            Math.Abs(move.To.Column - move.From.Column) == 2;

        if (isCastling)
            move.Type = MoveType.Castling;

        RemoveEnPassantPawn(move);
        MoveCastleRook(move);

        toTile.ChessPiece = _selectedPiece;
        fromTile.ChessPiece = null;
        _selectedPiece.Hasmove = true;

        MoveAndPromote(move);

        _lastMove = move;

        _moveHistory.Add(new MoveHistory(
            move,
            movedPieceType,
            movedPieceColor,
            capturedPieceType));

        CheckMoveResult();

        if (GameResult.Type == GameOverType.None)
            SwitchTurn();

        ClearSelection();
        return true;
    }


    private void SwitchTurn()
    {
        // Catat waktu pemain aktif sebelum mengganti giliran. Jika ternyata
        // sudah habis, permainan diakhiri dan giliran tidak dipindahkan.
        var timeStatus = CheckTimeStatus();
        if (timeStatus.IsExpired) return;

        lock (_clockSync)
        {
            CurrentPlayer = CurrentPlayer == WhitePlayer
                ? BlackPlayer!
                : WhitePlayer!;
            _turnStartedAt = DateTimeOffset.UtcNow;
        }
    }

    void EndGame(GameOverResult result)
    {
        lock (_clockSync)
        {
            _clockRunning = false;
        }
        WhiteTimer.StopTimer();
        BlackTimer.StopTimer();
        OnGameOver?.Invoke();
    }

    private void UpdateActiveClockLocked(DateTimeOffset now)
    {
        if (!_clockEnabled || !_clockRunning || CurrentPlayer == null) return;

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
        bool shouldEndGame = false;
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

        if (shouldEndGame) EndGame(GameResult);
    }

    /// <summary>
    /// Menghitung sisa waktu berdasarkan jam UTC server dan sekaligus
    /// menetapkan hasil timeout bila waktu pemain aktif sudah habis.
    /// </summary>
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

    void CheckMoveResult()
    {
        var opponent = (CurrentPlayer == WhitePlayer) ? BlackPlayer! : WhitePlayer!;
        var opponentColor = opponent.PlayerColor;

        if (IsKingInCheck(opponentColor))
        {
            if (GetAllLegalMoves(opponentColor).Count == 0)
            {
                GameResult = new GameOverResult
                {
                    Type = GameOverType.Checkmate,
                    Winner = CurrentPlayer.PlayerColor
                };
                EndGame(GameResult);
            }
        }
        else if (GetAllLegalMoves(opponentColor).Count == 0)
        {
            GameResult = new GameOverResult { Type = GameOverType.Stalemate };
            EndGame(GameResult);
        }
    }

    void RemoveEnPassantPawn(Move move)
    {
        if (_selectedPiece?.Type != PieceType.Pawn) return;
        if (move.To.Column != move.From.Column && _board.Tiles[move.To.Row * 8 + move.To.Column].ChessPiece == null)
        {
            int capturedRow = move.From.Row;
            _board.Tiles[capturedRow * 8 + move.To.Column].ChessPiece = null;
        }
    }

    void MoveCastleRook(Move move)
    {
        if (_selectedPiece?.Type != PieceType.King) return;
        if (Math.Abs(move.To.Column - move.From.Column) == 2)
        {
            if (move.To.Column > move.From.Column)
            {
                var rook = _board.Tiles[move.From.Row * 8 + 7].ChessPiece;
                _board.Tiles[move.From.Row * 8 + 5].ChessPiece = rook;
                _board.Tiles[move.From.Row * 8 + 7].ChessPiece = null;
                rook!.Hasmove = true;
            }
            else
            {
                var rook = _board.Tiles[move.From.Row * 8 + 0].ChessPiece;
                _board.Tiles[move.From.Row * 8 + 3].ChessPiece = rook;
                _board.Tiles[move.From.Row * 8 + 0].ChessPiece = null;
                rook!.Hasmove = true;
            }
        }
    }

    void MoveAndPromote(Move move)
    {
        if (_selectedPiece?.Type != PieceType.Pawn) return;
        if (move.To.Row == 0 || move.To.Row == 7)
        {
            var promotionType = move.PromotionType == PieceType.None
                ? PieceType.Queen
                : move.PromotionType;
            var promotedPiece = new Piece(_selectedPiece.PieceColor, promotionType);
            _board.Tiles[move.To.Row * 8 + move.To.Column].ChessPiece = promotedPiece;
        }
    }

    void AddSlidingMoves(Piece piece, Postition position, List<Postition> moves)
    {
        int[][] directions = piece.Type switch
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
                    if (tile.ChessPiece.PieceColor != piece.PieceColor)
                        moves.Add(new Postition { Row = r, Column = c });
                    break;
                }
            }
        }
    }

    void AddPawnMoves(Piece piece, Postition position, List<Postition> moves)
    {
        int dir = piece.PieceColor == Color.White ? 1 : -1;
        int startRow = piece.PieceColor == Color.White ? 1 : 6;

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
                if (tile.ChessPiece != null && tile.ChessPiece.PieceColor != piece.PieceColor)
                    moves.Add(new Postition { Row = r1, Column = c });
            }
        }
    }

    void AddFixedMoves(Piece piece, Postition position, List<Postition> moves)
    {
        int[][] offsets = piece.Type switch
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
            if (tile.ChessPiece == null || tile.ChessPiece.PieceColor != piece.PieceColor)
                moves.Add(new Postition { Row = r, Column = c });
        }
    }

    bool CanAttack(Postition from, Postition to, Color attackerColor)
    {
        var piece = _board.Tiles[from.Row * 8 + from.Column].ChessPiece;
        if (piece == null || piece.PieceColor != attackerColor) return false;

        var moves = new List<Postition>();
        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, from, moves);
                break;
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

        return moves.Contains(to);
    }
    // suggestion move piece
    public Move? GetSuggestedMoveForPiece(Postition piecePosition)
    {
    var tile = _board.Tiles[piecePosition.Row * 8 + piecePosition.Column];
    if (tile.ChessPiece == null || tile.ChessPiece.PieceColor != CurrentPlayer.PlayerColor)
    {
        return null;
    }

    // Dapatkan semua langkah legal untuk bidak ini
    List<Postition>? legalMoves = GetLegalMoves(piecePosition);
    if (legalMoves == null || legalMoves.Count == 0) return null;

    Move? bestMove = null;
    
    // Inisialisasi skor terburuk (sangat rendah untuk Putih, sangat tinggi untuk Hitam)
    int bestScore = (CurrentPlayer.PlayerColor == Color.White) ? int.MinValue : int.MaxValue;

    foreach (var targetPosition in legalMoves)
    {
        // 1. Simpan referensi papan asli[cite: 25]
        Board originalBoard = _board;

        // 2. Gunakan Deep Clone untuk simulasi[cite: 25]
        _board = BoardHelper.Copy(originalBoard);

        // 3. Eksekusi simulasi langkah
        var movingPiece = _board.Tiles[piecePosition.Row * 8 + piecePosition.Column].ChessPiece;
        _board.Tiles[targetPosition.Row * 8 + targetPosition.Column].ChessPiece = movingPiece;
        _board.Tiles[piecePosition.Row * 8 + piecePosition.Column].ChessPiece = null;

        // 4. Evaluasi hasil papan setelah langkah ini
        int currentScore = EvaluateBoard(_board);

        // 5. Kembalikan referensi ke papan asli[cite: 25]
        _board = originalBoard;

        // 6. Tentukan apakah ini langkah terbaik
        if (CurrentPlayer.PlayerColor == Color.White)
        {
            if (currentScore > bestScore)
            {
                bestScore = currentScore;
                bestMove = new Move { From = piecePosition, To = targetPosition, Type = MoveType.Normal };
            }
        }
        else // Hitam mencari skor terendah/paling negatif
        {
            if (currentScore < bestScore)
            {
                bestScore = currentScore;
                bestMove = new Move { From = piecePosition, To = targetPosition, Type = MoveType.Normal };
            }
        }
    }

    return bestMove;
}

    bool IsKingInCheck(Color kingColor)
    {
        var rulesGame = CreateRulesGame(kingColor);
        return rulesGame.IsInCheck(ToRulesPlayer(kingColor));
    }
    
    // Legal Moves
    List<Postition>? GetLegalMoves(Postition position)
    {
        var piece = _board.Tiles[position.Row * 8 + position.Column].ChessPiece;
        if (piece == null) return new List<Postition>();

        // ChessDotNet menjadi sumber aturan legal (termasuk check, castling,
        // en passant, dan promosi). FEN dibangun dari state papan lokal agar
        // API/DTO lama tidak perlu diubah.
        var rulesGame = CreateRulesGame(piece.PieceColor);
        var rulesPosition = new ChessRules.Position(
            (ChessRules.File)position.Column,
            position.Row + 1);

        return rulesGame
            .GetValidMoves(rulesPosition)
            .Select(move => new Postition
            {
                Row = move.NewPosition.Rank - 1,
                Column = (int)move.NewPosition.File
            })
            .Distinct()
            .ToList();
    }

    private bool IsMoveLegal(Postition from, Postition to, Color movingColor)
    {
        Board originalBoard = _board;

        try
        {
            _board = BoardHelper.Copy(originalBoard);

            var movingPiece =
                _board.Tiles[from.Row * 8 + from.Column].ChessPiece;

            _board.Tiles[to.Row * 8 + to.Column].ChessPiece = movingPiece;
            _board.Tiles[from.Row * 8 + from.Column].ChessPiece = null;

            return !IsKingInCheck(movingColor);
        }
        finally
        {
            // Wajib mengembalikan referensi papan utama.
            _board = originalBoard;
        }
    }

    List<Postition> GetAllLegalMoves(Color color)
    {
        var rulesGame = CreateRulesGame(color);
        return rulesGame
            .GetValidMoves(ToRulesPlayer(color))
            .Select(move => new Postition
            {
                Row = move.NewPosition.Rank - 1,
                Column = (int)move.NewPosition.File
            })
            .ToList();
    }

    List<Postition> GetStandardMoves()
    {
        return _availableMoves ?? new List<Postition>();
    }

    List<Postition> GetPromotionMoves()
    {
        return new List<Postition>
        {
            new Postition { Row = 0, Column = 0 },
            new Postition { Row = 0, Column = 0 }
        };
    }

    List<Postition> GetCastleMoves(Postition kingPos, Piece king)
    {
        var moves = new List<Postition>();
        if (king.Hasmove || IsKingInCheck(king.PieceColor)) return moves;
        
        
        int row = kingPos.Row;
        // KINGISDE CASTLING 
        var kingSideRook = _board.Tiles[row * 8 + 7].ChessPiece;
        if (kingSideRook != null && kingSideRook.Type == PieceType.Rook && !kingSideRook.Hasmove)
        {
            // Aturan FIDE 3: Jalur (kolom f/5 dan g/6) harus KOSONG
            if (_board.Tiles[row * 8 + 5].ChessPiece == null &&
                _board.Tiles[row * 8 + 6].ChessPiece == null)
            {
                // Aturan FIDE 4 & 5: Petak yang dilewati (f) dan dituju (g) TIDAK diserang lawan
                if (!IsSquareAttacked(new Postition { Row = row, Column = 5 }, king.PieceColor) &&
                    !IsSquareAttacked(new Postition { Row = row, Column = 6 }, king.PieceColor))
                {
                    moves.Add(new Postition { Row = row, Column = 6 });
                }
            }
        }
        // QUEENSIDE CASTLING
        var queenSideRook = _board.Tiles[row * 8 + 0].ChessPiece;
        if (queenSideRook != null && queenSideRook.Type == PieceType.Rook && !queenSideRook.Hasmove)
        {
            // Aturan FIDE 3: Jalur (kolom d/3, c/2, dan b/1) harus KOSONG
            if (_board.Tiles[row * 8 + 1].ChessPiece == null &&
                _board.Tiles[row * 8 + 2].ChessPiece == null &&
                _board.Tiles[row * 8 + 3].ChessPiece == null)
            {
                // Aturan FIDE 4 & 5: Petak dilewati (d/3) dan dituju (c/2) TIDAK diserang.
                // Sesuai aturan FIDE, petak b (kolom 1) TIDAK PERLU dicek serangannya karena Raja tidak melewatinya.
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
                    if (piece.Type == PieceType.Pawn)
                    {
                        // Pion menyerang secara diagonal 1 petak ke depan
                        int dir = (attackerColor == Color.White) ? 1 : -1;
                        if (r + dir == targetPos.Row && (c - 1 == targetPos.Column || c + 1 == targetPos.Column))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        // Untuk bidak lain, gunakan logika CanAttack standar
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
        if (pawn == null || pawn.Type != PieceType.Pawn) return moves;

        //evaluate last move lawan
        int lastMoveRowDiff = Math.Abs(_lastMove.To.Row - _lastMove.From.Row);

        if (lastMoveRowDiff == 2)
        {
            var lastMovedPiece = _board.Tiles[_lastMove.To.Row * 8 + _lastMove.To.Column].ChessPiece;

            // Pastikan bidak yang baru saja bergerak adalah Pion musuh
            if (lastMovedPiece != null &&
                lastMovedPiece.Type == PieceType.Pawn &&
                lastMovedPiece.PieceColor != pawn.PieceColor)
            {
                // 3. Syarat Posisi: Pion kita harus berada di baris yang sama dengan pion musuh, 
                // dan bersebelahan (selisih kolom = 1)
                if (_lastMove.To.Row == pawnPos.Row &&
                    Math.Abs(_lastMove.To.Column - pawnPos.Column) == 1)
                {
                    // Hitung baris tangkapan (maju 1 petak secara diagonal ke belakang pion musuh)
                    int captureRow = pawn.PieceColor == Color.White ? pawnPos.Row + 1 : pawnPos.Row - 1;
                    int captureCol = _lastMove.To.Column;

                    // Pastikan target berada di dalam papan
                    if (captureRow >= 0 && captureRow < 8 && captureCol >= 0 && captureCol < 8)
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
                if (piece != null && piece.Type == PieceType.King && piece.PieceColor == kingColor)
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
    
    public List<Postition>? GetAvailableMovesForSelectedPiece()
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
        // Memanggil fungsi pilihan bidak Anda yang sudah ada[cite: 25]
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
                int pieceValue = piece.Type switch
                {
                    PieceType.Pawn => 10,
                    PieceType.Knight => 30,
                    PieceType.Bishop => 30,
                    PieceType.Rook => 50,
                    PieceType.Queen => 90,
                    PieceType.King => 900,
                    _ => 0
                };
    
                // Tambah skor jika Putih, kurangi jika Hitam
                score += (piece.PieceColor == Color.White) ? pieceValue : -pieceValue;
            }
        }
        return score;
    }

    // ==========================================
    // BOT / AI ENGINE
    // - ChessDotNet 1.0.0: aturan, langkah legal, dan simulasi posisi.
    // - Negamax + Alpha-Beta: pemilihan langkah terbaik.
    // Tidak membutuhkan executable atau engine eksternal.
    // ==========================================

    public const int DefaultBotDepth = 3;

    private static char PieceToFen(Piece piece)
    {
        char symbol = piece.Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => throw new InvalidOperationException($"Tipe bidak {piece.Type} tidak dapat diubah menjadi FEN.")
        };

        return piece.PieceColor == Color.White
            ? char.ToUpperInvariant(symbol)
            : symbol;
    }

    private string GetCastlingFen()
    {
        var rights = new StringBuilder();

        void AddRight(Color color, int row, int rookColumn, char symbol)
        {
            var king = _board.Tiles[row * 8 + 4].ChessPiece;
            var rook = _board.Tiles[row * 8 + rookColumn].ChessPiece;
            if (king != null && king.Type == PieceType.King && king.PieceColor == color && !king.Hasmove &&
                rook != null && rook.Type == PieceType.Rook && rook.PieceColor == color && !rook.Hasmove)
            {
                rights.Append(symbol);
            }
        }

        AddRight(Color.White, 0, 7, 'K');
        AddRight(Color.White, 0, 0, 'Q');
        AddRight(Color.Black, 7, 7, 'k');
        AddRight(Color.Black, 7, 0, 'q');
        return rights.Length == 0 ? "-" : rights.ToString();
    }

    private string GetEnPassantFen()
    {
        if (_lastMove.Equals(default(Move))) return "-";

        var movedPiece = _board.Tiles[_lastMove.To.Row * 8 + _lastMove.To.Column].ChessPiece;
        if (movedPiece == null || movedPiece.Type != PieceType.Pawn ||
            Math.Abs(_lastMove.To.Row - _lastMove.From.Row) != 2)
        {
            return "-";
        }

        int targetRow = (_lastMove.From.Row + _lastMove.To.Row) / 2;
        return ToAlgebraicSquare(new Postition { Row = targetRow, Column = _lastMove.To.Column });
    }

    private string ToFen(Color colorToMove)
    {
        var fen = new StringBuilder();
        for (int row = 7; row >= 0; row--)
        {
            int empty = 0;
            for (int column = 0; column < 8; column++)
            {
                var piece = _board.Tiles[row * 8 + column].ChessPiece;
                if (piece == null)
                {
                    empty++;
                    continue;
                }

                if (empty > 0)
                {
                    fen.Append(empty);
                    empty = 0;
                }
                fen.Append(PieceToFen(piece));
            }

            if (empty > 0) fen.Append(empty);
            if (row > 0) fen.Append('/');
        }

        int fullMoveNumber = (_moveHistory.Count / 2) + 1;
        fen.Append(colorToMove == Color.White ? " w " : " b ");
        fen.Append(GetCastlingFen());
        fen.Append(' ');
        fen.Append(GetEnPassantFen());
        fen.Append(" 0 ");
        fen.Append(fullMoveNumber);
        return fen.ToString();
    }

    private static string ToAlgebraicSquare(Postition position) =>
        $"{(char)('a' + position.Column)}{position.Row + 1}";

    private ChessRules.ChessGame CreateRulesGame(Color colorToMove) =>
        new ChessRules.ChessGame(ToFen(colorToMove));

    private static ChessRules.Player ToRulesPlayer(Color color) =>
        color == Color.White ? ChessRules.Player.White : ChessRules.Player.Black;

    private static PieceType ToLocalPromotion(char? promotion)
    {
        if (!promotion.HasValue) return PieceType.None;
        return char.ToUpperInvariant(promotion.Value) switch
        {
            'Q' => PieceType.Queen,
            'R' => PieceType.Rook,
            'B' => PieceType.Bishop,
            'N' => PieceType.Knight,
            _ => PieceType.None
        };
    }

    private static int EvaluateRulesGame(ChessRules.ChessGame game)
    {
        int score = 0;
        string boardFen = game.GetFen().Split(' ')[0];
        foreach (char symbol in boardFen)
        {
            if (symbol == '/' || char.IsDigit(symbol)) continue;
            int value = char.ToLowerInvariant(symbol) switch
            {
                'p' => 10,
                'n' => 30,
                'b' => 30,
                'r' => 50,
                'q' => 90,
                'k' => 900,
                _ => 0
            };
            score += char.IsUpper(symbol) ? value : -value;
        }
        return score;
    }

    private int Negamax(
        ChessRules.ChessGame rulesGame,
        int depth,
        int alpha,
        int beta)
    {
        var player = rulesGame.WhoseTurn;
        var legalMoves = rulesGame.GetValidMoves(player).ToList();
        if (legalMoves.Count == 0)
        {
            return rulesGame.IsInCheck(player) ? -100000 - depth : 0;
        }

        if (depth <= 0)
        {
            int perspective = player == ChessRules.Player.White ? 1 : -1;
            return perspective * EvaluateRulesGame(rulesGame);
        }

        int best = int.MinValue;
        foreach (var rulesMove in legalMoves)
        {
            var child = new ChessRules.ChessGame(rulesGame.GetFen());
            child.MakeMove(rulesMove, true);
            int score = -Negamax(child, depth - 1, -beta, -alpha);

            if (score > best) best = score;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }

        return best;
    }

    /// <summary>
    /// Mencari langkah terbaik dengan Negamax + Alpha-Beta internal. Seluruh
    /// langkah legal dan posisi anak dibuat oleh ChessDotNet 1.0.0.
    /// </summary>
    public Move? GetBestMove(Color color, int depth = DefaultBotDepth)
    {
        depth = Math.Clamp(depth, 1, 4);
        var rulesGame = CreateRulesGame(color);
        var rulesPlayer = ToRulesPlayer(color);
        var legalMoves = rulesGame.GetValidMoves(rulesPlayer).ToList();
        if (legalMoves.Count == 0) return null;

        ChessRules.Move? bestRulesMove = null;
        int bestScore = int.MinValue;
        int alpha = int.MinValue;
        int beta = int.MaxValue;

        foreach (var rulesMove in legalMoves)
        {
            var child = new ChessRules.ChessGame(rulesGame.GetFen());
            child.MakeMove(rulesMove, true);
            int score = -Negamax(child, depth - 1, -beta, -alpha);

            if (bestRulesMove == null || score > bestScore)
            {
                bestScore = score;
                bestRulesMove = rulesMove;
            }
            if (score > alpha) alpha = score;
        }

        if (bestRulesMove == null) return null;
        return new Move
        {
            From = new Postition
            {
                Row = bestRulesMove.OriginalPosition.Rank - 1,
                Column = (int)bestRulesMove.OriginalPosition.File
            },
            To = new Postition
            {
                Row = bestRulesMove.NewPosition.Rank - 1,
                Column = (int)bestRulesMove.NewPosition.File
            },
            Type = MoveType.Normal,
            PromotionType = ToLocalPromotion(bestRulesMove.Promotion)
        };
    }

    /// <summary>
    /// Menjalankan giliran Bot untuk CurrentPlayer saat ini: mencari best
    /// move via Negamax internal berbasis ChessDotNet, lalu mengeksekusinya lewat alur
    /// TrySelectPiece -> MakeMove yang sudah ada (sehingga timer, history,
    /// dan pengecekan checkmate/stalemate tetap konsisten dengan langkah
    /// manusia biasa).
    /// </summary>
    public Move? MakeBotMove(int depth = DefaultBotDepth)
    {
        if (CheckTimeStatus().IsExpired) return null;

        var color = CurrentPlayer.PlayerColor;
        var bestMove = GetBestMove(color, depth);
        if (bestMove == null) return null;

        // Waktu pencarian Negamax ikut dihitung sebagai waktu berpikir bot.
        if (CheckTimeStatus().IsExpired) return null;

        if (!TrySelectPiece(bestMove.Value.From))
            return null;

        var success = MakeMove(bestMove.Value);
        return success ? bestMove : null;
    }
}
