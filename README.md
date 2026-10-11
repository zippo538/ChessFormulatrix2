# ChessFormulatrix

Engine catur 3 lapis — core engine (.NET), API REST (ASP.NET Core), antarmuka web (React), dan harness uji (NUnit) — berbagi satu solution.

```
ChessFormulatrix.sln
├── ChessFormularix.Console/     # Core engine + CLI  (net8.0, Exe)
├── ChessFormulatrix.WebAPI/      # REST API         (net8.0, ASP.NET Core Web)
├── ChessFormulatrix.Tests/       # Unit + integration test (net8.0, NUnit)
└── ChessFormulatrix.View/
    └── chess-arena-frontend/     # SPA              (React 19 + TS + Vite)
```

---

## 1. Arsitektur

```
┌─────────────────────────────┐
│  chess-arena-frontend       │  React 19 + TypeScript + Vite
│  react-chessboard           │  port 5173
└──────────────┬──────────────┘
               │ HTTP /api/chess
┌──────────────▼──────────────┐
│  ChessFormulatrix.WebAPI    │  Controllers + AutoMapper DTO
│  port 5000                  │  in-memory session store
└──────────────┬──────────────┘
               │ ProjectReference
┌──────────────▼──────────────┐
│  ChessFormularix.Console    │  Chess engine (domain)
│  namespace                  │  + Spectre.Console UI
└─────────────────────────────┘
```

**Arah dependensi satu arah.** WebAPI bergantung pada Console; Console tidak tahu-menahu WebAPI. Frontend hanyaodeskripsi lewat kontrak JSON, tidak pernah menyentuh tipe C#.

**Konsekuensi:** logic aturan catur hanya ada di satu tempat. WebAPI tidak memvalidasi aturan — ia meneruskan dan memetakan. Validasi move di `ChessController.MakeMove` hanya memeriksa bahwa tujuan ada di daftar move legal yang sudah dihitung engine, bukan mengimplementasi aturan sendiri.

---

## 2. Tech Stack

| Lapisan | Teknologi | Versi |
|---|---|---|
| Runtime | .NET SDK | 8.0.0 (`rollForward: latestMinor`) |
| API | ASP.NET Core | 8.0 |
| Mapping | AutoMapper | 16.2.0 |
| Logging | Serilog (+ AspNetCore) | 4.4.0 / 8.0.0 |
| API docs | Swashbuckle | 6.6.2 |
| CLI UI | Spectre.Console | 0.57.2 |
| Frontend | React / react-dom | 19.1.1 |
| Papan | react-chessboard | 5.12.1 |
| Build FE | Vite | 7.1.2 |
| Icons | lucide-react | 0.542.0 |
| Test | NUnit / NUnit3TestAdapter | 3.14.0 / 4.5.0 |
| Mock | Moq | 4.20.72 |
| Integration | Microsoft.AspNetCore.Mvc.Testing | 8.0.30 |
| Coverage | coverlet.collector | 6.0.0 |

---

## 3. Core Engine

### 3.1 Struktur namespace

Namespace mengikuti struktur folder (`RootNamespace` tiap `.csproj` disetel ke nama project).

```
ChessFormularix.Console
├── Enums/          → Color, PieceType, MoveType, MoveResult, GameOverType
├── Interface/      → IGame, IPiece, IPlayer
├── Model/          → Board, Tile, Pieces, Player, Postition, Move, MoveHistory
├── Helper/         → BoardHelper
├── Game.cs         → orkestrasi permainan
└── ChessTimer.cs   → jam per-player
```

### 3.2 Model inti

| Tipe | Peran |
|---|---|
| `Board` | 64 `Tile` dalam array linear (`index = Row * 8 + Column`) |
| `Tile` | satu petak + `Pieces?` |
| `Pieces` | `(Color, PieceType)` + flag `HasMoved` |
| `Postition` | struct `Row`/`Column` (0–7) |
| `Move` | `From`, `To`, `Type`, `PromotionType` |
| `MoveHistory` | satu entri riwayat: move + piece + piece yang diambil |

### 3.3 Pipeline move

```
MakeMove(move)
  ├─ CheckTimeStatus().IsExpired ?  → reject
  ├─ IsInsideBoard(from/to)         → reject
  ├─ GetLegalMoves(from)            → pseudo-legal + filter king safety
  ├─ validasi selection & destination → reject
  ├─ ReferenceEquals check (piece masih di board?)
  ├─ RemoveEnPassantPawn / MoveCastleRook
  ├─ mutasi tile, set HasMoved
  ├─ MoveAndPromote
  ├─ catat MoveHistory
  ├─ CheckMoveResult  → checkmate / stalemate / normal
  ├─ SwitchTurn       (kalau belum game over)
  └─ ClearSelection
```

