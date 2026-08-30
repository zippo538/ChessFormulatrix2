namespace ChessWeb.DTO;

public class TileDto
{
    // Memetakan ChessPiece dan TilePosition dari model Tile[cite: 12]
    public PieceDto? ChessPiece { get; set; }
    public PosititionDTO TilePosition { get; set; } = null!;
}