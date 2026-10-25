import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Chessboard } from 'react-chessboard';
import {
  Bot,
  ChevronRight,
  CircleHelp,
  Flag,
  Globe,
  History,
  Lightbulb,
  LoaderCircle,
  Menu,
  Plus,
  RefreshCw,
  ShieldCheck,
  UserRound,
  WifiOff,
  X,
} from 'lucide-react';
import * as signalR from '@microsoft/signalr';
import { ApiError, chessApi, colorLabel } from './api/chessApi';
import type { TimeStatusResponse } from './api/chessApi';
import { createSignalRConnection } from './api/signalr';
import { BoardCustomizer } from './components/BoardCustomizer';
import { ChessClock } from './components/ChessClock';
import { GameSetup } from './components/GameSetup';
import { LobbyModal } from './components/LobbyModal';
import {
  formatMove,
  normalizeBoard,
  opposite,
  positionToSquare,
  squareToPosition,
} from './lib/chess';
import { boardPalettes } from './lib/palettes';
import type {
  BoardPalette,
  BoardState,
  ChessColor,
  GameMode,
  HistoryEntry,
  LobbyRoomDto,
  MoveDto,
  PieceName,
} from './types/chess';

const INITIAL_POSITION = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR';
const promotionPieces = ['Queen', 'Rook', 'Bishop', 'Knight'] as const;
const promotionSymbols: Record<(typeof promotionPieces)[number], string> = {
  Queen: '♕',
  Rook: '♖',
  Bishop: '♗',
  Knight: '♘',
};

type GamePhase = 'setup' | 'playing' | 'over';
type GameResult = { reason: string; winner: string };
type PendingPromotion = { from: string; to: string };

function loadStoredPalette(): BoardPalette {
  try {
    const value = localStorage.getItem('chess-arena-palette');
    if (value) return JSON.parse(value) as BoardPalette;
  } catch {
  }
  return boardPalettes[0];
}

function getMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error) return error.message;
  return 'Terjadi kesalahan yang tidak diketahui.';
}