Setiap jalur validasi menolak move yang tidak valid. Response `bool` — caller wajib cek.

### 3.4 Cheese engine

| Metode | Fungsi |
|---|---|
| `GetPseudoLegalMoves` | Generate move berdasarkan aturan piece |
| `GetLegalMoves` | Filter pseudo-legal → buang yang"라고 bunuh king sendiri |
| `IsKingInCheck` | Apakah king`|square` diserang |
| `IsSquareAttacked` | Scan 64 petak, cek siapa yang menyerang |
| `GetCastleMoves` | Castling: raja belum 이동, di e1/e8, jalur kosong, tidak thru check |
| `GetEnPassantMoves` | En passant: relies on `_lastMove` |

Simulasi move (`IsMoveLegal`, `Negamax`, `GetBestMove`) memakai pola `BoardHelper.Copy` + `try/finally` untuk restore — bukan undo stack.

### 3.5 Bot AI

**Negamax dengan alpha-beta pruning.**

```
Negamax(color, depth, α, β):
  legalMoves = getLegalMoves(color)
  if empty  → return IsKingInCheck ? -100000 - depth : 0
  if depth ≤ 0 → return perspective × EvaluateBoard()
  for each move:
     copy board, apply, recurse Negamax(opposite, depth-1, -β, -α)
     restore board
     α = max(α, best);  break if α ≥ β
```

`EvaluateBoard` — material values: P=10, N=30, B=30, R=50, Q=90, K=900 (positif untuk White).

**Batas kedalaman di-clamp ke 1–4.** `DefaultBotDepth = 3`. Depth 4 pada posisi terbuka sudah cukup untuk board 8×8 tanpa tablebase.

`GetSuggestedMoveForPiece` — greedy satu ply: simulasi setiap move legal, ambil skor `EvaluateBoard` terbaik. Dipakai endpoint `GET /suggestion` untuk UI, bukan untuk bot.

---

## 4. API REST

Base URL: `http://localhost:5000/api/chess`

Swagger UI: `https://localhost:<port>/swagger` (Development only)

### 4.1 Endpoint

| Method | Route | Fungsi | Response |
|---|---|---|---|
| POST | `/api/chess` | Buat sesi | `{ gameId, message, vsBot, minutes, clockEnabled }` |
| GET | `/api/chess/{gameId}/board` | Ambil papan | `BoardDto` |
| GET | `/api/chess/{gameId}/valid-moves?row=&col=` | Move legal dari petak | `List<PosititionDto>` |
| POST | `/api/chess/{gameId}/moves` | Mainkan move | `{ gameOver, currentTurn, botMove }` |
| GET | `/api/chess/{gameId}/suggestion?row=&col=` | Saran move (AI) | `MoveDto` |
| POST | `/api/chess/{gameId}/bot-move?depth=` | Paksa jalan bot | `{ gameOver, currentTurn, botMove }` |
| GET | `/api/chess/{gameId}/time-status` | Sisa waktu | `{ whiteRemainingSeconds, blackRemainingSeconds, currentTurn, timeExpired, ... }` |
| POST | `/api/chess/{gameId}/resign` | Menyerah | `{ gameOver, reason, winner }` |
| GET | `/api/chess/{gameId}/history` | Riwayat move | `List<MoveHistoryDto>` |

### 4.2 Body request

`POST /api/chess`
```json
{
  "whiteName": "Andi",
  "blackName": "Budi",
  "vsBot": true,
  "minutes": 10,
  "clockEnabled": true
}
```
`minutes` di-clamp ke 1–180. Nama kosong → fallback (`"Anda"` / `"Pemain Hitam"`).

`POST /api/chess/{gameId}/moves`
```json
{
  "from": { "row": 6, "column": 4 },
  "to":   { "row": 4, "column": 4 },
  "promotionType": "Queen"
}
```
`promotionType` opsional; `PieceType.None` bila tidak diisi.

### 4.3 Status code

| Code | Arti |
|---|---|
| 200 | Berhasil / game over |
| 400 | Piece bukan milik pemain giliran, atau destination illegal |
| 404 | `gameId` tidak ditemukan di store |
| 409 | Waktu habis (`Conflict`) |
| 500 | Exception tak terduga |

Enum di-serialize sebagai **string** (`JsonStringEnumConverter` dengan `allowIntegerValues: false`), jadi `GameOverType.Checkmate` → `"Checkmate"`, bukan `2`.

### 4.4 Session store

`static ConcurrentDictionary<string, Game> ActiveGames` di dalam `ChessController`.

