using ChessConsole.Enums;
using ChessConsole.Model;

namespace Game.Tests;

[TestFixture]
public class MakeMoveTests
{
    private Player _whitePlayer = null!;
    private Player _blackPlayer = null!;
    private ChessConsole.Game _game = null!;

    [SetUp]
    public void Setup()
    {
        _whitePlayer = new Player("Mahindra", Color.White);
        _blackPlayer =  new Player("Saputra", Color.Black);
        
        _game = new ChessConsole.Game(_whitePlayer, _blackPlayer,vsBot:true);
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
        
        Assert.That(movedPiece.Type, Is.EqualTo(PieceType.Pawn));
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
        board.Tiles[6 * 8 + 3 ].ChessPiece = new Piece(Color.White, PieceType.Pawn);
        

        
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
        Assert.That(promotedPiece.Type, Is.EqualTo(PieceType.Queen));
    }
    
    
    //enpassant
    [Test]
    public void MakeMove_ValidWhitePawnEnpassant_ShouldBeCapturePawn()
{
    var board = PrepareCleanBoardWithKings();
    
    board.Tiles[1 * 8 + 4].ChessPiece = new Piece(Color.White, PieceType.Pawn); // White Pawn E2
    board.Tiles[6 * 8 + 4].ChessPiece = new Piece(Color.Black, PieceType.Pawn); // Black Pawn E7
    board.Tiles[6 * 8 + 3].ChessPiece = new Piece(Color.Black, PieceType.Pawn); // Black Pawn D7
  
    
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
        board.Tiles[0 * 8 + 0 ].ChessPiece = new Piece(Color.White, PieceType.Rook);
        
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
        
        Assert.That(movedPiece.Type, Is.EqualTo(PieceType.Rook));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
        
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
        Assert.That(_game.GetMoveHistory().Count, Is.EqualTo(1));
    }
    [Test]
    public void MakeMove_ValidWhiteKnightLShape_ShouldUpdateBoardAndSwitchTurn()
    {
        var board = PrepareCleanBoardWithKings();
        
        // Arrange: Taruh Kuda Putih di b1 (Row 0, Column 1)
        board.Tiles[0 * 8 + 1].ChessPiece = new Piece(Color.White, PieceType.Knight);
        
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
        Assert.That(movedPiece.Type, Is.EqualTo(PieceType.Knight));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
    }
    [Test]
    public void MakeMove_ValidWhiteBishopDiagonal_ShouldUpdateBoardAndSwitchTurn()
    {
        var board = PrepareCleanBoardWithKings();

        // Taruh Gajah Putih di c4 (Row 3, Column 2) - posisi terbuka tanpa halangan
        var fromPos = new Postition { Row = 3, Column = 2 };
        board.Tiles[3 * 8 + 2].ChessPiece = new Piece(Color.White, PieceType.Bishop);

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
        Assert.That(movedPiece.Type, Is.EqualTo(PieceType.Bishop));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
    }
    [Test]
    public void MakeMove_ValidWhiteQueenCaptureEnemy_ShouldUpdateBoardAndSwitchTurn()
    {
        var board = PrepareCleanBoardWithKings();

        // Taruh Menteri Putih di d3 (Row 2, Column 3)
        var fromPos = new Postition { Row = 2, Column = 3 };
        board.Tiles[2 * 8 + 3].ChessPiece = new Piece(Color.White, PieceType.Queen);

        // Taruh Benteng Hitam di d6 (Row 5, Column 3) untuk memicu capture
        board.Tiles[5 * 8 + 3].ChessPiece = new Piece(Color.Black, PieceType.Rook);

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
        Assert.That(movedPiece.Type, Is.EqualTo(PieceType.Queen));
        Assert.That(movedPiece.PieceColor, Is.EqualTo(Color.White));
    
        var lastHistory = _game.GetMoveHistory().LastOrDefault();
        Assert.IsNotNull(lastHistory);
        Assert.That(lastHistory.CapturedPieceType, Is.EqualTo(PieceType.Rook));
        Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
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
        board.Tiles[0 * 8 + 4].ChessPiece = new Piece(Color.White, PieceType.King); // e1
        board.Tiles[7 * 8 + 4].ChessPiece = new Piece(Color.Black, PieceType.King); // e8

        return board;
    }
}
    
    