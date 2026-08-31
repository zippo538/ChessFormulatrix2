import { Clock3 } from 'lucide-react';
import { formatClock } from '../lib/chess';

type ChessClockProps = {
  milliseconds: number;
  active: boolean;
  label: string;
};

export function ChessClock({ milliseconds, active, label }: ChessClockProps) {
  const isLow = milliseconds <= 30_000;
  return (
      <div
          className={`chess-clock${active ? ' chess-clock--active' : ''}${
              isLow ? ' chess-clock--low' : ''
          }`}
          aria-label={`${label}: ${formatClock(milliseconds)}`}
          aria-live={active ? 'polite' : 'off'}
          role="timer"
      >
        <Clock3 size={16} strokeWidth={2.2} aria-hidden="true" />
        <span>{formatClock(milliseconds)}</span>
      </div>
  );
}