Ini **state in-memory per proses**. Konsekuensi:

- Session hilang saat API restart
- Tidak bisa multi-instance / horizontal scale — request ke instance berbeda tidak akan menemukan game yang sama
- `ConcurrentDictionary` memastikan thread-safety map, **tapi bukan** thread-safety `Game` itu sendiri

> **Pontoon:** buat production, ganti dengan store eksternal (Redis, atau signalR + session persistence). `ChessTimer` sudah jalan di thread terpisah lewat event `OnTimerTick`/`OnTimeOut`, jadi ada race condition pada `Game` yang belum dilindungi penuh di luar `_clockSync`.

### 4.5 Mapping DTO

`ChessProfile.cs` (AutoMapper Profile) memetakan domain → DTO:

- Enum → `string` via `.ToString()`
- `PromotionType` → `null` bila `PieceType.None`
- `AfterMap`embersih: `MoveHistoryDto.PromotionType` di-null-kan bila move sebenarnya bukan promosi (pawn tidak mencapai baris 0/7)

DTO tidak pernah bocor tipe domain — response JSON stabil walau model internal berubah.

---

## 5. Frontend

```
ChessFormulatrix.View/chess-arena-frontend/src/
├── App.tsx
├── components/
├── hooks/
├── services/
└── types/
```

| Item | Nilai |
|---|---|
| Dev server | `http://localhost:5173` |
| API proxy | `/api` → `http://localhost:5000` (atur `VITE_API_PROXY_TARGET`) |
| Production API | `VITE_CHESS_API_URL` |

### 5.1 Fitur

- Buat sesi (vs manusia / vs bot), timer 1–180 menit
- Papan interaktif dengan highlight petak Selected, move legal (hijau), dan target threaten (merah)
- Klik atau drag-and-drop untuk move
- Dialog promosi (Queen/Rook/Bishop/Knight)
- Riwayat move + timer live di header
- Tiga tema papan + pemilih warna kustom, disimpan di browser
- Layout responsif desktop/mobile

### 5.2 Normalisasi DTO

Frontend menormalisasi `BoardDTO` secara defensif — terima properti camelCase maupun PascalCase, dan terima `PieceType` sebagai angka maupun string. `VITE_PIECE_ENUM_ORDER` mengemap urutan enum backend.

Kalau enum di backend berubah urutan deklarasi, **frontend harus di-update** — tidak ada negosiasi otomatis.

### 5.3 CORS

`Program.cs` mengizinkan origin `http://localhost:3000` dan `http://localhost:5173`, semua header & method. Kalau frontend production, tambah origin-nya di policy `AllowReactApp`.

---

## 6. Testing

```bash
dotnet test ChessFormulatrix.sln
```

| Assembly | Cakupan |
|---|---|
| `GameConstuctor.Test` | Inisialisasi board, state awal |
| `GameEngine.Test` | Operasi dasar game |
| `CheckMove.Test` | Validasi move |
| `MakeMove.Test` | Eksekusi move |
| `IGame.Test` / `IPiece.Test` / `IPlayer.Test` | Kontrak interface |
| `GameTimer.Test` | Timer & timeout |
| `CheckTimeStatus.Test` | Status waktu |
| `Controllers/ChessController.Test` | Integration test via `WebApplicationFactory<Program>` |

Test memakai NUnit + Moq, `Assert.That` style.

---

## 7. Menjalankan

### Prasyarat
- .NET SDK 8.0+
- Node.js 20+ (untuk frontend)

### 7.1 Console
```bash
dotnet run --project ChessFormularix.Console
```
Interaktif: input nama pemain, pilih bidak, pilih tujuan. `quit` keluar, `resign` menyerah.

### 7.2 API
```bash
dotnet run --project ChessFormulatrix.WebAPI
```
Swagger: buka `/swagger` saat environment Development.

### 7.3 Frontend
```bash
cd ChessFormulatrix.View/chess-arena-frontend
npm install
npm run dev
```

### 7.4 Build production
```bash
dotnet build ChessFormulatrix.sln -c Release
cd ChessFormulatrix.View/chess-arena-frontend && npm run build
```

---

## 8. Logging

Serilog, level `Debug` untuk app, `Warning` untuk namespace `Microsoft`.

| Output | Path | Level |
|---|---|---|
| Console | stdout | `Warning` (Console app) / `Debug` (API) |
| File | `logs/chess-Console.log` | `Warning` |
| File | `logs/chess-API.log` | `Debug` |

Rolling harian, retensi 14 hari, batas 10 MB per file, `shared: true` (multi-proses aman).

