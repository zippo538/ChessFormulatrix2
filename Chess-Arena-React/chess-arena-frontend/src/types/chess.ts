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

// Mode pertandingan:
// - 'practice' = latihan santai, tanpa batas waktu & tanpa perlu isi nama, selalu vs Bot.
// - 'bot'      = lawan Bot dengan kontrol waktu (seperti sebelumnya).
// - 'pvp'      = player vs player lokal (satu layar, bergantian).
export type GameMode = 'practice' | 'bot' | 'pvp';

export type MoveResponse = {
  gameOver: boolean;
  currentTurn?: ChessColor;
  reason?: string;
  winner?: string;
  // Terisi ketika backend otomatis membalas dengan langkah Bot (mode 'bot').
  botMove?: MoveDto;
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