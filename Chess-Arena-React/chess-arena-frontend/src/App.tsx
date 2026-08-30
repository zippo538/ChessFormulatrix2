import { useEffect, useMemo, useRef, useState } from 'react';
import { Chessboard } from 'react-chessboard';
import {
  Bot,
  ChevronRight,
  CircleHelp,
  Flag,
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
import { ApiError, chessApi, colorLabel } from './api/chessApi';
import { BoardCustomizer } from './components/BoardCustomizer';
import { ChessClock } from './components/ChessClock';
import { GameSetup } from './components/GameSetup';
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
    // A private browser context can block localStorage; the default remains usable.
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
  const [minutes, setMinutes] = useState(10);
  const [increment, setIncrement] = useState(0);
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

  const phaseRef = useRef<GamePhase>(phase);
  const clocksRef = useRef({ white: whiteMs, black: blackMs });

  useEffect(() => {
    phaseRef.current = phase;
  }, [phase]);

  useEffect(() => {
    try {
      localStorage.setItem('chess-arena-palette', JSON.stringify(palette));
    } catch {
      // The selected colors still work for this session.
    }
  }, [palette]);

  useEffect(() => {
    if (phase !== 'playing') return;
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

      if (next === 0 && phaseRef.current === 'playing') {
        phaseRef.current = 'over';
        setResult({ reason: 'Timeout', winner: colorLabel(opposite(currentTurn)) });
        setNotice(`Waktu ${colorLabel(currentTurn)} habis.`);
        setPhase('over');
      }
    }, 200);
    return () => window.clearInterval(timer);
  }, [phase, currentTurn]);

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

  function addIncrement(color: ChessColor) {
    if (increment <= 0) return;
    const amount = increment * 1000;
    if (color === 'White') {
      const next = clocksRef.current.white + amount;
      clocksRef.current.white = next;
      setWhiteMs(next);
    } else {
      const next = clocksRef.current.black + amount;
      clocksRef.current.black = next;
      setBlackMs(next);
    }
  }

  function finishGame(reason?: string, winner?: string) {
    phaseRef.current = 'over';
    setPhase('over');
    setResult({
      reason: reason || 'Permainan selesai',
      winner: winner === 'White' || winner === 'Black' ? colorLabel(winner) : winner || 'Seri',
    });
  }

  // Catatan: dulu ada logika AI client-side (findAiMove/playAiTurn) yang
  // memilih langkah Hitam dengan memanggil /suggestion berulang kali per
  // bidak. Itu sudah tidak diperlukan lagi: untuk mode 'bot', backend kini
  // otomatis membalas dengan engine Minimax + Alpha-Beta miliknya sendiri
  // di dalam satu request POST /moves (lihat ChessController.MakeMove &
  // Game.MakeBotMove), dan mengirim balik langkah bot lewat `botMove`.

  async function playMove(fromSquare: string, toSquare: string, promotionType: PieceName = 'Queen') {
    if (!gameId || busy || phase !== 'playing') return;
    setBusy(true);
    setSuggestedMove(undefined);
    try {
      const movingColor = currentTurn;
      const response = await chessApi.makeMove(gameId, {
        from: squareToPosition(fromSquare),
        to: squareToPosition(toSquare),
        promotionType,
      });
      addIncrement(movingColor);
      if (mode === 'bot' && response.botMove) addIncrement('Black');
      setSelectedSquare(undefined);
      setValidMoves([]);
      await loadSnapshot(gameId);
      setApiOffline(false);
      if (response.gameOver) {
        finishGame(response.reason, response.winner);
        setNotice('Pertandingan selesai.');
        return;
      }

      const nextTurn = response.currentTurn || opposite(movingColor);
      setCurrentTurn(nextTurn);
      if (mode === 'bot' && response.botMove) {
        setNotice(
          `AI Bot membalas: ${positionToSquare(response.botMove.from)} → ${positionToSquare(response.botMove.to)}.`,
        );
      } else {
        setNotice(`Giliran ${colorLabel(nextTurn)}.`);
      }
    } catch (error) {
      setApiOffline(true);
      setNotice(getMessage(error));
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
    if (mode === 'bot' && currentTurn !== 'White') return;
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
    if (mode === 'bot' && currentTurn !== 'White') return;
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
    if (!safeName) {
      setSetupError('Nama pemain Putih harus diisi.');
      return;
    }
    const safeBlackName = blackName.trim();
    if (mode === 'pvp' && !safeBlackName) {
      setSetupError('Nama pemain Hitam harus diisi.');
      return;
    }
    const safeMinutes = Math.min(180, Math.max(1, Number(minutes) || 10));
    const safeIncrement = Math.min(60, Math.max(0, Number(increment) || 0));
    setBusy(true);
    setSetupError(undefined);
    try {
      const id = await chessApi.createGame({
        whiteName: safeName,
        blackName: mode === 'pvp' ? safeBlackName : undefined,
        vsBot: mode === 'bot',
      });
      setGameId(id);
      const initialTime = safeMinutes * 60_000;
      clocksRef.current = { white: initialTime, black: initialTime };
      setWhiteMs(initialTime);
      setBlackMs(initialTime);
      setMinutes(safeMinutes);
      setIncrement(safeIncrement);
      setCurrentTurn('White');
      setHistory([]);
      setResult(undefined);
      setSelectedSquare(undefined);
      setValidMoves([]);
      setSuggestedMove(undefined);
      await loadSnapshot(id);
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
    boardOrientation: 'white' as const,
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
    canDragPiece: ({ piece }: { piece: { pieceType: string } }) =>
      phase === 'playing' &&
      !busy &&
      (mode === 'pvp'
        ? (currentTurn === 'White' && piece.pieceType.startsWith('w')) ||
          (currentTurn === 'Black' && piece.pieceType.startsWith('b'))
        : currentTurn === 'White' && piece.pieceType.startsWith('w')),
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
            <em>ARENA</em>
          </span>
        </a>
        <div className="topbar__status">
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
            <span>{minutes}+{increment} • {mode === 'bot' ? 'vs AI' : 'vs Pemain'}</span>
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

          <div className={currentTurn === 'Black' && phase === 'playing' ? 'player-strip player-strip--active' : 'player-strip'}>
            <div className={mode === 'bot' ? 'avatar avatar--bot' : 'avatar'}>
              {mode === 'bot' ? <Bot size={21} /> : <UserRound size={21} />}
            </div>
            <div className="player-strip__identity">
              <strong>{mode === 'bot' ? 'AI Bot' : blackName || 'Pemain Hitam'}</strong>
              <span>{mode === 'bot' && busy ? 'Sedang berpikir…' : 'Hitam'}</span>
            </div>
            <ChessClock milliseconds={blackMs} active={currentTurn === 'Black' && phase === 'playing'} label="Waktu Hitam" />
          </div>

          <div className={`board-frame${busy ? ' board-frame--busy' : ''}`}>
            <Chessboard options={boardOptions} />
            {busy && (
              <div className="board-busy" aria-live="polite">
                <LoaderCircle className="spin" size={24} />
              </div>
            )}
          </div>

          <div className={currentTurn === 'White' && phase === 'playing' ? 'player-strip player-strip--active' : 'player-strip'}>
            <div className="avatar"><UserRound size={21} /></div>
            <div className="player-strip__identity">
              <strong>{playerName || 'Pemain Putih'}</strong>
              <span>{mode === 'bot' ? 'Putih • Anda' : 'Putih'}</span>
            </div>
            <ChessClock milliseconds={whiteMs} active={currentTurn === 'White' && phase === 'playing'} label="Waktu Putih" />
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
        increment={increment}
        loading={busy}
        error={setupError}
        onNameChange={setPlayerName}
        onModeChange={setMode}
        onBlackNameChange={setBlackName}
        onMinutesChange={setMinutes}
        onIncrementChange={setIncrement}
        onSubmit={startGame}
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
              {mode === 'bot' ? 'AI Bot' : blackName || 'Pemain Hitam'} akan dinyatakan menang dan sesi ini
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