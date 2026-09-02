using System.Reflection;
using System.Runtime.Serialization;
using ChessConsole.Enums;
using ChessConsole.Model;

namespace Game.Tests.GameEngine;

[TestFixture]
[NonParallelizable]
public class MakeMoveTests
{
    private Player _whitePlayer = null!;
    private Player _blackPlayer = null!;
    private ChessConsole2.Game _game = null!;

    [SetUp]
    public void Setup()
    {
        _whitePlayer = new Player("Mahindra", Color.White);
        _blackPlayer =  new Player("Saputra", Color.Black);
        
        _game = new ChessConsole2.Game(_whitePlayer, _blackPlayer,vsBot:true);
        _game.StartGame();
    }
    //Pawn Test
    [Test]
    public void MakeMove_ValidWhitePawnFromA2toA4_ShouldUpdateBoardSwitchTurn()
    {
        
        //a2
        var fromPos = new Postition{Row = 1, Column = 0};
        //a4
        var toPos = new Postition{Row = 3, Column = 0};
        
        bool isSelected = _game.TrySelectPiece(fromPos);
        Assert.IsTrue(isSelected, "Pion Pawn Putih Gagal dipilih");
        
        var move = new Move
        {
            From = fromPos, 
            To = toPos,
            Type = MoveType.Normal,
            PromotionType = PieceType.None
        };
        
        bool moveResult =_game.MakeMove(move);
        
        Assert.IsTrue(moveResult, "Pawn Dapat Bergerak ke a4");
        
        var board = _game.GetCurrentBoard();
        
        Assert.IsNull(board.Tiles[1 * 8 + 0].ChessPiece);
        
        var movedPiece = board.Tiles[3 * 8 + 0].ChessPiece;
        Assert.IsNotNull(movedPiece);
        
        Assert.That(movedPiece.Piece, Is.EqualTo(PieceType.Pawn));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
        
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
        Assert.That(_game.GetMoveHistory().Count, Is.EqualTo(1));
        
    }
    // promotion
    [Test]
    public void MakeMove_ValidWhitePawnPromotion_ShouldBeChangedPieceToQueen()
    {
        var board = PrepareCleanBoardWithKings();
        
        //taruh Pawn E6
        board.Tiles[6 * 8 + 3 ].ChessPiece = new Pieces(Color.White, PieceType.Pawn);
        

        
        var fromPos = new Postition{Row = 6, Column = 3};
        var toPos = new Postition{Row = 7, Column = 3};
        
        _game.TrySelectPiece(fromPos);

        var move = new Move
        {
            From = fromPos,
            To = toPos,
            Type = MoveType.Promotion,
            PromotionType = PieceType.Queen // Pemain memilih promosi menjadi Queen
        };

        // Act
        bool moveResult = _game.MakeMove(move);

        // Assert
        Assert.IsTrue(moveResult);
    
        // Pastikan bidak di a8 sekarang adalah Queen, bukan Pion
        var promotedPiece = board.Tiles[7 * 8 + 3].ChessPiece;
        Assert.IsNotNull(promotedPiece);
        Assert.That(promotedPiece.Piece, Is.EqualTo(PieceType.Queen));
    }
    
    
    //enpassant
    [Test]
    public void MakeMove_ValidWhitePawnEnpassant_ShouldBeCapturePawn()
{
    var board = PrepareCleanBoardWithKings();
    
    board.Tiles[1 * 8 + 4].ChessPiece = new Pieces(Color.White, PieceType.Pawn); // White Pawn E2
    board.Tiles[6 * 8 + 4].ChessPiece = new Pieces(Color.Black, PieceType.Pawn); // Black Pawn E7
    board.Tiles[6 * 8 + 3].ChessPiece = new Pieces(Color.Black, PieceType.Pawn); // Black Pawn D7
  
    
    // ==========================================
    // TURN 1: White (E2 ke E4)
    // ==========================================
    var fromPosWhiteTurn1 = new Postition{Row = 1, Column = 4};
    var toWhiteTurn1 = new Postition{Row = 3, Column = 4};
    var moveWhiteTurn1 = new Move
    {
        From = fromPosWhiteTurn1, To = toWhiteTurn1, Type = MoveType.Normal, PromotionType = PieceType.Pawn
    };
    Assert.IsTrue(_game.TrySelectPiece(fromPosWhiteTurn1), "GAGAL: Tidak bisa memilih Pion Putih di E2");
    Assert.IsTrue(_game.MakeMove(moveWhiteTurn1), "GAGAL: Pion Putih tidak bisa bergerak ke E4");

    // ==========================================
    // TURN 1: Black (E7 ke E6)
    // ==========================================
    var fromPosBlackTurn1 = new Postition{Row = 6, Column = 4};
    var toBlackTurn1 = new Postition{Row = 5, Column = 4};
    var moveBlackTurn1 = new Move
    {
        From = fromPosBlackTurn1, To = toBlackTurn1, Type = MoveType.Normal, PromotionType = PieceType.Pawn
    };
    Assert.IsTrue(_game.TrySelectPiece(fromPosBlackTurn1), "GAGAL: Tidak bisa memilih Pion Hitam di E7");
    Assert.IsTrue(_game.MakeMove(moveBlackTurn1), "GAGAL: Pion Hitam tidak bisa bergerak ke E6");

    // ==========================================
    // TURN 2: White (E4 ke E5)
    // ==========================================
    var fromPosWhiteTurn2 = new Postition{Row = 3, Column = 4};
    var toWhiteTurn2 = new Postition{Row = 4, Column = 4};
    var moveWhiteTurn2 = new Move
    {
        From = fromPosWhiteTurn2, To = toWhiteTurn2, Type = MoveType.Normal, PromotionType = PieceType.Pawn
    };
    Assert.IsTrue(_game.TrySelectPiece(fromPosWhiteTurn2), "GAGAL: Tidak bisa memilih Pion Putih di E4");
    Assert.IsTrue(_game.MakeMove(moveWhiteTurn2), "GAGAL: Pion Putih tidak bisa bergerak ke E5");

    // ==========================================
    // TURN 2: Black (D7 ke D5) - Memicu Syarat En Passant
    // ==========================================
    var fromPosBlackTurn2 = new Postition{Row = 6, Column = 3};
    var toBlackTurn2 = new Postition{Row = 4, Column = 3};
    var moveBlackTurn2 = new Move
    {
        From = fromPosBlackTurn2, To = toBlackTurn2, Type = MoveType.Normal, PromotionType = PieceType.Pawn
    };
    Assert.IsTrue(_game.TrySelectPiece(fromPosBlackTurn2), "GAGAL: Tidak bisa memilih Pion Hitam di D7");
    Assert.IsTrue(_game.MakeMove(moveBlackTurn2), "GAGAL: Pion Hitam tidak bisa bergerak ke D5");

    // ==========================================
    // TURN 3: White (En Passant E5 ke D6)
    // ==========================================
    var fromPosWhiteTurn3 = new Postition{Row = 4, Column = 4}; // Pion ada di E5
    var toWhiteTurn3 = new Postition{Row = 5, Column = 3};      // Menangkap secara diagonal ke D6
    var moveWhiteTurn3 = new Move
    {
        From = fromPosWhiteTurn3, To = toWhiteTurn3, Type = MoveType.EnPassent, PromotionType = PieceType.Pawn
    };
    
    Assert.IsTrue(_game.TrySelectPiece(fromPosWhiteTurn3), "GAGAL: Tidak bisa memilih Pion Putih di E5 untuk En Passant");
    bool isEnpassentMove = _game.MakeMove(moveWhiteTurn3);
    
    // Validasi akhir
    Assert.IsTrue(isEnpassentMove, "GAGAL: Langkah En Passant ditolak oleh MakeMove");
    Assert.IsNull(board.Tiles[4 * 8 + 3].ChessPiece, "Pion Hitam di D5 seharusnya hilang ditangkap (null).");
}

