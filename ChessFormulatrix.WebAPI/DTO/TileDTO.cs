namespace ChessFormulatrix.WebAPI.DTO;

public class TileDto
{
    // Memetakan ChessPiece dan TilePosition dari model Tile[cite: 12]
    public PieceDto? ChessPiece { get; set; }
    public PosititionDto TilePosition { get; set; } = null!;
}