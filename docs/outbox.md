# Transactional Outbox MorrusPOS

MorrusPOS menyimpan perubahan bisnis dan durable event pada transaction PostgreSQL yang sama. Dispatcher mengirim event setelah commit dengan jaminan **at-least-once**.

## Event pertama

`pos.stock.updated` versi 1 membawa nilai `QtyOnHand` absolut per produk/varian. SignalR handler mengirim `ReceiveStockUpdateV2` dengan `EventId`, global sequence, `IsAbsolute=true`, dan daftar update. Client harus mengabaikan sequence yang lebih lama atau sama; event bukan delta.

SignalR tetap bukan offline message broker. Client yang reconnect harus reload inventory melalui API.

## Lifecycle

```text
pending -> processing -> processed
                     -> failed -> processing
                     -> dead_letter -> pending (manual replay)
```

Claim memakai `FOR UPDATE SKIP LOCKED` dan lock token. Handler timeout 30 detik, lease 120 detik, retry exponential 5 detik sampai maksimum 15 menit, dan maksimum delapan attempt. Worker crash dipulihkan dari expired lease; duplicate delivery tetap mungkin dan aman karena event ID, sequence, dan absolute payload.

Processed event disimpan 14 hari. Failed/dead-letter tidak dihapus otomatis.

## Security

- `morrus_app` hanya dapat INSERT dan membaca tiga conflict-key columns untuk `ON CONFLICT`.
- `morrus_system` dapat claim/update/delete processed rows.
- RLS/FORCE RLS melindungi tenant insert.
- Trigger melarang perubahan identity, tenant, event type/version, payload, correlation, dedupe key, occurred time, dan max attempts.
- Payload tidak boleh dicatat di log/metric.

## Konfigurasi

```text
OUTBOX_ENABLED=true
OUTBOX_POLL_INTERVAL_MS=1000
OUTBOX_BATCH_SIZE=50
OUTBOX_MAX_ATTEMPTS=8
OUTBOX_BASE_RETRY_SECONDS=5
OUTBOX_MAX_RETRY_SECONDS=900
OUTBOX_HANDLER_TIMEOUT_SECONDS=30
OUTBOX_LEASE_SECONDS=120
OUTBOX_PROCESSED_RETENTION_DAYS=14
OUTBOX_MAX_PAYLOAD_BYTES=262144
OUTBOX_EVALUATE_BACKLOG=true
```

Outbox health tersedia sebagai queue `outbox` di `/health/queues`. Backlog tidak memblokir `/health/ready`, sehingga POS tetap dapat menerima transaksi ketika SignalR sedang gagal.

## Manual replay

Setelah akar masalah diperbaiki, operator dapat replay satu event:

```bash
scripts/requeue-outbox.sh EVENT_UUID operator.name 'alasan minimal lima karakter'
```

Script hanya menerima status `failed`/`dead_letter`, tidak mengubah payload, dan mencatat operator, reason, timestamp, serta replay count. Bulk replay sengaja tidak disediakan.

GoBiz order inbox tetap inbound report-only. Transactional outbox tidak mengaktifkan consumer order GoBiz.