    [Test]
    public void MakeMove_ValidWhiteRookFromA1toA8_ShouldUpdateBoardSwitchTurn()
    {
        var board = PrepareCleanBoardWithKings();
        
        //taruh Rook A1
        board.Tiles[0 * 8 + 0 ].ChessPiece = new Pieces(Color.White, PieceType.Rook);
        
        //a1
        var fromPos = new Postition{Row = 0, Column = 0};
        //a8
        var toPos = new Postition{Row = 7, Column = 0};
        
        bool isSelected = _game.TrySelectPiece(fromPos);
        Assert.IsTrue(isSelected, "Pion Rook Putih Gagal dipilih");

        var move = new Move
        {
            From = fromPos,
            To = toPos,
            Type = MoveType.Normal,
            PromotionType = PieceType.Rook
        };
        bool moveResult;
        moveResult = _game.MakeMove(move);

        Assert.IsTrue(moveResult, "Rook Dapat Bergerak ke a8");
        
        Assert.IsNull(board.Tiles[0 * 8 + 0].ChessPiece);
        
        var movedPiece = board.Tiles[7 * 8 + 0].ChessPiece;
        Assert.IsNotNull(movedPiece);
        
        Assert.That(movedPiece.Piece, Is.EqualTo(PieceType.Rook));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
        
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
        Assert.That(_game.GetMoveHistory().Count, Is.EqualTo(1));
    }
    [Test]
    public void MakeMove_ValidWhiteKnightLShape_ShouldUpdateBoardAndSwitchTurn()
    {
        var board = PrepareCleanBoardWithKings();
        
        // Arrange: Taruh Kuda Putih di b1 (Row 0, Column 1)
        board.Tiles[0 * 8 + 1].ChessPiece = new Pieces(Color.White, PieceType.Knight);
        
        var fromPos = new Postition { Row = 0, Column = 1 };

        // Melangkah bentuk "L" ke c3 (Row 2, Column 2)
        var toPos = new Postition { Row = 2, Column = 2 };

        Assert.IsTrue(_game.TrySelectPiece(fromPos), "Kuda Putih gagal dipilih.");

        var move = new Move
        {
            From = fromPos,
            To = toPos,
            Type = MoveType.Normal,
            PromotionType = PieceType.None
        };

        // Act
        bool moveResult = _game.MakeMove(move);

        // Assert
        Assert.IsTrue(moveResult, "Kuda seharusnya dapat melangkah dalam pola L ke c3.");
        Assert.IsNull(board.Tiles[0 * 8 + 1].ChessPiece, "Petak b1 asal harus menjadi null.");
            
        var movedPiece = board.Tiles[2 * 8 + 2].ChessPiece;
        Assert.IsNotNull(movedPiece);
        Assert.That(movedPiece.Piece, Is.EqualTo(PieceType.Knight));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
    }
    [Test]
    public void MakeMove_ValidWhiteBishopDiagonal_ShouldUpdateBoardAndSwitchTurn()
    {
        var board = PrepareCleanBoardWithKings();

        // Taruh Gajah Putih di c4 (Row 3, Column 2) - posisi terbuka tanpa halangan
        var fromPos = new Postition { Row = 3, Column = 2 };
        board.Tiles[3 * 8 + 2].ChessPiece = new Pieces(Color.White, PieceType.Bishop);

        // Melangkah diagonal ke f7 (Row 6, Column 5)
        var toPos = new Postition { Row = 6, Column = 5 };

        Assert.IsTrue(_game.TrySelectPiece(fromPos), "Gajah Putih gagal dipilih.");

        var move = new Move
        {
            From = fromPos,
            To = toPos,
            Type = MoveType.Normal,
            PromotionType = PieceType.None
        };

        // Act
        bool moveResult = _game.MakeMove(move);

        // Assert
        Assert.IsTrue(moveResult, "Gajah seharusnya dapat melangkah secara diagonal ke f7.");
        Assert.IsNull(board.Tiles[3 * 8 + 2].ChessPiece, "Petak asal c4 harus menjadi null.");
    
        var movedPiece = board.Tiles[6 * 8 + 5].ChessPiece;
        Assert.IsNotNull(movedPiece);
        Assert.That(movedPiece.Piece, Is.EqualTo(PieceType.Bishop));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
    }
    [Test]
    public void MakeMove_ValidWhiteQueenCaptureEnemy_ShouldUpdateBoardAndSwitchTurn()
    {
        var board = PrepareCleanBoardWithKings();

        // Taruh Menteri Putih di d3 (Row 2, Column 3)
        var fromPos = new Postition { Row = 2, Column = 3 };
        board.Tiles[2 * 8 + 3].ChessPiece = new Pieces(Color.White, PieceType.Queen);

        // Taruh Benteng Hitam di d6 (Row 5, Column 3) untuk memicu capture
        board.Tiles[5 * 8 + 3].ChessPiece = new Pieces(Color.Black, PieceType.Rook);

        // Tujuan: Melangkah lurus memakan Benteng Hitam di d6
        var toPos = new Postition { Row = 5, Column = 3 };

        Assert.IsTrue(_game.TrySelectPiece(fromPos), "Menteri Putih gagal dipilih.");

        var move = new Move
        {
            From = fromPos,
            To = toPos,
            Type = MoveType.Normal,
            PromotionType = PieceType.None
        };

        // Act
        bool moveResult = _game.MakeMove(move);

        // Assert
        Assert.IsTrue(moveResult, "Menteri seharusnya dapat melangkah lurus dan memakan lawan di d6.");
        Assert.IsNull(board.Tiles[2 * 8 + 3].ChessPiece, "Petak d3 asal harus menjadi null.");
    
        var movedPiece = board.Tiles[5 * 8 + 3].ChessPiece;
        Assert.IsNotNull(movedPiece);
        Assert.That(movedPiece.Piece, Is.EqualTo(PieceType.Queen));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
    
        var lastHistory = _game.GetMoveHistory().LastOrDefault();
        Assert.IsNotNull(lastHistory);
        Assert.That(lastHistory.CapturedPieceType, Is.EqualTo(PieceType.Rook));
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
    }
    // check checkmate with FoolsMate
    [Test]
    public void MakeMove_FoolsMate_SetsCheckmateAndBlackAsWinner()
    {
        var game = CreateStartedGame(clockEnabled: false);

        Move(game, 1, 5, 2, 5); // f2-f3
        Move(game, 6, 4, 4, 4); // e7-e5
        Move(game, 1, 6, 3, 6); // g2-g4
        Move(game, 7, 3, 3, 7); // Qd8-h4#

        Assert.Multiple(() =>
        {
            Assert.That(game.GameResult.Type, Is.EqualTo(GameOverType.Checkmate));
            Assert.That(game.GameResult.Winner, Is.EqualTo(Color.Black));
            Assert.That(game.GetMoveResult(), Is.EqualTo(MoveResult.Checkmate));
        });
    }

