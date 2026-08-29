using ChessConsole2.Enums;
using ChessConsole2.Interface;
using ChessConsole2.Model;

namespace ChessConsole2.Helper;

public class BoardHelper
{
    public static Board Copy(Board originalBoard)
    {
        // 1. Inisialisasi papan baru. 
        // Konstruktor akan otomatis membuat 64 objek Tile lengkap dengan Postition-nya.
        var newBoard = new Board(); 

        // 2. Iterasi array 1D untuk menyalin referensi bidak (Deep Clone)
        for (int i = 0; i < 64; i++) // Bisa juga menggunakan originalBoard.Tiles.Length
        {
            var originalPiece = originalBoard.Tiles[i].ChessPiece;
            
            if (originalPiece != null)
            {
                // 3. Buat instance Piece baru untuk memutus referensi memori dari papan asli
                newBoard.Tiles[i].ChessPiece = new Piece(originalPiece.PieceColor, originalPiece.Type)
                {
                    // Pastikan riwayat pergerakan (Hasmove) ikut tersalin
                    Hasmove = originalPiece.Hasmove 
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
    
        // Konversi koordinat 2D (row, col) menjadi indeks 1D
        return board.Tiles[row * 8 + col];
    }
    public static bool IsInBounds(Board board, int row, int col)
    {
        return row >= 0 && row < board.Size && col >= 0 && col < board.Size;
    }
}