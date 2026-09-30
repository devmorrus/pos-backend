# Onboarding Client Baru — GoBiz Opsi B (per-Business)

1 deployment melayani N client. Tiap `Business` punya kredensial sendiri di tabel `gobiz_client_configs`.

## Cara tambah client baru (tanpa deploy ulang)

1. Buat `Business + Outlet` baru di POS seperti biasa, catat `businessId`.
2. Login sebagai `Owner/Admin` milik Business tersebut.
3. Buka `Integrasi GoBiz Direct`.
4. Isi form **Kredensial GoBiz Client**:
   - `Environment`: Sandbox / Production (otomatis isi URL preset, bisa dioverride)
   - `ClientId`, `ClientSecret` (dari dashboard GoBiz developer)
   - `PartnerId`
   - `RedirectUri`: `https://<domain-client>/api/gobiz/callback` (wajib HTTPS, tanpa query)
   - `AuthorizationUrl`, `TokenUrl`, `ApiBaseUrl`, `Scope`
   - `WebhookSecret` (opsional tapi disarankan untuk production)
   - `IsActive`: true
5. Klik **Simpan Kredensial** → badge `Source: Database` muncul.
6. Di bagian **Outlet Mapping**: isi `GoBiz Outlet ID` (cth sandbox `G405270505`), `Partner ID` opsional (default dari kredensial), klik **Connect Direct**.
7. Klik **Test Token** (wajib kirim `outletId`) → harus hijau + `expiresAtUtc` valid.
8. Klik **Preview Sync** → pastikan 0 validation error → **Sync Catalog**.
9. Cek `Integration Logs` + `Webhook Orders`.

## Fallback

Resolve config: `DB per-Business` → `Env Var GoBiz__*` → `appsettings.json`.
Jika `Source: AppsettingsFallback` muncul di UI, berarti Business itu belum punya baris DB — segera isi via UI.

## Webhook

- URL webhook didaftarkan ke GoBiz per outlet: `POST /api/gobiz/webhooks/orders`.
- Routing otomatis by `outlet_id` di payload → `gobiz_direct_integrations.GoBizOutletId`.
- Signature diverifikasi per-Business pakai `WebhookSecret` (HMAC-SHA256, header `x-gobiz-signature` / `x-gojek-signature` / `signature`).
- Jika secret kosong: webhook tetap diterima tapi dicatat warning (agar sandbox lama tetap jalan).

## Rotasi secret

Secret lama pernah ke-commit di `appsettings.json` — wajib rotasi di dashboard GoBiz, lalu update via UI (field secret dikosongkan = tidak diubah).

## Endpoint baru

- `GET /api/gobiz/config?businessId=` (Owner,Admin)
- `PUT /api/gobiz/config` (Owner,Admin)
- `GET /api/gobiz/config/debug?businessId=&outletId=`
- `GET /api/gobiz/direct/token/test?outletId=` (sekarang wajib outletId, sebelumnya global)
- `POST /api/gobiz/direct/connect` sekarang menerima `partnerId` opsional.
