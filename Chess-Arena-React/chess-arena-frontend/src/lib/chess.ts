import type {
  BoardState,
  ChessColor,
  HistoryEntry,
  MoveDto,
  PieceName,
  PositionDto,
} from '../types/chess';

type UnknownRecord = Record<string, unknown>;

const files = 'abcdefgh';
const pieceCodes: Record<PieceName, string> = {
  Pawn: 'P',
  Knight: 'N',
  Bishop: 'B',
  Rook: 'R',
  Queen: 'Q',
  King: 'K',
};

const pieceSymbols: Record<PieceName, string> = {
  Pawn: '♟',
  Knight: '♞',
  Bishop: '♝',
  Rook: '♜',
  Queen: '♛',
  King: '♚',
};

const enumOrder = (
    import.meta.env.VITE_PIECE_ENUM_ORDER ||
    'Pawn,Knight,Bishop,Rook,Queen,King'
)
    .split(',')
    .map((value: string) => value.trim());

function record(value: unknown): UnknownRecord {
  return value && typeof value === 'object' ? (value as UnknownRecord) : {};
}

function first(source: UnknownRecord, keys: string[]): unknown {
  for (const key of keys) {
    if (source[key] !== undefined && source[key] !== null) return source[key];
  }
  return undefined;
}

export function normalizeColor(value: unknown): ChessColor | undefined {
  if (value === 0 || String(value).toLowerCase() === 'white') return 'White';
  if (value === 1 || String(value).toLowerCase() === 'black') return 'Black';
  return undefined;
}

export function normalizePiece(value: unknown): PieceName | undefined {
  let candidate = value;
  if (typeof value === 'number') candidate = enumOrder[value] ?? enumOrder[value - 1];
  const normalized = String(candidate ?? '').toLowerCase();
  const names: PieceName[] = ['Pawn', 'Knight', 'Bishop', 'Rook', 'Queen', 'King'];
  return names.find((name) => name.toLowerCase() === normalized);
}

export function positionToSquare(position: PositionDto): string {
  return `${files[position.column]}${position.row + 1}`;
}

export function squareToPosition(square: string): PositionDto {
  return {
    row: Number(square[1]) - 1,
    column: files.indexOf(square[0].toLowerCase()),
  };
}

export function normalizePosition(value: unknown): PositionDto | undefined {
  const source = record(value);
  const row = Number(first(source, ['row', 'Row']));
  const column = Number(first(source, ['column', 'Column', 'col', 'Col']));
  if (!Number.isInteger(row) || !Number.isInteger(column)) return undefined;
  if (row < 0 || row > 7 || column < 0 || column > 7) return undefined;
  return { row, column };
}

export function normalizeBoard(payload: unknown): BoardState {
  const source = record(payload);
  const rawTiles = first(source, ['tiles', 'Tiles']);
  const tiles = Array.isArray(rawTiles)
      ? rawTiles
      : Array.isArray(payload)
          ? payload
          : [];
  const position: BoardState['position'] = {};
  const pieces: BoardState['pieces'] = {};

  tiles.forEach((rawTile, index) => {
    const tile = record(rawTile);
    const rawPiece = first(tile, ['chessPiece', 'ChessPiece', 'piece', 'Piece']);
    if (!rawPiece) return;
    const piece = record(rawPiece);
    const color = normalizeColor(
        first(piece, ['pieceColor', 'PieceColor', 'color', 'Color']),
    );
    const type = normalizePiece(
        first(piece, ['type', 'Type', 'pieceType', 'PieceType']),
    );
    if (!color || !type) return;

    const tilePosition =
        normalizePosition(first(tile, ['position', 'Position'])) ??
        normalizePosition(tile) ?? {
          row: Math.floor(index / 8),
          column: index % 8,
        };
    const square = positionToSquare(tilePosition);
    pieces[square] = { color, type };
    position[square] = {
      pieceType: `${color === 'White' ? 'w' : 'b'}${pieceCodes[type]}`,
    };
  });

  return { position, pieces };
}

export function normalizeMove(payload: unknown): MoveDto | undefined {
  const source = record(payload);
  const nestedMove = record(first(source, ['move', 'Move']));
  const move = Object.keys(nestedMove).length ? nestedMove : source;
  const from = normalizePosition(first(move, ['from', 'From']));
  const to = normalizePosition(first(move, ['to', 'To']));
  if (!from || !to) return undefined;
  return {
    from,
    to,
    promotionType: normalizePiece(
        first(move, ['promotionType', 'PromotionType', 'promptionType', 'PromptionType']),
    ),
  };
}

export function normalizeHistory(payload: unknown): HistoryEntry[] {
  if (!Array.isArray(payload)) return [];
  return payload.flatMap((rawEntry) => {
    const entry = record(rawEntry);
    const move = normalizeMove(first(entry, ['move', 'Move']) ?? entry);
    if (!move) return [];
    return [
      {
        ...move,
        color: normalizeColor(first(entry, ['pieceColor', 'PieceColor', 'color', 'Color'])),
        piece: normalizePiece(first(entry, ['pieceType', 'PieceType', 'piece', 'Piece'])),
        capturedPiece: normalizePiece(
            first(entry, ['capturedPieceType', 'CapturedPieceType']),
        ),
        moveType: String(first(entry, ['moveType', 'MoveType', 'type', 'Type']) ?? ''),
      },
    ];
  });
}

export function formatMove(entry: HistoryEntry): string {
  const piece = entry.piece ? pieceSymbols[entry.piece] : '•';
  const capture = entry.capturedPiece ? '×' : '–';
  const promotion = entry.promotionType ? `=${pieceSymbols[entry.promotionType]}` : '';
  return `${piece} ${positionToSquare(entry.from)}${capture}${positionToSquare(entry.to)}${promotion}`;
}

export function formatClock(milliseconds: number): string {
  const safe = Math.max(0, milliseconds);
  const totalSeconds = Math.ceil(safe / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  if (hours > 0) {
    return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  }
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function opposite(color: ChessColor): ChessColor {
  return color === 'White' ? 'Black' : 'White';
}

export function readBoolean(source: unknown, keys: string[]): boolean {
  const value = first(record(source), keys);
  return value === true || String(value).toLowerCase() === 'true';
}

export function readString(source: unknown, keys: string[]): string | undefined {
  const value = first(record(source), keys);
  return value === undefined ? undefined : String(value);
}

export function readNumber(source: unknown, keys: string[]): number | undefined {
  const value = first(record(source), keys);
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}
