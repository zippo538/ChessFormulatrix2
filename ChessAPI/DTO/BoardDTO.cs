namespace ChessWeb.DTO;

public class BoardDTO
{
    public IEnumerable<TileDto> Tiles { get; set; } = new List<TileDto>();
    public int Size { get; set; }
}