using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Model;

namespace ChessFormulatrix.Tests.GameEngine;

[TestFixture]   
public class GameConstructorTests
{
    private Player? _dummyWhite = null;
    private Player? _dummyBlack = null;

    [SetUp]
    public void Setup()
    {
        _dummyWhite = new Player("mahindra", Color.White);
        _dummyBlack = new Player("Saputra", Color.Black);
    }

    [TestCase(0, 1)]
    [TestCase(-50, 1)]
    [TestCase(10, 10)]
    [TestCase(180, 180)]
    [TestCase(200, 180)]
    public void Constructor_InitialMinutes_ShouldBeClampedCorrectly(int minutes, int expectedMinutes)
    {
        var game = new ChessFormularix.Console.Game(_dummyWhite, _dummyBlack, initialMinutes:minutes);
        var timeStatus = game.CheckTimeStatus();
        
        Assert.That(timeStatus.WhiteRemaining, Is.EqualTo(TimeSpan.FromMinutes(expectedMinutes)));
        Assert.That(timeStatus.BlackRemaining, Is.EqualTo(TimeSpan.FromMinutes(expectedMinutes)));
    }

    [Test]
    public void Constructor_DefaultParameters_ShouldInitalizedCorrectly()
    {
        var game = new ChessFormularix.Console.Game(_dummyWhite, _dummyBlack);
        
        Assert.That(game.WhitePlayer, Is.EqualTo(_dummyWhite));
        Assert.That(game.BlackPlayer, Is.EqualTo(_dummyBlack));
        Assert.IsTrue(game.VsBot);

        var board = game.GetCurrentBoard();
        Assert.IsNotNull(board);

        var pieceAtA1 = board.Tiles[0].ChessPiece;
        Assert.IsNotNull(pieceAtA1);
        Assert.That(pieceAtA1.Piece, Is.EqualTo(PieceType.Rook));
        Assert.That(pieceAtA1.PieceColor, Is.EqualTo(Color.White));
    }

    [Test]
    public void Constructor_NullWhitePlayer_ShouldThrowException()
    {
        Player? invalidWhitePlayer = null;
        
        var exception = Assert.Throws<ArgumentNullException>(()=> new ChessFormularix.Console.Game(invalidWhitePlayer, _dummyBlack));
        
        Assert.That(exception.ParamName, Is.EqualTo("WhitePlayer"));
        
    }
    [Test]
    public void Constructor_NullBlackPlayer_ShouldThrowException()
    {
        Player? invalidBlackPlayer = null;
        
        var exception = Assert.Throws<ArgumentNullException>(()=> new ChessFormularix.Console.Game(_dummyWhite, invalidBlackPlayer));
        
        Assert.That(exception.ParamName, Is.EqualTo("BlackPlayer"));
    }
    
    

}