**Contextual logging.** Setiap `Game` punya `GameId`; log di-enrich dengan `ForContext("GameId", ...)`. Contoh baris log:

```
[2026-10-02 14:23:01.123 +00:00 INF] [GameId: 8f3a...] [ChessFormularix.Console.Game]
Move completed. Player: White, Piece: Pawn, From: (6, 4), To: (4, 4), MoveType: Normal, ...
```

**API pakai `app.UseSerilogRequestLogging()`** — setiap HTTP request logged dengan duration + status code.

Bootstrap logger di luar `try` block, supaya kegagalan startup pun tercatat. Filter `catch when (ex is not HostAbortedException)` mencegah shutdown normal tercatat sebagai fatal.

---

## 9. Static Analysis

`.editorconfig` di root — dibaca Rider/Resharper **dan** compiler lewat `dotnet build`. Tanpa dependency tambahan.

| Kategori | Severity |
|---|---|
| `category-Style` | suggestion |
| `category-Performance` | suggestion |
| `category-Security` | **warning** |

Rule CA yang di-enable: `CA1416` (platform), `CA2000` (dispose), `CA2200` (rethrow), `CA1848` (LoggerMessage). Sisanya suggestion: `CA1305`/`CA1310` (culture & string comparison), `CA1822` (static), `CA1852` (seal internal), `CA1002`/`CA1819` (collection exposure).

Naming rule: private field `_camelCase`, interface `I` prefix.

NUnit: `NUnit2005` (pakai `Assert.That`) di-warning.

Analisis manual: Rider **Inspect Code** → *Solution*.

---

## 10. Known Issues

### 10.1 Rename namespace belum tuntas

`ChessController.cs` masih mix namespace lama:

```csharp
using ChessConsole2.Enums;      // ← tidak ada folder ini lagi
namespace ChessAPI.Controllers;  // ← seharusnya ChessFormulatrix.WebAPI.Controllers
```

`ChessAPI.http` dan `ChessConsole2.sln.DotSettings.user` juga sisa rename.

**Fix:** di Rider, Solution Explorer → klik kanan project → **Refactor** → **Rename** → `ChessFormulatrix.WebAPI` → pilih **"Rename namespace and all its usages"**. Ulangi per project.

### 10.2 Session in-memory
Lihat §4.4. Hilang saat restart, tidak bisa scale horizontal.

### 10.3 Typo `Postition`
Class dan field-nya `Postition` (huruf `s` hilang) di seluruh domain model. **Renaming menyentuh DTO, mapper profile, controller, dan frontend types.** Sudahigrationsafe untuk breaking API karena JSON wire-format tidak berubah — tapi `POST /moves` request body tetap `{ row, column }`.

### 10.4 `GetSuggestedMoveForPiece` bukan AI
Greedy satu ply, hanya banding material (`EvaluateBoard`). Tidak ada evaluasi positional seperti pawn structure, king safety, atau mobility. Bot sesungguhnya pakai `GetBestMove` (negamax).

### 10.5 Thread safety belum lengkap
`_clockSync` melindungi state clock. Tapi `MakeMove`, `SelectPiece`, `GetLegalMoves` diakses dari HTTP thread tanpa lock. Pada mode vs-bot, `ChessTimer` (`OnTimerTick`) berjalan di thread terpisah dan bisa race dengan request.

### 10.6 README frontend perlu sinkron
`ChessFormulatrix.View/chess-arena-frontend/README.md` menyebut "`ChessController` tidak menerima konfigurasi timer dan tidak mengembalikan sisa waktu". **Ini sudah tidak akurat** — `CreateGame` sudah menerima `minutes`/`clockEnabled`, dan `GET /time-status` sudah mengembalikan sisa waktu. Update file itu.

---

## 11. Struktur File

```
ChessFormularix.Console/
├── Program.cs          # CLI loop + Spectre.Console UI
├── Game.cs             # engine (1197 baris — kandidat ekstraksi)
├── GameOverResult.cs
├── ChessTimer.cs
├── ChessClassDiagram.mmd
├── Enums/  Interface/  Model/  Helper/

ChessFormulatrix.WebAPI/
├── Program.cs          # host + Serilog bootstrap
├── ChessProfile.cs     # AutoMapper profile
├── ChessAPI.http       # ← nama lama
├── Controllers/ChessController.cs
└── DTO/                # 8 DTO

ChessFormulatrix.Tests/
├── GameEngine/         # 9 test files
├── Controllers/        # integration test
└── Helper/             # fixture builders

ChessFormulatrix.View/chess-arena-frontend/
├── src/
├── package.json
└── README.md
```

---

## 12. Credit

Proyek pribadi — tidak ada kontributor eksternal.