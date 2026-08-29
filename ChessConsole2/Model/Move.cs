using ChessConsole2.Enums;

namespace ChessConsole2.Model;

public struct Move
{
    public Postition From { get; set; }
    public Postition To { get; set; }
    public PieceType PromptionType { get; set; }
    public MoveType Type { get; set; }
    
}