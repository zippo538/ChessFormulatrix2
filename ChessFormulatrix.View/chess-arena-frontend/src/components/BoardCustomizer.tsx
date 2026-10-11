import { Check, Palette } from 'lucide-react';
import { boardPalettes } from '../lib/palettes';
import type { BoardPalette } from '../types/chess';

type BoardCustomizerProps = {
  value: BoardPalette;
  onChange: (palette: BoardPalette) => void;
};

export function BoardCustomizer({ value, onChange }: BoardCustomizerProps) {
  return (
    <section className="panel-section" aria-labelledby="board-color-heading">
      <div className="panel-heading">
        <Palette size={17} />
        <h2 id="board-color-heading">Warna papan</h2>
      </div>
      <div className="palette-presets">
        {boardPalettes.map((palette) => {
          const active = palette.light === value.light && palette.dark === value.dark;
          return (
            <button
              type="button"
              key={palette.name}
              className={active ? 'palette-chip palette-chip--active' : 'palette-chip'}
              onClick={() => onChange(palette)}
              aria-pressed={active}
              title={`Tema ${palette.name}`}
            >
              <span className="palette-chip__colors" aria-hidden="true">
                <i style={{ background: palette.light }} />
                <i style={{ background: palette.dark }} />
              </span>
              <span>{palette.name}</span>
              {active && <Check size={14} />}
            </button>
          );
        })}
      </div>
      <div className="color-controls">
        <label>
          <span>Petak terang</span>
          <input
            type="color"
            value={value.light}
            onChange={(event) => onChange({ ...value, name: 'Custom', light: event.target.value })}
          />
        </label>
        <label>
          <span>Petak gelap</span>
          <input
            type="color"
            value={value.dark}
            onChange={(event) => onChange({ ...value, name: 'Custom', dark: event.target.value })}
          />
        </label>
        <label>
          <span>Aksen</span>
          <input
            type="color"
            value={value.accent}
            onChange={(event) => onChange({ ...value, name: 'Custom', accent: event.target.value })}
          />
        </label>
      </div>
    </section>
  );
}
