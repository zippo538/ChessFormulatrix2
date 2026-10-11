import { Bot, Clock3, Play, Sparkles, UserRound, Zap } from 'lucide-react';
import type { GameMode } from '../types/chess';

type GameSetupProps = {
  open: boolean;
  name: string;
  mode: GameMode;
  blackName: string;
  minutes: number;
  loading: boolean;
  error?: string;
  onNameChange: (value: string) => void;
  onModeChange: (mode: GameMode) => void;
  onBlackNameChange: (value: string) => void;
  onMinutesChange: (value: number) => void;
  onSubmit: () => void;
};

const timePresets = [3, 5, 10, 15, 30];

export function GameSetup({
                            open,
                            name,
                            mode,
                            blackName,
                            minutes,
                            loading,
                            error,
                            onNameChange,
                            onModeChange,
                            onBlackNameChange,
                            onMinutesChange,
                            onSubmit,
                          }: GameSetupProps) {
  if (!open) return null;

  return (
      <div className="modal-backdrop" role="presentation">
        <section className="setup-dialog" role="dialog" aria-modal="true" aria-labelledby="setup-title">
          <div className="setup-dialog__mark" aria-hidden="true">♞</div>
          <p className="eyebrow">Pertandingan baru</p>
          <h1 id="setup-title">Masuk ke arena.</h1>
          <p className="setup-dialog__lead">
            {mode === 'practice'
                ? 'Latihan santai tanpa batas waktu dan tanpa lawan Bot. Anda bebas menggerakkan Putih maupun Hitam sendiri, tanpa perlu isi nama.'
                : mode === 'bot'
                    ? 'Anda memegang Putih. Lawan AI Bot membaca saran langkah langsung dari mesin game.'
                    : 'Anda memegang Putih. Mainkan bersama teman secara bergantian di perangkat yang sama.'}
          </p>

          <fieldset className="setup-time">
            <legend>Mode permainan</legend>
            <div className="time-presets" aria-label="Pilihan mode permainan">
              <button
                  className={mode === 'practice' ? 'time-preset time-preset--active' : 'time-preset'}
                  type="button"
                  onClick={() => onModeChange('practice')}
              >
                <Zap size={15} /> Latihan
              </button>
              <button
                  className={mode === 'bot' ? 'time-preset time-preset--active' : 'time-preset'}
                  type="button"
                  onClick={() => onModeChange('bot')}
              >
                <Bot size={15} /> Lawan Bot
              </button>
              <button
                  className={mode === 'pvp' ? 'time-preset time-preset--active' : 'time-preset'}
                  type="button"
                  onClick={() => onModeChange('pvp')}
              >
                <UserRound size={15} /> Lawan Pemain
              </button>
            </div>
          </fieldset>

          {mode !== 'practice' && (
              <label className="field">
                <span>Nama pemain Putih</span>
                <input
                    value={name}
                    onChange={(event) => onNameChange(event.target.value)}
                    placeholder="Contoh: Budi"
                    maxLength={40}
                    autoFocus
                />
              </label>
          )}

          {mode === 'pvp' && (
              <label className="field">
                <span>Nama pemain Hitam</span>
                <input
                    value={blackName}
                    onChange={(event) => onBlackNameChange(event.target.value)}
                    placeholder="Contoh: Siti"
                    maxLength={40}
                />
              </label>
          )}

          {mode !== 'practice' && (
              <fieldset className="setup-time">
                <legend><Clock3 size={16} /> Kontrol waktu</legend>
                <div className="time-presets" aria-label="Pilihan cepat menit">
                  {timePresets.map((preset) => (
                      <button
                          className={minutes === preset ? 'time-preset time-preset--active' : 'time-preset'}
                          key={preset}
                          type="button"
                          onClick={() => onMinutesChange(preset)}
                      >
                        {preset}m
                      </button>
                  ))}
                </div>
                <label className="field field--compact">
                  <span>Menit per pemain</span>
                  <input
                      type="number"
                      min={1}
                      max={180}
                      value={minutes}
                      onChange={(event) => onMinutesChange(Number(event.target.value))}
                  />
                </label>
              </fieldset>
          )}

          {error && <p className="form-error" role="alert">{error}</p>}

          <button className="primary-button primary-button--wide" type="button" onClick={onSubmit} disabled={loading}>
            {loading ? <Sparkles className="spin" size={18} /> : <Play size={18} fill="currentColor" />}
            {loading ? 'Menyiapkan papan…' : mode === 'practice' ? 'Mulai latihan' : 'Mulai pertandingan'}
          </button>
          <p className="setup-dialog__footnote">Pilih bidak, lihat petak legal, lalu klik atau tarik ke tujuan.</p>
        </section>
      </div>
  );
}
