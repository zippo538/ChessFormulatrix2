using ChessConsole.Enums;

namespace ChessConsole.Model;

public struct Move
{
    public Postition From { get; set; }
    public Postition To { get; set; }
    public PieceType PromptionType { get; set; }
    public MoveType Type { get; set; }
    
}