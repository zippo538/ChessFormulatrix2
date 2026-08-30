import { Clock3, Play, Sparkles } from 'lucide-react';

type GameSetupProps = {
  open: boolean;
  whitePlayerName: string;
  blackPlayerName: string;
  minutes: number;
  increment: number;
  loading: boolean;
  error?: string;
  onWhitePlayerNameChange: (value: string) => void;
  onBlackPlayerNameChange: (value: string) => void;
  onMinutesChange: (value: number) => void;
  onIncrementChange: (value: number) => void;
  onSubmit: () => void;
};

const timePresets = [3, 5, 10, 15, 30];

export function GameSetup({
                            open,
                            whitePlayerName,
                            blackPlayerName,
                            minutes,
                            increment,
                            loading,
                            error,
                            onWhitePlayerNameChange,
                            onBlackPlayerNameChange,
                            onMinutesChange,
                            onIncrementChange,
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
            Dua pemain bermain bergantian. Mesin game memvalidasi setiap pilihan bidak dan langkah.
          </p>

          <label className="field">
            <span>Nama pemain Putih</span>
            <input
                value={whitePlayerName}
                onChange={(event) => onWhitePlayerNameChange(event.target.value)}
                placeholder="Pemain Putih"
                maxLength={40}
                autoFocus
            />
          </label>

          <label className="field">
            <span>Nama pemain Hitam</span>
            <input
                value={blackPlayerName}
                onChange={(event) => onBlackPlayerNameChange(event.target.value)}
                placeholder="Pemain Hitam"
                maxLength={40}
            />
          </label>

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
            <div className="setup-time__grid">
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
              <label className="field field--compact">
                <span>Increment / langkah</span>
                <div className="input-suffix">
                  <input
                      type="number"
                      min={0}
                      max={60}
                      value={increment}
                      onChange={(event) => onIncrementChange(Number(event.target.value))}
                  />
                  <span>detik</span>
                </div>
              </label>
            </div>
          </fieldset>

          {error && <p className="form-error" role="alert">{error}</p>}

          <button className="primary-button primary-button--wide" type="button" onClick={onSubmit} disabled={loading}>
            {loading ? <Sparkles className="spin" size={18} /> : <Play size={18} fill="currentColor" />}
            {loading ? 'Menyiapkan papan…' : 'Mulai pertandingan'}
          </button>
          <p className="setup-dialog__footnote">Pilih bidak, lihat petak legal, lalu klik atau tarik ke tujuan.</p>
        </section>
      </div>
  );
}
