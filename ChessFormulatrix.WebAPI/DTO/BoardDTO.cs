namespace ChessFormulatrix.WebAPI.DTO;

public class BoardDto
{
    public IEnumerable<TileDto> Tiles { get; set; } = new List<TileDto>();
    public int Size { get; set; }
}