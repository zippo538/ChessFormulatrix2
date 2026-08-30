export type ChessColor = 'White' | 'Black';

export type PieceName =
  | 'Pawn'
  | 'Knight'
  | 'Bishop'
  | 'Rook'
  | 'Queen'
  | 'King';

export type PositionDto = {
  row: number;
  column: number;
};

export type ChessPiece = {
  color: ChessColor;
  type: PieceName;
};

export type BoardState = {
  position: Record<string, { pieceType: string }>;
  pieces: Record<string, ChessPiece>;
};

export type MoveDto = {
  from: PositionDto;
  to: PositionDto;
  promotionType?: PieceName;
};

export type MoveResponse = {
  gameOver: boolean;
  currentTurn?: ChessColor;
  reason?: string;
  winner?: string;
};

export type HistoryEntry = {
  from: PositionDto;
  to: PositionDto;
  color?: ChessColor;
  piece?: PieceName;
  capturedPiece?: PieceName;
  moveType?: string;
  promotionType?: PieceName;
};

export type BoardPalette = {
  name: string;
  light: string;
  dark: string;
  accent: string;
};
