namespace ChessWeb.DTO;

public class BoardDTO
{
    // Memetakan array Tiles dan Size dari model Board[cite: 15]
    public IEnumerable<TileDto> Tiles { get; set; } = new List<TileDto>();
    public int Size { get; set; }
}