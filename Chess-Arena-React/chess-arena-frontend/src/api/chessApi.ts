import {
  normalizeColor,
  normalizeHistory,
  normalizeMove,
  normalizePosition,
  readBoolean,
  readString,
} from '../lib/chess';
import type {
  ChessColor,
  HistoryEntry,
  MoveDto,
  MoveResponse,
  PositionDto,
} from '../types/chess';

const apiBase = (import.meta.env.VITE_CHESS_API_URL || '/api/chess').replace(/\/$/, '');

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

async function request(path = '', init?: RequestInit): Promise<unknown> {
  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  });
  const text = await response.text();
  let data: unknown = text;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = text;
    }
  }
  if (!response.ok) {
    const message =
      typeof data === 'string'
        ? data
        : readString(data, ['message', 'Message', 'title', 'Title']) ||
          `Permintaan gagal (${response.status}).`;
    throw new ApiError(message, response.status);
  }
  return data;
}

function normalizeMoveResponse(payload: unknown): MoveResponse {
  const source = payload && typeof payload === 'object' ? (payload as Record<string, unknown>) : {};
  const rawBotMove = source['botMove'] ?? source['BotMove'];
  return {
    gameOver: readBoolean(payload, ['gameOver', 'GameOver']),
    currentTurn: normalizeColor(readString(payload, ['currentTurn', 'CurrentTurn'])),
    reason: readString(payload, ['reason', 'Reason']),
    winner: readString(payload, ['winner', 'Winner']),
    botMove: normalizeMove(rawBotMove),
  };
}

export type CreateGameOptions = {
  whiteName: string;
  // Nama pemain Hitam untuk mode player-vs-player. Diabaikan backend saat vsBot true.
  blackName?: string;
  // true = lawan AI Bot (default), false = player vs player lokal.
  vsBot: boolean;
};

export const chessApi = {
  async createGame(options: CreateGameOptions): Promise<string> {
    const data = await request('', {
      method: 'POST',
      body: JSON.stringify({
        whiteName: options.whiteName,
        blackName: options.blackName,
        vsBot: options.vsBot,
      }),
    });
    const gameId = readString(data, ['gameId', 'GameId']);
    if (!gameId) throw new ApiError('GameId tidak ditemukan pada respons API.', 500);
    return gameId;
  },

  getBoard(gameId: string): Promise<unknown> {
    return request(`/${gameId}/board`);
  },

  async getValidMoves(gameId: string, position: PositionDto): Promise<PositionDto[]> {
    const params = new URLSearchParams({
      row: String(position.row),
      col: String(position.column),
    });
    const data = await request(`/${gameId}/valid-moves?${params}`);
    return Array.isArray(data)
      ? data.map(normalizePosition).filter((value): value is PositionDto => Boolean(value))
      : [];
  },

  async makeMove(gameId: string, move: MoveDto): Promise<MoveResponse> {
      const body: Record<string, unknown> = {
        from: { row: move.from.row, column: move.from.column },
        to: { row: move.to.row, column: move.to.column },
      };
      // Hanya disertakan saat memang ada pilihan promosi (pion mencapai ujung
      // papan). Untuk langkah biasa, field ini tidak dikirim sama sekali --
      // biar backend yang menentukan default Queen HANYA jika promosi terjadi.
      if (move.promotionType) {
        body.promotionType = move.promotionType;
      }
      const data = await request(`/${gameId}/moves`, {
        method: 'POST',
        body: JSON.stringify(body),
      });
      return normalizeMoveResponse(data);
    },

  async getSuggestion(gameId: string, position: PositionDto): Promise<MoveDto | undefined> {
    const params = new URLSearchParams({
      row: String(position.row),
      col: String(position.column),
    });
    const data = await request(`/${gameId}/suggestion?${params}`);
    return normalizeMove(data);
  },

  async resign(gameId: string): Promise<MoveResponse> {
    const data = await request(`/${gameId}/resign`, { method: 'POST' });
    return normalizeMoveResponse(data);
  },

  async getHistory(gameId: string): Promise<HistoryEntry[]> {
    return normalizeHistory(await request(`/${gameId}/history`));
  },
};

export function colorLabel(color?: ChessColor): string {
  return color === 'Black' ? 'Hitam' : 'Putih';
}