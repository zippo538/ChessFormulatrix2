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
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

const apiBase = (import.meta.env.VITE_CHESS_API_URL || '/api/chess').replace(/\/$/, '');
const hubUrl =
    import.meta.env.VITE_CHESS_HUB_URL ||
    `${apiBase.startsWith('http') ? new URL(apiBase).origin : window.location.origin}/Hubs/ChessHub`;

export type RealtimeBoardUpdate = {
  gameId?: string;
  board?: unknown;
  currentTurn?: ChessColor;
  lastMove?: MoveDto;
  whiteMs?: number;
  blackMs?: number;
  serverTimeUtc?: string;
};

export type RealtimeClockUpdate = {
  gameId?: string;
  currentTurn?: ChessColor;
  whiteMs?: number;
  blackMs?: number;
  serverTimeUtc?: string;
};

export type RealtimeGameOver = RealtimeClockUpdate & {
  reason?: string;
  winner?: string;
};

type RealtimeHandlers = {
  onBoardUpdated: (payload: RealtimeBoardUpdate) => void;
  onClockUpdated: (payload: RealtimeClockUpdate) => void;
  onGameOver: (payload: RealtimeGameOver) => void;
  onConnectionChanged?: (connected: boolean) => void;
};

function readNumber(value: unknown, keys: string[]): number | undefined {
  if (!value || typeof value !== 'object') return undefined;
  const record = value as Record<string, unknown>;
  for (const key of keys) {
    const candidate = record[key];
    if (typeof candidate === 'number' && Number.isFinite(candidate)) return candidate;
  }
  return undefined;
}

function normalizeRealtimeClock(payload: unknown): RealtimeClockUpdate {
  const currentTurn = readString(payload, ['currentTurn', 'CurrentTurn']);
  return {
    gameId: readString(payload, ['gameId', 'GameId']),
    currentTurn: currentTurn ? normalizeColor(currentTurn) : undefined,
    whiteMs: readNumber(payload, ['whiteMs', 'WhiteMs', 'whiteMilliseconds', 'WhiteMilliseconds']),
    blackMs: readNumber(payload, ['blackMs', 'BlackMs', 'blackMilliseconds', 'BlackMilliseconds']),
    serverTimeUtc: readString(payload, ['serverTimeUtc', 'ServerTimeUtc']),
  };
}

function normalizeRealtimeBoard(payload: unknown): RealtimeBoardUpdate {
  const value = payload && typeof payload === 'object' ? (payload as Record<string, unknown>) : {};
  return {
    ...normalizeRealtimeClock(payload),
    board: value.board ?? value.Board,
    lastMove: normalizeMove(value.lastMove ?? value.LastMove),
  };
}

export function createChessRealtime(gameId: string, handlers: RealtimeHandlers) {
  const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
      .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
      .build();

  const joinGame = async () => {
    if (connection.state === HubConnectionState.Connected) {
      await connection.invoke('JoinGame', gameId);
      handlers.onConnectionChanged?.(true);
    }
  };

  connection.on('BoardUpdated', (payload: unknown) => handlers.onBoardUpdated(normalizeRealtimeBoard(payload)));
  // Mendukung kedua nama event agar kompatibel dengan kontrak backend lama/baru.
  connection.on('ClockUpdated', (payload: unknown) => handlers.onClockUpdated(normalizeRealtimeClock(payload)));
  connection.on('TimeUpdated', (payload: unknown) => handlers.onClockUpdated(normalizeRealtimeClock(payload)));
  connection.on('GameOver', (payload: unknown) => {
    handlers.onGameOver({
      ...normalizeRealtimeClock(payload),
      reason: readString(payload, ['reason', 'Reason']),
      winner: readString(payload, ['winner', 'Winner']),
    });
  });
  connection.onreconnecting(() => handlers.onConnectionChanged?.(false));
  connection.onreconnected(() => void joinGame().catch(() => handlers.onConnectionChanged?.(false)));
  connection.onclose(() => handlers.onConnectionChanged?.(false));

  return {
    async start() {
      if (connection.state === HubConnectionState.Disconnected) await connection.start();
      await joinGame();
    },
    async stop() {
      if (connection.state !== HubConnectionState.Disconnected) await connection.stop();
    },
  };
}

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
  return {
    gameOver: readBoolean(payload, ['gameOver', 'GameOver']),
    currentTurn: normalizeColor(readString(payload, ['currentTurn', 'CurrentTurn'])),
    reason: readString(payload, ['reason', 'Reason']),
    winner: readString(payload, ['winner', 'Winner']),
  };
}

export const chessApi = {
  async createGame(name: string, minutes: number): Promise<string> {
    const params = new URLSearchParams({ minutes: String(minutes) });
    const data = await request(`?${params}`, {
      method: 'POST',
      body: JSON.stringify({ name }),
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
    const data = await request(`/${gameId}/moves`, {
      method: 'POST',
      body: JSON.stringify({
        from: { row: move.from.row, column: move.from.column },
        to: { row: move.to.row, column: move.to.column },
        promotionType: move.promotionType || 'Queen',
      }),
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
