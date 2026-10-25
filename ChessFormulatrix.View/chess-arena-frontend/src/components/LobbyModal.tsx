import { useState } from 'react';
import {
  Clock3,
  Copy,
  DoorOpen,
  LoaderCircle,
  Plus,
  RefreshCw,
  Users,
  Wifi,
  X,
  Check,
} from 'lucide-react';
import type { LobbyRoomDto } from '../types/chess';

type LobbyModalProps = {
  open: boolean;
  playerName: string;
  rooms: LobbyRoomDto[];
  currentRoom: LobbyRoomDto | null;
  loading: boolean;
  error?: string;
  onPlayerNameChange: (name: string) => void;
  onCreateRoom: (roomName: string, minutes: number, clockEnabled: boolean) => Promise<void>;
  onJoinRoom: (roomId: string) => Promise<void>;
  onLeaveRoom: () => Promise<void>;
  onRefreshRooms: () => Promise<void>;
  onClose: () => void;
};

const timePresets = [3, 5, 10, 15, 30];

export function LobbyModal({
  open,
  playerName,
  rooms,
  currentRoom,
  loading,
  error,
  onPlayerNameChange,
  onCreateRoom,
  onJoinRoom,
  onLeaveRoom,
  onRefreshRooms,
  onClose,
}: LobbyModalProps) {
  const [activeTab, setActiveTab] = useState<'browse' | 'create'>('browse');
  const [newRoomName, setNewRoomName] = useState('');
  const [minutes, setMinutes] = useState(10);
  const [clockEnabled, setClockEnabled] = useState(true);
  const [copied, setCopied] = useState(false);
  const [actionBusy, setActionBusy] = useState(false);

  if (!open) return null;

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!playerName.trim()) return;
    setActionBusy(true);
    try {
      await onCreateRoom(newRoomName, minutes, clockEnabled);
    } finally {
      setActionBusy(false);
    }
  }

  async function handleJoin(roomId: string) {
    if (!playerName.trim()) return;
    setActionBusy(true);
    try {
      await onJoinRoom(roomId);
    } finally {
      setActionBusy(false);
    }
  }

  function copyRoomId(id: string) {
    navigator.clipboard.writeText(id).catch(() => {});
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="modal-backdrop modal-backdrop--top" role="presentation">
      <section
        className="setup-dialog lobby-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="lobby-title"
      >
        <button
          className="icon-button dialog-close"
          type="button"
          onClick={onClose}
          aria-label="Tutup"
        >
          <X size={20} />
        </button>

        <div className="setup-dialog__mark" aria-hidden="true">
          🌐
        </div>
        <p className="eyebrow">Multiplayer Online</p>
        <h1 id="lobby-title">Lobi Catur Online</h1>

        {/* If inside a room waiting for opponent */}
        {currentRoom ? (
          <div className="waiting-room" aria-live="polite">
            <div className="waiting-room__header">
              <span className="live-dot" />
              <strong>{currentRoom.roomName}</strong>
              <small>{currentRoom.minutes} menit • {currentRoom.clockEnabled ? 'Dengan Timer' : 'Tanpa Timer'}</small>
            </div>

            <div className="waiting-players">
              <div className="waiting-player waiting-player--host">
                <span className="player-badge">Putih (Host)</span>
                <strong>{currentRoom.hostName}</strong>
              </div>
              <div className="waiting-vs">VS</div>
              <div className="waiting-player waiting-player--guest">
                <span className="player-badge">Hitam (Tamu)</span>
                {currentRoom.guestName ? (
                  <strong>{currentRoom.guestName}</strong>
                ) : (
                  <span className="waiting-placeholder">
                    <LoaderCircle className="spin" size={16} /> Menunggu lawan bergabung…
                  </span>
                )}
              </div>
            </div>

            <div className="room-code-box">
              <span>Kode Kamar:</span>
              <code>{currentRoom.roomId}</code>
              <button
                type="button"
                className="icon-button-small"
                onClick={() => copyRoomId(currentRoom.roomId)}
                title="Salin ID Kamar"
              >
                {copied ? <Check size={14} color="green" /> : <Copy size={14} />}
              </button>
            </div>

            {error && <p className="form-error" role="alert">{error}</p>}

            <div className="dialog-actions">
              <button
                className="danger-button"
                type="button"
                onClick={onLeaveRoom}
                disabled={actionBusy}
              >
                <DoorOpen size={16} /> Tinggalkan Kamar
              </button>
            </div>
          </div>
        ) : (
          /* Normal Lobby view: Player Name + Tabs (Browse / Create) */
          <>
            <label className="field">
              <span>Nama Anda</span>
              <input
                value={playerName}
                onChange={(e) => onPlayerNameChange(e.target.value)}
                placeholder="Masukkan nama pemain..."
                maxLength={30}
              />
            </label>

            <div className="lobby-tabs">
              <button
                type="button"
                className={`lobby-tab ${activeTab === 'browse' ? 'lobby-tab--active' : ''}`}
                onClick={() => setActiveTab('browse')}
              >
                <Users size={16} /> Cari Kamar ({rooms.length})
              </button>
              <button
                type="button"
                className={`lobby-tab ${activeTab === 'create' ? 'lobby-tab--active' : ''}`}
                onClick={() => setActiveTab('create')}
              >
                <Plus size={16} /> Buat Kamar Baru
              </button>
            </div>

            {error && <p className="form-error" role="alert">{error}</p>}

            {activeTab === 'browse' ? (
              <div className="room-browser">
                <div className="room-browser__toolbar">
                  <span>Kamar Tersedia</span>
                  <button
                    type="button"
                    className="text-button text-button--compact"
                    onClick={onRefreshRooms}
                    disabled={loading}
                  >
                    <RefreshCw size={14} className={loading ? 'spin' : ''} /> Segarkan
                  </button>
                </div>

                {rooms.length === 0 ? (
                  <div className="empty-rooms">
                    <Wifi size={24} />
                    <p>Belum ada kamar yang terbuka.</p>
                    <small>Buat kamar baru untuk menunggu pemain lain bergabung.</small>
                  </div>
                ) : (
                  <div className="room-list">
                    {rooms.map((room) => (
                      <div className="room-card" key={room.roomId}>
                        <div className="room-card__info">
                          <strong>{room.roomName}</strong>
                          <small>Host: {room.hostName} • {room.minutes}m</small>
                        </div>
                        <button
                          type="button"
                          className="primary-button primary-button--compact"
                          onClick={() => handleJoin(room.roomId)}
                          disabled={!playerName.trim() || actionBusy || room.isFull}
                        >
                          Gabung
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ) : (
              <form onSubmit={handleCreate} className="create-room-form">
                <label className="field">
                  <span>Nama Kamar (Opsional)</span>
                  <input
                    value={newRoomName}
                    onChange={(e) => setNewRoomName(e.target.value)}
                    placeholder="Contoh: Duel Santai"
                    maxLength={40}
                  />
                </label>

                <fieldset className="setup-time">
                  <legend>
                    <Clock3 size={16} /> Durasi Waktu
                  </legend>
                  <div className="time-presets">
                    {timePresets.map((preset) => (
                      <button
                        key={preset}
                        type="button"
                        className={minutes === preset ? 'time-preset time-preset--active' : 'time-preset'}
                        onClick={() => setMinutes(preset)}
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
                      onChange={(e) => setMinutes(Number(e.target.value))}
                    />
                  </label>
                </fieldset>

                <label className="checkbox-field">
                  <input
                    type="checkbox"
                    checked={clockEnabled}
                    onChange={(e) => setClockEnabled(e.target.checked)}
                  />
                  <span>Aktifkan jam catur</span>
                </label>

                <button
                  className="primary-button primary-button--wide"
                  type="submit"
                  disabled={!playerName.trim() || actionBusy}
                >
                  {actionBusy ? (
                    <LoaderCircle className="spin" size={18} />
                  ) : (
                    <Plus size={18} />
                  )}
                  Buat & Tunggu Lawan
                </button>
              </form>
            )}
          </>
        )}
      </section>
    </div>
  );
}
