using ChessConsole2.Enums;
using ChessConsole2.Helper;
using ChessConsole2.Interface;
using ChessConsole2.Model;

namespace ChessConsole2;

public class Game : IGame
{
    Board _board;
    public Player? WhitePlayer;
    public Player? BlackPlayer;
    public Player CurrentPlayer;
    public ChessTimer WhiteTimer;
    public ChessTimer BlackTimer;
    private Piece? _selectedPiece;
    private Postition _selectedPostition;
    private List<Postition>? _availableMoves;
    private Move _lastMove;
    private readonly List<MoveHistory> _moveHistory = new();
    public event Action OnGameOver;
    public GameOverResult GameResult;

    public Game(Player whitePlayer, Player blackPlayer)
        {
        WhitePlayer = whitePlayer;
        BlackPlayer = blackPlayer;
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
        GameResult = new GameOverResult { Type = GameOverType.Timeout, Winner = Color.Black };
        EndGame(GameResult);
    }   

    private void OnBlackTimeOut()
    {
        GameResult = new GameOverResult { Type = GameOverType.Timeout, Winner = Color.White };
        EndGame(GameResult);
    }

    public void StartGame()
    {
        CurrentPlayer = WhitePlayer;
        WhiteTimer = new ChessTimer(TimeSpan.FromMinutes(10));
        BlackTimer = new ChessTimer(TimeSpan.FromMinutes(10));

        WhiteTimer.OnTimeOut += OnWhiteTimeOut;
        BlackTimer.OnTimeOut += OnBlackTimeOut;

        WhiteTimer.StartTimer();
    }

    public void Resign(Player player)
    {
        GameResult = (player == WhitePlayer)
            ? new GameOverResult { Type = GameOverType.Resigned, Winner = Color.Black }
            : new GameOverResult { Type = GameOverType.Resigned, Winner = Color.White };
        EndGame(GameResult);
    }

    

    public void MakeMove(Move move)
    {
        // Jangan izinkan langkah memakai selection lama atau posisi asal palsu.
        if (_selectedPiece == null ||
            _availableMoves == null ||
            !IsInsideBoard(move.From) ||
            !IsInsideBoard(move.To) ||
            move.From.row != _selectedPostition.row ||
            move.From.Column != _selectedPostition.Column ||
            _selectedPiece.PieceColor != CurrentPlayer.PlayerColor ||
            !_availableMoves.Contains(move.To))
        {
            return;
        }

        Tile fromTile = _board.Tiles[move.From.row * 8 + move.From.Column];
        Tile toTile = _board.Tiles[move.To.row * 8 + move.To.Column];

        if (!ReferenceEquals(fromTile.ChessPiece, _selectedPiece))
        {
            ClearSelection();
            return;
        }

        var movedPieceType = _selectedPiece.Type;
        var movedPieceColor = _selectedPiece.PieceColor;
        var capturedPieceType = toTile.ChessPiece?.Type;
        var isEnPassant = movedPieceType == PieceType.Pawn &&
                          move.From.Column != move.To.Column &&
                          toTile.ChessPiece == null;

        if (isEnPassant)
        {
            capturedPieceType = PieceType.Pawn;
            move.Type = MoveType.EnPassent;
        }
        
        //detection castling move
        bool isCastling = _selectedPiece.Type == PieceType.King && Math.Abs(move.To.Column - move.From.Column) == 2;
        if (isCastling)
        {
            move.Type = MoveType.Castling; 
        }
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
        SwitchTurn();
    }


    private void SwitchTurn()
    {
        if (CurrentPlayer == WhitePlayer)
        {
            WhiteTimer.StopTimer();
            BlackTimer.StartTimer();
            CurrentPlayer = BlackPlayer;
        }
        else
        {
            BlackTimer.StopTimer();
            WhiteTimer.StartTimer();
            CurrentPlayer = WhitePlayer;
        }
    }

    void EndGame(GameOverResult result)
    {
        WhiteTimer.StopTimer();
        BlackTimer.StopTimer();
        OnGameOver?.Invoke();
    }

