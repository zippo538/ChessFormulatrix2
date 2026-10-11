using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Model;

namespace ChessFormulatrix.Tests.Helper;

public class BoardGameHelper
{
    
    
    public static Board PrepareCleanBoardWithKings(ChessFormularix.Console.Game game)
    {
        var board = game.GetCurrentBoard();

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
    public static void ClearBoard(Board board)
    {
        foreach (var tile in board.Tiles)
            tile.ChessPiece = null;
    }
}