export default function App() {
  const [phase, setPhase] = useState<GamePhase>('setup');
  const [setupOpen, setSetupOpen] = useState(true);
  const [gameId, setGameId] = useState<string>();
  const [playerName, setPlayerName] = useState('Pemain Putih');
  const [mode, setMode] = useState<GameMode>('bot');
  const [blackName, setBlackName] = useState('Pemain Hitam');
  const isBotOpponent = mode === 'bot';
  const isOnlineMode = mode === 'online';
  const [myColor, setMyColor] = useState<ChessColor>('White');

  const [minutes, setMinutes] = useState(10);
  const [whiteMs, setWhiteMs] = useState(10 * 60_000);
  const [blackMs, setBlackMs] = useState(10 * 60_000);
  const [currentTurn, setCurrentTurn] = useState<ChessColor>('White');
  const [board, setBoard] = useState<BoardState>({ position: {}, pieces: {} });
  const [history, setHistory] = useState<HistoryEntry[]>([]);
  const [selectedSquare, setSelectedSquare] = useState<string>();
  const [validMoves, setValidMoves] = useState<string[]>([]);
  const [suggestedMove, setSuggestedMove] = useState<MoveDto>();
  const [pendingPromotion, setPendingPromotion] = useState<PendingPromotion>();
  const [result, setResult] = useState<GameResult>();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('Mulai game untuk mengaktifkan papan.');
  const [setupError, setSetupError] = useState<string>();
  const [apiOffline, setApiOffline] = useState(false);
  const [confirmResign, setConfirmResign] = useState(false);
  const [mobilePanel, setMobilePanel] = useState(false);
  const [palette, setPalette] = useState<BoardPalette>(loadStoredPalette);

  // Lobby & SignalR states
  const [lobbyOpen, setLobbyOpen] = useState(false);
  const [rooms, setRooms] = useState<LobbyRoomDto[]>([]);
  const [currentRoom, setCurrentRoom] = useState<LobbyRoomDto | null>(null);
  const [lobbyError, setLobbyError] = useState<string>();
  const [lobbyBusy, setLobbyBusy] = useState(false);

  const lobbyHubRef = useRef<signalR.HubConnection | null>(null);
  const gameHubRef = useRef<signalR.HubConnection | null>(null);

  const phaseRef = useRef<GamePhase>(phase);
  const clocksRef = useRef({ white: whiteMs, black: blackMs });
  const timeoutSyncStartedRef = useRef(false);

  useEffect(() => {
    phaseRef.current = phase;
  }, [phase]);

  const finishGame = useCallback((reason?: string, winner?: string) => {
    phaseRef.current = 'over';
    setPhase('over');
    setResult({
      reason: reason || 'Permainan selesai',
      winner: winner === 'White' || winner === 'Black' ? colorLabel(winner) : winner || 'Seri',
    });
  }, []);

  const applyTimeStatus = useCallback(
    (status: TimeStatusResponse) => {
      const white = Math.max(0, status.whiteRemainingSeconds * 1000);
      const black = Math.max(0, status.blackRemainingSeconds * 1000);
      clocksRef.current = { white, black };
      setWhiteMs(white);
      setBlackMs(black);
      if (status.currentTurn) setCurrentTurn(status.currentTurn);

      if (status.gameOver) {
        finishGame(status.reason, status.winner);
        if (status.timeExpired && status.timedOutPlayer) {
          setNotice(`Waktu ${colorLabel(status.timedOutPlayer)} habis.`);
        } else {
          setNotice('Pertandingan selesai.');
        }
      }
    },
    [finishGame],
  );

  const syncTimeStatus = useCallback(
    async (id: string, signal?: AbortSignal) => {
      const status = await chessApi.getTimeStatus(id, signal);
      applyTimeStatus(status);
      return status;
    },
    [applyTimeStatus],
  );

  async function loadSnapshot(id: string): Promise<BoardState> {
    const [boardPayload, moveHistory] = await Promise.all([
      chessApi.getBoard(id),
      chessApi.getHistory(id),
    ]);
    const nextBoard = normalizeBoard(boardPayload);
    setBoard(nextBoard);
    setHistory(moveHistory);
    return nextBoard;
  }

  // Connect & listen to LobbyHub
  useEffect(() => {
    if (!lobbyOpen) {
      if (lobbyHubRef.current) {
        lobbyHubRef.current.stop().catch(() => {});
        lobbyHubRef.current = null;
      }
      return;
    }

    const hub = createSignalRConnection('/hubs/lobby');
    lobbyHubRef.current = hub;

    hub.on('RoomListUpdated', (updatedRooms: LobbyRoomDto[]) => {
      setRooms(updatedRooms || []);
    });

    hub.on('RoomCreated', (room: LobbyRoomDto) => {
      setCurrentRoom(room);
      setLobbyError(undefined);
    });

    hub.on('PlayerJoined', (data: { roomId: string; guestName: string }) => {
      setCurrentRoom((prev) => (prev ? { ...prev, guestName: data.guestName, isFull: true } : prev));
      setNotice(`Pemain ${data.guestName} telah bergabung! Memulai game...`);
    });

    hub.on('PlayerLeft', () => {
      setCurrentRoom((prev) => (prev ? { ...prev, guestName: null, isFull: false } : prev));
      setNotice('Lawan telah meninggalkan kamar.');
    });

    hub.on('RoomClosed', (msg: string) => {
      setCurrentRoom(null);
      setLobbyError(msg);
    });

    hub.on('GameStarted', (match: { gameId: string; whitePlayer: string; blackPlayer: string; minutes: number; clockEnabled: boolean }) => {
      setLobbyOpen(false);
      setSetupOpen(false);
      setMode('online');
      setGameId(match.gameId);

      const amIHost = playerName.trim().toLowerCase() === match.whitePlayer.toLowerCase();
      setMyColor(amIHost ? 'White' : 'Black');
      setPlayerName(match.whitePlayer);
      setBlackName(match.blackPlayer);

      const initialTime = match.minutes * 60_000;
      clocksRef.current = { white: initialTime, black: initialTime };
      setWhiteMs(initialTime);
      setBlackMs(initialTime);
      setMinutes(match.minutes);
      setCurrentTurn('White');
      setHistory([]);
      setResult(undefined);
      setSelectedSquare(undefined);
      setValidMoves([]);
      setSuggestedMove(undefined);

      phaseRef.current = 'playing';
      setPhase('playing');
      setNotice(`Game dimulai! Anda bermain sebagai ${amIHost ? 'Putih' : 'Hitam'}.`);

      void loadSnapshot(match.gameId);
    });

    hub.on('Error', (err: string) => {
      setLobbyError(err);
      setLobbyBusy(false);
    });

    setLobbyBusy(true);
    hub
      .start()
      .then(() => {
        setLobbyBusy(false);
        return hub.invoke('GetRooms');
      })
      .catch((err) => {
        setLobbyBusy(false);
        setLobbyError(`Gagal terhubung ke lobi: ${getMessage(err)}`);
      });

    return () => {
      hub.stop().catch(() => {});
    };
  }, [lobbyOpen, playerName]);

  // Connect to GameHub when in online mode playing
  useEffect(() => {
    if (!gameId || mode !== 'online' || phase !== 'playing') {
      if (gameHubRef.current) {
        gameHubRef.current.stop().catch(() => {});
        gameHubRef.current = null;
      }
      return;
    }

    const hub = createSignalRConnection('/hubs/game');
    gameHubRef.current = hub;

    hub.on('BoardUpdated', (boardPayload: unknown) => {
      const next = normalizeBoard(boardPayload);
      setBoard(next);
      void chessApi.getHistory(gameId).then(setHistory).catch(() => {});
    });

    hub.on('MoveMade', (moveInfo: { from: { row: number; column: number }; to: { row: number; column: number }; currentTurn: ChessColor }) => {
      if (moveInfo.currentTurn) setCurrentTurn(moveInfo.currentTurn);
      setNotice(`Langkah dilakukan: ${positionToSquare(moveInfo.from)} → ${positionToSquare(moveInfo.to)}.`);
      if (gameId) void loadSnapshot(gameId);
    });

    hub.on('GameOver', (res: { reason: string; winner: string }) => {
      finishGame(res.reason, res.winner);
    });

    hub.on('MoveRejected', (reason: string) => {
      setNotice(`Langkah ditolak: ${reason}`);
      setBusy(false);
    });

    hub
      .start()
      .then(() => hub.invoke('JoinGame', gameId))
      .catch((err) => {
        setNotice(`Koneksi Game WebSocket bermasalah: ${getMessage(err)}`);
      });

    return () => {
      hub.stop().catch(() => {});
    };
  }, [finishGame, gameId, mode, phase]);

  useEffect(() => {
    try {
      localStorage.setItem('chess-arena-palette', JSON.stringify(palette));
    } catch {
    }
  }, [palette]);

  // Clock countdown interval
  useEffect(() => {
    if (phase !== 'playing' || mode === 'practice') return;

    timeoutSyncStartedRef.current = false;
    const controller = new AbortController();
    let previous = Date.now();
    const timer = window.setInterval(() => {
      const now = Date.now();
      const elapsed = now - previous;
      previous = now;
      const key = currentTurn === 'White' ? 'white' : 'black';
      const next = Math.max(0, clocksRef.current[key] - elapsed);
      clocksRef.current[key] = next;
      if (key === 'white') setWhiteMs(next);
      else setBlackMs(next);

      if (next === 0 && gameId && !timeoutSyncStartedRef.current) {
        timeoutSyncStartedRef.current = true;
        void (async () => {
          try {
            const status = await syncTimeStatus(gameId, controller.signal);
            if (controller.signal.aborted) return;

            setApiOffline(false);
            if (!status.gameOver) {
              timeoutSyncStartedRef.current = false;
            }
          } catch (error) {
            if (controller.signal.aborted) return;
            setApiOffline(true);
            setNotice(`Gagal mengonfirmasi waktu habis: ${getMessage(error)}`);
          }
        })();
      }
    }, 200);

    return () => {
      window.clearInterval(timer);
      controller.abort();
    };
  }, [currentTurn, gameId, mode, phase, syncTimeStatus]);

  const historyRows = useMemo(() => {
    const rows: Array<{ number: number; white?: HistoryEntry; black?: HistoryEntry }> = [];
    history.forEach((entry, index) => {
      const rowIndex = Math.floor(index / 2);
      rows[rowIndex] ||= { number: rowIndex + 1 };
      if (entry.color === 'Black' || (entry.color === undefined && index % 2 === 1)) {
        rows[rowIndex].black = entry;
      } else {
        rows[rowIndex].white = entry;
      }
    });
    return rows;
  }, [history]);

  const lastMove = history.at(-1);
  const squareStyles = useMemo(() => {
    const styles: Record<string, React.CSSProperties> = {};
    if (lastMove) {
      styles[positionToSquare(lastMove.from)] = {
        boxShadow: `inset 0 0 0 999px ${palette.accent}3d`,
      };
      styles[positionToSquare(lastMove.to)] = {
        boxShadow: `inset 0 0 0 999px ${palette.accent}5c`,
      };
    }
    if (selectedSquare) {
      styles[selectedSquare] = {
        boxShadow: `inset 0 0 0 4px ${palette.accent}`,
      };
    }
    validMoves.forEach((square) => {
      const isCapture = Boolean(board.pieces[square]);
      styles[square] = {
        ...styles[square],
        background: isCapture
          ? `radial-gradient(transparent 58%, ${palette.accent} 60%, ${palette.accent} 71%, transparent 73%)`
          : `radial-gradient(${palette.accent} 0 14%, transparent 16%)`,
      };
    });
    return styles;
  }, [board.pieces, lastMove, palette.accent, selectedSquare, validMoves]);

  async function playMove(fromSquare: string, toSquare: string, promotionType?: PieceName) {
    if (!gameId || busy || phase !== 'playing') return;
    setBusy(true);
    setSuggestedMove(undefined);

    // If online mode, send move via GameHub WebSocket
    if (isOnlineMode && gameHubRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await gameHubRef.current.invoke(
          'MakeMove',
          gameId,
          squareToPosition(fromSquare),
          squareToPosition(toSquare),
          promotionType || null,
        );
        setSelectedSquare(undefined);
        setValidMoves([]);
      } catch (err) {
        setNotice(`Gagal mengirim langkah: ${getMessage(err)}`);
      } finally {
        setBusy(false);
        setPendingPromotion(undefined);
      }
      return;
    }

    try {
      const movingColor = currentTurn;
      const response = await chessApi.makeMove(gameId, {
        from: squareToPosition(fromSquare),
        to: squareToPosition(toSquare),
        promotionType,
      });
      setSelectedSquare(undefined);
      setValidMoves([]);
      await loadSnapshot(gameId);
      if (mode !== 'practice') await syncTimeStatus(gameId);
      setApiOffline(false);
      if (response.gameOver) {
        finishGame(response.reason, response.winner);
        setNotice('Pertandingan selesai.');
        return;
      }

      const nextTurn = response.currentTurn || opposite(movingColor);
      setCurrentTurn(nextTurn);
      if (isBotOpponent && response.botMove) {
        setNotice(
          `AI Bot membalas: ${positionToSquare(response.botMove.from)} → ${positionToSquare(response.botMove.to)}.`,
        );
      } else {
        setNotice(`Giliran ${colorLabel(nextTurn)}.`);
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 409 && gameId) {
        try {
          await syncTimeStatus(gameId);
          setApiOffline(false);
        } catch (syncError) {
          setApiOffline(true);
          setNotice(getMessage(syncError));
        }
      } else {
        setApiOffline(true);
        setNotice(getMessage(error));
      }
    } finally {
      setBusy(false);
      setPendingPromotion(undefined);
    }
  }

  function prepareMove(fromSquare: string, toSquare: string) {
    const movingPiece = board.pieces[fromSquare];
    const to = squareToPosition(toSquare);
    if (movingPiece?.type === 'Pawn' && (to.row === 0 || to.row === 7)) {
      setPendingPromotion({ from: fromSquare, to: toSquare });
      return;
    }
    void playMove(fromSquare, toSquare);
  }

  async function selectSquare(square: string) {
    if (!gameId || busy || phase !== 'playing') return;
    if (isBotOpponent && currentTurn !== 'White') return;
    if (isOnlineMode && currentTurn !== myColor) return;

    if (selectedSquare && validMoves.includes(square)) {
      prepareMove(selectedSquare, square);
      return;
    }
    const piece = board.pieces[square];
    if (!piece || piece.color !== currentTurn) {
      setSelectedSquare(undefined);
      setValidMoves([]);
      setSuggestedMove(undefined);
      return;
    }
    try {
      const moves = await chessApi.getValidMoves(gameId, squareToPosition(square));
      setSelectedSquare(square);
      setValidMoves(moves.map(positionToSquare));
      setSuggestedMove(undefined);
      setNotice(
        moves.length > 0
          ? `${moves.length} langkah legal untuk ${piece.type}.`
          : 'Bidak ini tidak memiliki langkah legal.',
      );
      setApiOffline(false);
    } catch (error) {
      setNotice(getMessage(error));
      setValidMoves([]);
    }
  }

  async function handleDrop(sourceSquare: string, targetSquare: string | null) {
    if (!targetSquare || !gameId || phase !== 'playing' || busy) return;
    if (isBotOpponent && currentTurn !== 'White') return;
    if (isOnlineMode && currentTurn !== myColor) return;

    let moves = validMoves;
    if (selectedSquare !== sourceSquare) {
      try {
        const response = await chessApi.getValidMoves(gameId, squareToPosition(sourceSquare));
        moves = response.map(positionToSquare);
        setSelectedSquare(sourceSquare);
        setValidMoves(moves);
      } catch (error) {
        setNotice(getMessage(error));
        return;
      }
    }
    if (moves.includes(targetSquare)) prepareMove(sourceSquare, targetSquare);
    else setNotice('Tujuan tersebut bukan langkah legal.');
  }

  async function requestSuggestion() {
    if (!gameId || !selectedSquare || busy) return;
    setBusy(true);
    try {
      const move = await chessApi.getSuggestion(gameId, squareToPosition(selectedSquare));
      if (!move) throw new Error('Tidak ada saran untuk bidak ini.');
      setSuggestedMove(move);
      setNotice(`Saran mesin: ${positionToSquare(move.from)} → ${positionToSquare(move.to)}.`);
    } catch (error) {
      setNotice(getMessage(error));
    } finally {
      setBusy(false);
    }
  }

  async function startGame() {
    const safeName = playerName.trim();
    if (mode !== 'practice' && !safeName) {
      setSetupError('Nama pemain Putih harus diisi.');
      return;
    }
    const safeBlackName = blackName.trim();
    if (mode === 'pvp' && !safeBlackName) {
      setSetupError('Nama pemain Hitam harus diisi.');
      return;
    }
    const safeMinutes = Math.min(180, Math.max(1, Number(minutes) || 10));
    setBusy(true);
    setSetupError(undefined);
    try {
      const id = await chessApi.createGame({
        whiteName: safeName || 'Anda',
        blackName: mode === 'pvp' ? safeBlackName : undefined,
        vsBot: mode === 'bot',
        minutes: safeMinutes,
        clockEnabled: mode !== 'practice',
      });
      setGameId(id);
      setMyColor('White');
      const initialTime = safeMinutes * 60_000;
      clocksRef.current = { white: initialTime, black: initialTime };
      setWhiteMs(initialTime);
      setBlackMs(initialTime);
      setMinutes(safeMinutes);
      setCurrentTurn('White');
      setHistory([]);
      setResult(undefined);
      setSelectedSquare(undefined);
      setValidMoves([]);
      setSuggestedMove(undefined);
      await loadSnapshot(id);
      if (mode !== 'practice') {
        await syncTimeStatus(id);
      }
      phaseRef.current = 'playing';
      setPhase('playing');
      setSetupOpen(false);
      setApiOffline(false);
      setNotice('Giliran Anda. Pilih bidak Putih.');
    } catch (error) {
      setApiOffline(true);
      setSetupError(`${getMessage(error)} Pastikan Chess API sedang berjalan.`);
    } finally {
      setBusy(false);
    }
  }

  async function resign() {
    if (!gameId) return;
    setConfirmResign(false);
    setBusy(true);

    if (isOnlineMode && gameHubRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await gameHubRef.current.invoke('Resign', gameId);
      } catch (err) {
        setNotice(getMessage(err));
      } finally {
        setBusy(false);
      }
      return;
    }

    try {
      const response = await chessApi.resign(gameId);
      finishGame(response.reason || 'Resigned', response.winner || 'Black');
      setNotice('Anda menyerah. Pertandingan telah berakhir.');
      setApiOffline(false);
    } catch (error) {
      setApiOffline(true);
      setNotice(getMessage(error));
    } finally {
      setBusy(false);
    }
  }

  function newGame() {
    if (gameId && phase === 'playing') void chessApi.resign(gameId).catch(() => undefined);
    phaseRef.current = 'setup';
    setPhase('setup');
    setSetupOpen(true);
    setResult(undefined);
    setMobilePanel(false);
  }

  // Lobby Hub Actions
  async function handleCreateRoom(roomName: string, roomMinutes: number, roomClockEnabled: boolean) {
    if (!lobbyHubRef.current) return;
    setLobbyError(undefined);
    try {
      await lobbyHubRef.current.invoke('CreateRoom', playerName.trim(), roomName, roomMinutes, roomClockEnabled);
    } catch (err) {
      setLobbyError(`Gagal membuat kamar: ${getMessage(err)}`);
    }
  }

  async function handleJoinRoom(roomId: string) {
    if (!lobbyHubRef.current) return;
    setLobbyError(undefined);
    try {
      await lobbyHubRef.current.invoke('JoinRoom', roomId, playerName.trim());
    } catch (err) {
      setLobbyError(`Gagal bergabung: ${getMessage(err)}`);
    }
  }

  async function handleLeaveRoom() {
    if (!lobbyHubRef.current) return;
    try {
      await lobbyHubRef.current.invoke('LeaveRoom');
      setCurrentRoom(null);
    } catch (err) {
      setLobbyError(`Gagal meninggalkan kamar: ${getMessage(err)}`);
    }
  }

  async function handleRefreshRooms() {
    if (!lobbyHubRef.current) return;
    try {
      await lobbyHubRef.current.invoke('GetRooms');
    } catch (err) {
      setLobbyError(`Gagal memuat kamar: ${getMessage(err)}`);
    }
  }

  const arrows = suggestedMove
    ? [
        {
          startSquare: positionToSquare(suggestedMove.from),
          endSquare: positionToSquare(suggestedMove.to),
          color: palette.accent,
        },
      ]
    : [];

  const boardOptions = {
    id: 'chess-arena-board',
    position: Object.keys(board.position).length ? board.position : INITIAL_POSITION,
    boardOrientation: (isOnlineMode && myColor === 'Black' ? 'black' : 'white') as 'white' | 'black',
    lightSquareStyle: { backgroundColor: palette.light },
    darkSquareStyle: { backgroundColor: palette.dark },
    squareStyles,
    boardStyle: {
      borderRadius: '3px',
      boxShadow: '0 24px 70px rgba(5, 19, 16, .28)',
    },
    animationDurationInMs: 220,
    showNotation: true,
    allowDrawingArrows: true,
    arrows,
    canDragPiece: ({ piece }: { piece: { pieceType: string } }) => {
      if (phase !== 'playing' || busy) return false;
      if (isOnlineMode) {
        return (
          currentTurn === myColor &&
          (myColor === 'White' ? piece.pieceType.startsWith('w') : piece.pieceType.startsWith('b'))
        );
      }
      return isBotOpponent
        ? currentTurn === 'White' && piece.pieceType.startsWith('w')
        : (currentTurn === 'White' && piece.pieceType.startsWith('w')) ||
            (currentTurn === 'Black' && piece.pieceType.startsWith('b'));
    },
    onPieceDrag: ({ square }: { square: string | null }) => {
      if (square) void selectSquare(square);
    },
    onPieceDrop: ({ sourceSquare, targetSquare }: { sourceSquare: string; targetSquare: string | null }) => {
      void handleDrop(sourceSquare, targetSquare);
      return false;
    },
    onSquareClick: ({ square }: { square: string }) => void selectSquare(square),
  };

  return (
    <div className="app-shell" style={{ '--board-accent': palette.accent } as React.CSSProperties}>
      <header className="topbar">
        <a className="brand" href="#top" aria-label="Chess Arena">
          <span className="brand__mark">♞</span>
          <span>
            <strong>CHESS</strong>
            <em> FORMUALTRIX</em>
          </span>
        </a>
        <div className="topbar__status">
          <button
            className="topbar-lobby-button"
            type="button"
            onClick={() => setLobbyOpen(true)}
          >
            <Globe size={16} /> Lobi Online
          </button>
          <span className={apiOffline ? 'api-state api-state--offline' : 'api-state'}>
            {apiOffline ? <WifiOff size={14} /> : <ShieldCheck size={14} />}
            {apiOffline ? 'API terputus' : gameId ? 'Sesi aktif' : 'Siap bermain'}
          </span>
          <button className="icon-button mobile-menu" type="button" onClick={() => setMobilePanel(true)} aria-label="Buka panel permainan">
            <Menu size={20} />
          </button>
          <button className="new-game-button" type="button" onClick={newGame}>
            <Plus size={17} /> Game baru
          </button>
        </div>
      </header>

      <main className="arena" id="top">
        <section className="board-column" aria-label="Arena permainan">
          <div className="match-meta">
            <div>
              <span className="live-dot" />
              {phase === 'playing' ? `Giliran ${colorLabel(currentTurn)}` : phase === 'over' ? 'Pertandingan selesai' : 'Menunggu game'}
            </div>
            <span>
              {mode === 'practice'
                ? 'Latihan • tanpa batas waktu'
                : mode === 'online'
                ? `Online 2P • ${minutes} menit (${myColor === 'White' ? 'Anda Putih' : 'Anda Hitam'})`
                : `${minutes} menit • ${mode === 'bot' ? 'vs AI' : 'vs Pemain'}`}
            </span>
          </div>

          {result && (
            <div className="result-banner" role="status">
              <div>
                <span>Hasil pertandingan</span>
                <strong>{result.winner === 'Seri' ? 'Seri' : `${result.winner} menang`}</strong>
                <small>{result.reason}</small>
              </div>
              <button type="button" onClick={newGame}>Main lagi <ChevronRight size={17} /></button>
            </div>
          )}

          {/* Top Player Strip (Black) */}
          <div className={currentTurn === 'Black' && phase === 'playing' ? 'player-strip player-strip--active' : 'player-strip'}>
            <div className={isBotOpponent ? 'avatar avatar--bot' : 'avatar'}>
              {isBotOpponent ? <Bot size={21} /> : <UserRound size={21} />}
            </div>
            <div className="player-strip__identity">
              <strong>{isBotOpponent ? 'AI Bot' : blackName || 'Pemain Hitam'}</strong>
              <span>
                {isOnlineMode && myColor === 'Black' ? 'Hitam • Anda' : isBotOpponent && busy ? 'Sedang berpikir…' : 'Hitam'}
              </span>
            </div>
            {mode !== 'practice' && (
              <ChessClock milliseconds={blackMs} active={currentTurn === 'Black' && phase === 'playing'} label="Waktu Hitam" />
            )}
          </div>

          <div className={`board-frame${busy ? ' board-frame--busy' : ''}`}>
            <Chessboard options={boardOptions} />
            {busy && (
              <div className="board-busy" aria-live="polite">
                <LoaderCircle className="spin" size={24} />
              </div>
            )}
          </div>

          {/* Bottom Player Strip (White) */}
          <div className={currentTurn === 'White' && phase === 'playing' ? 'player-strip player-strip--active' : 'player-strip'}>
            <div className="avatar"><UserRound size={21} /></div>
            <div className="player-strip__identity">
              <strong>{playerName || 'Pemain Putih'}</strong>
              <span>
                {isOnlineMode && myColor === 'White' ? 'Putih • Anda' : isBotOpponent ? 'Putih • Anda' : 'Putih'}
              </span>
            </div>
            {mode !== 'practice' && (
              <ChessClock milliseconds={whiteMs} active={currentTurn === 'White' && phase === 'playing'} label="Waktu Putih" />
            )}
          </div>

          <div className="notice-line" aria-live="polite">
            <CircleHelp size={15} />
            <span>{notice}</span>
          </div>
        </section>

        <aside className={mobilePanel ? 'side-panel side-panel--open' : 'side-panel'}>
          <div className="side-panel__mobile-head">
            <strong>Panel pertandingan</strong>
            <button className="icon-button" type="button" onClick={() => setMobilePanel(false)} aria-label="Tutup panel">
              <X size={20} />
            </button>
          </div>

          <section className="move-panel" aria-labelledby="history-heading">
            <div className="panel-heading panel-heading--spaced">
              <span><History size={17} /><h2 id="history-heading">Riwayat langkah</h2></span>
              <small>{history.length} ply</small>
            </div>
            <div className="moves-table" role="table" aria-label="Riwayat langkah catur">
              <div className="moves-table__head" role="row">
                <span>#</span><span>Putih</span><span>Hitam</span>
              </div>
              {historyRows.length === 0 ? (
                <div className="empty-history">
                  <span>♙</span>
                  <p>Langkah pertama akan muncul di sini.</p>
                </div>
              ) : (
                <div className="moves-table__body">
                  {historyRows.map((row) => (
                    <div className="moves-table__row" role="row" key={row.number}>
                      <span>{row.number}.</span>
                      <span>{row.white ? formatMove(row.white) : '—'}</span>
                      <span>{row.black ? formatMove(row.black) : '—'}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </section>

          <BoardCustomizer value={palette} onChange={setPalette} />

          <section className="panel-section panel-actions" aria-label="Aksi permainan">
            <button type="button" className="secondary-button" onClick={requestSuggestion} disabled={!selectedSquare || busy || phase !== 'playing'}>
              <Lightbulb size={17} /> Saran langkah
            </button>
            <button type="button" className="danger-button" onClick={() => setConfirmResign(true)} disabled={phase !== 'playing' || busy}>
              <Flag size={17} /> Menyerah
            </button>
            <button type="button" className="text-button" onClick={() => gameId && loadSnapshot(gameId)} disabled={!gameId || busy}>
              <RefreshCw size={15} /> Sinkronkan papan
            </button>
          </section>
        </aside>
        {mobilePanel && <button className="panel-scrim" type="button" onClick={() => setMobilePanel(false)} aria-label="Tutup panel" />}
      </main>

      <GameSetup
        open={setupOpen}
        name={playerName}
        mode={mode}
        blackName={blackName}
        minutes={minutes}
        loading={busy}
        error={setupError}
        onNameChange={setPlayerName}
        onModeChange={setMode}
        onBlackNameChange={setBlackName}
        onMinutesChange={setMinutes}
        onOpenLobby={() => {
          setSetupOpen(false);
          setLobbyOpen(true);
        }}
        onSubmit={startGame}
      />

      <LobbyModal
        open={lobbyOpen}
        playerName={playerName}
        rooms={rooms}
        currentRoom={currentRoom}
        loading={lobbyBusy}
        error={lobbyError}
        onPlayerNameChange={setPlayerName}
        onCreateRoom={handleCreateRoom}
        onJoinRoom={handleJoinRoom}
        onLeaveRoom={handleLeaveRoom}
        onRefreshRooms={handleRefreshRooms}
        onClose={() => setLobbyOpen(false)}
      />

      {pendingPromotion && (
        <div className="modal-backdrop modal-backdrop--top" role="presentation">
          <section className="small-dialog" role="dialog" aria-modal="true" aria-labelledby="promotion-title">
            <p className="eyebrow">Promosi pion</p>
            <h2 id="promotion-title">Pilih bidak baru</h2>
            <div className="promotion-grid">
              {promotionPieces.map((piece) => (
                <button key={piece} type="button" onClick={() => playMove(pendingPromotion.from, pendingPromotion.to, piece)}>
                  <span aria-hidden="true">{promotionSymbols[piece]}</span>
                  {piece}
                </button>
              ))}
            </div>
            <button className="text-button" type="button" onClick={() => setPendingPromotion(undefined)}>Batal</button>
          </section>
        </div>
      )}

      {confirmResign && (
        <div className="modal-backdrop modal-backdrop--top" role="presentation">
          <section className="small-dialog" role="alertdialog" aria-modal="true" aria-labelledby="resign-title">
            <div className="dialog-icon dialog-icon--danger"><Flag size={22} /></div>
            <h2 id="resign-title">Menyerah sekarang?</h2>
            <p>
              {isBotOpponent ? 'AI Bot' : blackName || 'Pemain Hitam'} akan dinyatakan menang dan sesi ini
              berakhir.
            </p>
            <div className="dialog-actions">
              <button className="secondary-button" type="button" onClick={() => setConfirmResign(false)}>Lanjut bermain</button>
              <button className="danger-button danger-button--solid" type="button" onClick={resign}>Ya, menyerah</button>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
