using ChessFormularix.Console.Interface;
using ChessFormularix.Console.Model;

namespace ChessFormularix.Console.Helper;

public class BoardHelper
{
    public static Board Copy(Board originalBoard)
    {
        var newBoard = new Board(); 

        for (int i = 0; i < 64; i++) 
        {
            var originalPiece = originalBoard.Tiles[i].ChessPiece;
            
            if (originalPiece != null)
            {
                newBoard.Tiles[i].ChessPiece = new Pieces(originalPiece.PieceColor, originalPiece.Piece)
                {
                    HasMoved = originalPiece.HasMoved 
                };
            }
        }

        return newBoard;
    }
    
    public static IPiece? GetPiece(Board board, int row, int col)
    {
        var tile = GetTile(board, row, col);
        return tile?.ChessPiece;
    }
    
    
    public static Tile? GetTile(Board board, int row, int col)
    {
        if (!IsInBounds(board, row, col)) return null;
    
        return board.Tiles[row * 8 + col];
    }
    public static bool IsInBounds(Board board, int row, int col)
    {
        return row >= 0 && row < board.Size && col >= 0 && col < board.Size;
    }
}