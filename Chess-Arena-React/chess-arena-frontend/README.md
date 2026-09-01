# Chess Arena Frontend

Frontend React + TypeScript untuk `ChessController.cs`, menggunakan [`react-chessboard`](https://github.com/Clariity/react-chessboard).

## Fitur

- Membuat sesi melalui `POST /api/chess`.
- Memuat papan dari `GET /{gameId}/board`.
- Memilih bidak dan menyorot langkah legal dari `GET /valid-moves`.
- Klik atau drag-and-drop untuk menjalankan `POST /moves`.
- Dialog promosi Queen, Rook, Bishop, atau Knight.
- Castling dan en passant mengikuti validasi di `Game.cs`.
- AI Bot memilih langkah melalui endpoint `GET /suggestion`.
- Riwayat langkah dari `GET /history` dan aksi menyerah.
- Timer kustom 1–180 menit dengan increment 0–60 detik.
- Tiga tema papan dan pemilih warna custom; pilihan disimpan di browser.
- Layout responsif untuk desktop dan mobile.

## Menjalankan

```bash
npm install
cp .env.example .env
npm run dev
```

Default frontend berjalan di `http://localhost:5173` dan meneruskan `/api` ke `http://localhost:5000`. Ubah `VITE_API_PROXY_TARGET` bila port ASP.NET berbeda.

Untuk production, isi alamat API penuh:

```env
VITE_CHESS_API_URL=https://api-domain-anda.com/api/chess
```

Lalu build:

```bash
npm run build
```

## Catatan integrasi backend

`ChessController` saat ini tidak menerima konfigurasi timer dan tidak mengembalikan sisa waktu. Karena itu, timer custom dijalankan secara akurat di sisi frontend. Untuk timer yang authoritative dan sinkron lintas perangkat, ubah `CreateGame` agar menerima durasi/increment, teruskan ke `Game.StartGame`, lalu tambahkan sisa waktu pada respons status game.

`BoardDTO` dinormalisasi secara defensif untuk properti camelCase maupun PascalCase. Jika `PieceType` dikirim sebagai angka, sesuaikan `VITE_PIECE_ENUM_ORDER` dengan urutan deklarasi enum pada backend.

Jika frontend dan API berada pada origin berbeda, aktifkan CORS di ASP.NET untuk origin frontend.