    [Test]
    // check StaleMate
    public void MakeMove_WhenOpponentHasNoLegalMoveAndIsNotChecked_SetsStalemate()
    {
        var game = CreateStartedGame(clockEnabled: false);
        var board = game.GetCurrentBoard();

        ClearBoard(board);

        // Posisi sebelum Qc7-b6:
        // Black: Ka8. White: Kc6, Qc7. Setelah Qb6, semua petak gerak
        // raja hitam diserang, tetapi Ka8 sendiri tidak sedang diserang.
        Place(board, 7, 0, Color.Black, PieceType.King); // a8
        Place(board, 5, 2, Color.White, PieceType.King); // c6
        Place(board, 6, 2, Color.White, PieceType.Queen); // c7

        Move(game, 6, 2, 5, 1); // Qc7-b6

        Assert.Multiple(() =>
        {
            Assert.That(game.GameResult.Type, Is.EqualTo(GameOverType.Stalemate));
            Assert.That(game.GetMoveResult(), Is.EqualTo(MoveResult.Stalemate));
        });
    }
    // check timeout
    [Test]
    public void CheckTimeStatus_WhenWhiteTimeIsZero_SetsTimeoutAndBlackAsWinner()
    {
        var game = CreateStartedGame(clockEnabled: true);
        bool gameOverRaised = false;
        game.OnGameOver += () => gameOverRaised = true;

        SetPrivateField(game, "_whiteRemaining", TimeSpan.Zero);

        var status = game.CheckTimeStatus();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsExpired, Is.True);
            Assert.That(status.TimedOutColor, Is.EqualTo(Color.White));
            Assert.That(status.WhiteRemaining, Is.EqualTo(TimeSpan.Zero));
            Assert.That(game.GameResult.Type, Is.EqualTo(GameOverType.Timeout));
            Assert.That(game.GameResult.Winner, Is.EqualTo(Color.Black));
            Assert.That(gameOverRaised, Is.True);
        });
    }
    private Board PrepareCleanBoardWithKings()
    {
        var board = _game.GetCurrentBoard();

        // 1. Kosongkan semua petak
        foreach (var tile in board.Tiles)
        {
            tile.ChessPiece = null;
        }

        // 2. Taruh Raja Putih (e1) dan Hitam (e8) agar validasi engine aman
        board.Tiles[0 * 8 + 4].ChessPiece = new Pieces(Color.White, PieceType.King); // e1
        board.Tiles[7 * 8 + 4].ChessPiece = new Pieces(Color.Black, PieceType.King); // e8

        return board;
    }
    private static ChessConsole2.Game CreateStartedGame(bool clockEnabled)
    {
        var white = CreatePlayer(Color.White);
        var black = CreatePlayer(Color.Black);
        var game = new ChessConsole2.Game(
            white,
            black,
            vsBot: false,
            initialMinutes: 10,
            clockEnabled: clockEnabled);

        game.StartGame();
        return game;
    }
     private static void Move(ChessConsole2.Game game, int fromRow, int fromColumn, int toRow, int toColumn)
    {
        var from = new Postition { Row = fromRow, Column = fromColumn };
        var to = new Postition { Row = toRow, Column = toColumn };

        Assert.That(
            game.TrySelectPiece(from),
            Is.True,
            $"Bidak pada ({fromRow}, {fromColumn}) tidak dapat dipilih.");

        Assert.That(
            game.MakeMove(new Move
            {
                From = from,
                To = to,
                Type = MoveType.Normal,
                PromotionType = PieceType.None
            }),
            Is.True,
            $"Langkah ({fromRow}, {fromColumn}) ke ({toRow}, {toColumn}) ditolak.");
    }

    private static void ClearBoard(Board board)
    {
        foreach (var tile in board.Tiles)
            tile.ChessPiece = null;
    }

    private static void Place(
        Board board,
        int row,
        int column,
        Color color,
        PieceType pieceType)
    {
        board.Tiles[row * 8 + column].ChessPiece = new Pieces(color, pieceType);
    }

    // Game hanya membaca PlayerColor. Helper ini membuat test tidak terikat
    // pada bentuk konstruktor Player yang tidak disertakan bersama Game.cs.
    private static Player CreatePlayer(Color color)
    {
        foreach (var constructor in typeof(Player)
                     .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .OrderBy(candidate => candidate.GetParameters().Length))
        {
            try
            {
                var arguments = constructor.GetParameters()
                    .Select(parameter => CreateArgument(parameter.ParameterType, color))
                    .ToArray();
                var player = (Player)constructor.Invoke(arguments);
                SetColorMember(player, color);
                return player;
            }
            catch
            {
                // Coba konstruktor lain; fallback di bawah menangani model
                // yang tidak menyediakan konstruktor yang dapat dipanggil.
            }
        }

#pragma warning disable SYSLIB0050
        var uninitialized = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
#pragma warning restore SYSLIB0050
        SetColorMember(uninitialized, color);
        return uninitialized;
    }

    private static object? CreateArgument(Type type, Color color)
    {
        if (type == typeof(Color)) return color;
        if (type == typeof(string)) return color.ToString();
        if (type.IsValueType) return Activator.CreateInstance(type);
        return null;
    }

    private static void SetColorMember(Player player, Color color)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var property = typeof(Player).GetProperty("PlayerColor", flags);
        if (property?.SetMethod != null)
        {
            property.SetValue(player, color);
            return;
        }

        var field = typeof(Player).GetField("PlayerColor", flags)
                    ?? typeof(Player).GetField("_playerColor", flags)
                    ?? typeof(Player).GetField("<PlayerColor>k__BackingField", flags);

        if (field == null)
            throw new InvalidOperationException("Member Player.PlayerColor tidak ditemukan.");

        field.SetValue(player, color);
    }

    private static void SetPrivateField<T>(ChessConsole2.Game game, string fieldName, T value)
    {
        var field = typeof(ChessConsole2.Game).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"Field private {fieldName} tidak ditemukan.");
        field!.SetValue(game, value);
    }
    
}
    
    