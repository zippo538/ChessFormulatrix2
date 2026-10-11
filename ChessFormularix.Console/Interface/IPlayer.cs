using ChessFormularix.Console.Enums;

namespace ChessFormularix.Console.Interface;

public interface IPlayer
{ 
     string Name { get; set; }
     Color PlayerColor { get; }
}