    void CheckMoveResult()
    {
        var opponent = (CurrentPlayer == WhitePlayer) ? BlackPlayer : WhitePlayer;
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
        if (_selectedPiece.Type != PieceType.Pawn) return;
        if (move.To.Column != move.From.Column && _board.Tiles[move.To.row * 8 + move.To.Column].ChessPiece == null)
        {
            int capturedRow = move.From.row;
            _board.Tiles[capturedRow * 8 + move.To.Column].ChessPiece = null;
        }
    }

    void MoveCastleRook(Move move)
    {
        if (_selectedPiece.Type != PieceType.King) return;
        if (Math.Abs(move.To.Column - move.From.Column) == 2)
        {
            if (move.To.Column > move.From.Column)
            {
                var rook = _board.Tiles[move.From.row * 8 + 7].ChessPiece;
                _board.Tiles[move.From.row * 8 + 5].ChessPiece = rook;
                _board.Tiles[move.From.row * 8 + 7].ChessPiece = null;
                rook.Hasmove = true;
            }
            else
            {
                var rook = _board.Tiles[move.From.row * 8 + 0].ChessPiece;
                _board.Tiles[move.From.row * 8 + 3].ChessPiece = rook;
                _board.Tiles[move.From.row * 8 + 0].ChessPiece = null;
                rook.Hasmove = true;
            }
        }
    }

