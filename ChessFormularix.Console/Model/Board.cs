namespace ChessConsole.Model;

public class Board
{
    private const int DEFAULT_SIZE = 64;
    public Tile[] Tiles { get; set; }
    public int Size { get; }

    public Board(int size = DEFAULT_SIZE)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
        Tiles = new Tile[size];
        for (int i = 0; i < size; i++)
        {
            Tiles[i] = new Tile
            {
                TilePosition = new Postition { Row = i / 8, Column = i % 8 }
            };
        }
    }
}
