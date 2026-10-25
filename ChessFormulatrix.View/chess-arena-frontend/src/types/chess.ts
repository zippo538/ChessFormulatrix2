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
// - 'online'   = multiplayer online 2 pemain via SignalR lobby.
export type GameMode = 'practice' | 'bot' | 'pvp' | 'online';

export type LobbyRoomDto = {
  roomId: string;
  roomName: string;
  hostName: string;
  guestName?: string | null;
  minutes: number;
  clockEnabled: boolean;
  isFull: boolean;
  isStarted: boolean;
  createdAt: string;
};

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