    void MoveAndPromote(Move move)
    {
        if (_selectedPiece.Type != PieceType.Pawn) return;
        if (move.To.row == 0 || move.To.row == 7)
        {
            var promotedPiece = new Piece(_selectedPiece.PieceColor, move.PromptionType);
            _board.Tiles[move.To.row * 8 + move.To.Column].ChessPiece = promotedPiece;
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
                int r = position.row + dir[0] * i;
                int c = position.Column + dir[1] * i;
                if (r < 0 || r >= 8 || c < 0 || c >= 8) break;

                var tile = _board.Tiles[r * 8 + c];
                if (tile.ChessPiece == null)
                {
                    moves.Add(new Postition { row = r, Column = c });
                }
                else
                {
                    if (tile.ChessPiece.PieceColor != piece.PieceColor)
                        moves.Add(new Postition { row = r, Column = c });
                    break;
                }
            }
        }
    }

    void AddPawnMoves(Piece piece, Postition position, List<Postition> moves)
    {
        int dir = piece.PieceColor == Color.White ? 1 : -1;
        int startRow = piece.PieceColor == Color.White ? 1 : 6;

        int r1 = position.row + dir;
        if (r1 >= 0 && r1 < 8 && _board.Tiles[r1 * 8 + position.Column].ChessPiece == null)
        {
            moves.Add(new Postition { row = r1, Column = position.Column });
            int r2 = position.row + dir * 2;
            if (position.row == startRow && _board.Tiles[r2 * 8 + position.Column].ChessPiece == null)
                moves.Add(new Postition { row = r2, Column = position.Column });
        }

        for (int dc = -1; dc <= 1; dc += 2)
        {
            int c = position.Column + dc;
            if (r1 >= 0 && r1 < 8 && c >= 0 && c < 8)
            {
                var tile = _board.Tiles[r1 * 8 + c];
                if (tile.ChessPiece != null && tile.ChessPiece.PieceColor != piece.PieceColor)
                    moves.Add(new Postition { row = r1, Column = c });
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
            int r = position.row + offset[0];
            int c = position.Column + offset[1];
            if (r < 0 || r >= 8 || c < 0 || c >= 8) continue;

            var tile = _board.Tiles[r * 8 + c];
            if (tile.ChessPiece == null || tile.ChessPiece.PieceColor != piece.PieceColor)
                moves.Add(new Postition { row = r, Column = c });
        }
    }

    bool CanAttack(Postition from, Postition to, Color attackerColor)
    {
        var piece = _board.Tiles[from.row * 8 + from.Column].ChessPiece;
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
    var tile = _board.Tiles[piecePosition.row * 8 + piecePosition.Column];
    if (tile.ChessPiece == null || tile.ChessPiece.PieceColor != CurrentPlayer.PlayerColor)
    {
        return null;
    }

    // Dapatkan semua langkah legal untuk bidak ini
    List<Postition>? legalMoves = GetLegalMoves(piecePosition);
    if (legalMoves.Count == 0) return null;

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
        var movingPiece = _board.Tiles[piecePosition.row * 8 + piecePosition.Column].ChessPiece;
        _board.Tiles[targetPosition.row * 8 + targetPosition.Column].ChessPiece = movingPiece;
        _board.Tiles[piecePosition.row * 8 + piecePosition.Column].ChessPiece = null;

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
        var kingPos = GetKingPosition(kingColor);
        var opponentColor = (kingColor == Color.White) ? Color.Black : Color.White;

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[r * 8 + c].ChessPiece;
                if (piece != null && piece.PieceColor == opponentColor)
                {
                    if (CanAttack(new Postition { row = r, Column = c }, kingPos, opponentColor))
                        return true;
                }
            }
        }
        return false;
    }
    
    // Legal Moves
    List<Postition>? GetLegalMoves(Postition position)
    {
        var piece = _board.Tiles[position.row * 8 + position.Column].ChessPiece;
        if (piece == null) return new List<Postition>();

        var pseudoLegalMoves = new List<Postition>();
        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, position, pseudoLegalMoves);
                // GABUNGKAN DI SINI: agar En Passant ikut diuji IsMoveLegal
                pseudoLegalMoves.AddRange(GetEnPassantMoves(position)); 
                break;
            case PieceType.Rook:
            case PieceType.Bishop:
            case PieceType.Queen:
                AddSlidingMoves(piece, position, pseudoLegalMoves);
                break;
            case PieceType.Knight:
            case PieceType.King:
                AddFixedMoves(piece, position, pseudoLegalMoves);
                // GABUNGKAN DI SINI: agar Castling ikut diuji IsMoveLegal
                pseudoLegalMoves.AddRange(GetCastleMoves(position, piece)); 
                break;
        }

        var legalMoves = new List<Postition>();
    
        // Sekarang semua pergerakan akan disaring secara merata
        foreach (var move in pseudoLegalMoves)
        {
            if (IsMoveLegal(position, move, piece.PieceColor))
                legalMoves.Add(move);
        }

        return legalMoves;
    }

    private bool IsMoveLegal(Postition from, Postition to, Color movingColor)
    {
        Board originalBoard = _board;

        try
        {
            _board = BoardHelper.Copy(originalBoard);

            var movingPiece =
                _board.Tiles[from.row * 8 + from.Column].ChessPiece;

            _board.Tiles[to.row * 8 + to.Column].ChessPiece = movingPiece;
            _board.Tiles[from.row * 8 + from.Column].ChessPiece = null;

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
        var allMoves = new List<Postition>();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[r * 8 + c].ChessPiece;
                if (piece != null && piece.PieceColor == color)
                {
                    var moves = GetLegalMoves(new Postition { row = r, Column = c });
                    allMoves.AddRange(moves);
                }
            }
        }
        return allMoves;
    }

    List<Postition> GetStandardMoves()
    {
        return _availableMoves ?? new List<Postition>();
    }

    List<Postition> GetPromotionMoves()
    {
        return new List<Postition>
        {
            new Postition { row = 0, Column = 0 },
            new Postition { row = 0, Column = 0 }
        };
    }

    List<Postition> GetCastleMoves(Postition kingPos, Piece king)
    {
        var moves = new List<Postition>();
        if (king.Hasmove || IsKingInCheck(king.PieceColor)) return moves;
        
        
        int row = kingPos.row;
        // KINGISDE CASTLING 
        var kingSideRook = _board.Tiles[row * 8 + 7].ChessPiece;
        if (kingSideRook != null && kingSideRook.Type == PieceType.Rook && !kingSideRook.Hasmove)
        {
            // Aturan FIDE 3: Jalur (kolom f/5 dan g/6) harus KOSONG
            if (_board.Tiles[row * 8 + 5].ChessPiece == null &&
                _board.Tiles[row * 8 + 6].ChessPiece == null)
            {
                // Aturan FIDE 4 & 5: Petak yang dilewati (f) dan dituju (g) TIDAK diserang lawan
                if (!IsSquareAttacked(new Postition { row = row, Column = 5 }, king.PieceColor) &&
                    !IsSquareAttacked(new Postition { row = row, Column = 6 }, king.PieceColor))
                {
                    moves.Add(new Postition { row = row, Column = 6 });
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
                if (!IsSquareAttacked(new Postition { row = row, Column = 2 }, king.PieceColor) &&
                    !IsSquareAttacked(new Postition { row = row, Column = 3 }, king.PieceColor))
                {
                    moves.Add(new Postition { row = row, Column = 2 });
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
                        if (r + dir == targetPos.row && (c - 1 == targetPos.Column || c + 1 == targetPos.Column))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        // Untuk bidak lain, gunakan logika CanAttack standar
                        if (CanAttack(new Postition { row = r, Column = c }, targetPos, attackerColor))
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

        var pawn = _board.Tiles[pawnPos.row * 8 + pawnPos.Column].ChessPiece;
        if (pawn == null || pawn.Type != PieceType.Pawn) return moves;

        //evaluate last move lawan
        int lastMoveRowDiff = Math.Abs(_lastMove.To.row - _lastMove.From.row);

        if (lastMoveRowDiff == 2)
        {
            var lastMovedPiece = _board.Tiles[_lastMove.To.row * 8 + _lastMove.To.Column].ChessPiece;

            // Pastikan bidak yang baru saja bergerak adalah Pion musuh
            if (lastMovedPiece != null &&
                lastMovedPiece.Type == PieceType.Pawn &&
                lastMovedPiece.PieceColor != pawn.PieceColor)
            {
                // 3. Syarat Posisi: Pion kita harus berada di baris yang sama dengan pion musuh, 
                // dan bersebelahan (selisih kolom = 1)
                if (_lastMove.To.row == pawnPos.row &&
                    Math.Abs(_lastMove.To.Column - pawnPos.Column) == 1)
                {
                    // Hitung baris tangkapan (maju 1 petak secara diagonal ke belakang pion musuh)
                    int captureRow = pawn.PieceColor == Color.White ? pawnPos.row + 1 : pawnPos.row - 1;
                    int captureCol = _lastMove.To.Column;

                    // Pastikan target berada di dalam papan
                    if (captureRow >= 0 && captureRow < 8 && captureCol >= 0 && captureCol < 8)
                    {
                        moves.Add(new Postition { row = captureRow, Column = captureCol });
                    }
                }
            }
        }

        return moves;
    }


    public MoveResult GetMoveResult()
    {
        var opponent = (CurrentPlayer == WhitePlayer) ? BlackPlayer : WhitePlayer;
        if (IsKingInCheck(opponent.PlayerColor))
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
                    return new Postition { row = r, Column = c };
                }
            }
        }
        return new Postition { row = -1, Column = -1 };
    }

    public Board GetCurrentBoard() { return _board; }
    
    public void SelectPiece(Postition position)
    {
        ClearSelection();

        if (!IsInsideBoard(position)) return;

        var tile = _board.Tiles[position.row * 8 + position.Column];
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
                Console.WriteLine($"Saran Langkah: Ke baris {suggestedMove.Value.To.row}, kolom {suggestedMove.Value.To.Column}");
            }
        }
    }
    private static bool IsInsideBoard(Postition position) =>  position.row >= 0 && 
                                                              position.row < 8 && position.Column >= 0 && position.Column < 8;

    private void ClearSelection()
    {
        _selectedPiece = null;
        _availableMoves = null;
        _selectedPostition = new Postition { row = -1, Column = -1 };
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
    
}
