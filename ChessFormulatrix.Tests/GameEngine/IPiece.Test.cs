using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Interface;

namespace ChessFormulatrix.Tests.GameEngine;

[TestFixture]
public class PieceTest
{
    [Test]
    public void IPiece_WhenCreated_ImplementsIPiece()
    {
        IPiece piece = new DummyPiece(
            Color.Black,
            PieceType.Queen);

        Assert.Multiple(() =>
        {
            Assert.That(piece, Is.InstanceOf<IPiece>());
            Assert.That(piece.PieceColor, Is.EqualTo(Color.Black));
            Assert.That(piece.Piece, Is.EqualTo(PieceType.Queen));
            Assert.That(piece.HasMoved, Is.False);
        });
    }

    [Test]
    public void IPiece_HasMovedCanBeChanged()
    {
        IPiece piece = new DummyPiece(
            Color.White,
            PieceType.Pawn);

        piece.HasMoved = true;

        Assert.That(piece.HasMoved, Is.True);
    }

    [TestCase(Color.White, PieceType.King)]
    [TestCase(Color.White, PieceType.Queen)]
    [TestCase(Color.White, PieceType.Rook)]
    [TestCase(Color.Black, PieceType.Bishop)]
    [TestCase(Color.Black, PieceType.Knight)]
    [TestCase(Color.Black, PieceType.Pawn)]
    public void IPiece_WithDifferentTypes_ReturnsExpectedProperties(
        Color color,
        PieceType type)
    {
        IPiece piece = new DummyPiece(color, type);

        Assert.Multiple(() =>
        {
            Assert.That(piece.PieceColor, Is.EqualTo(color));
            Assert.That(piece.Piece, Is.EqualTo(type));
        });
    }
}

public sealed class DummyPiece(
    Color pieceColor,
    PieceType type,
    bool hasMoved = false) : IPiece
{
    public Color PieceColor { get; } = pieceColor;
    public PieceType Piece { get; } = type;

    public bool HasMoved { get; set; } = hasMoved;
}