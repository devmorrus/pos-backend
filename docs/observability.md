# Observability MorrusPOS

MorrusPOS memakai OpenTelemetry untuk structured logs, metrics, dan traces. Production stack terdiri dari OTel Collector, Prometheus, Loki, Tempo, Grafana, dan Alertmanager.

## Data policy

Log/trace boleh memuat `BusinessId`, `OutletId`, operation, status, dan bounded error code. Jangan mencatat Authorization/JWT, refresh token, password, connection string, GoBiz secret/token, request/response body, KTP, email, telepon, atau payment reference mentah. Tenant/user IDs tidak boleh menjadi metric label.

## Menjalankan lokal

```bash
mkdir -p ops/observability/secrets
printf '%s\n' 'https://your-alert-webhook.example' > ops/observability/secrets/alert_webhook_url
export GRAFANA_ADMIN_PASSWORD='change-me'
export OBSERVABILITY_ENABLED=true
docker compose -f docker-compose.yml -f ops/observability/docker-compose.observability.yml up -d
```

Grafana hanya bind ke `127.0.0.1:3000`. Akses melalui SSH tunnel atau reverse proxy ber-TLS dan autentikasi. Prometheus, Loki, Tempo, Alertmanager, dan OTLP tidak dipublikasikan ke internet.

## Endpoint aplikasi

- `/health/live`: process hidup.
- `/health/ready`: PostgreSQL dapat diakses.
- `/health`: alias readiness untuk kompatibilitas deployment.
- `/health/queues`: status monitor antrean; backlog tidak memblokir readiness POS.

Semua response membawa `X-Request-ID`. Caller boleh mengirim request ID berisi huruf, angka, titik, underscore, atau strip dengan panjang maksimal 64 karakter.

## Sinyal awal

- automatic ASP.NET Core, HttpClient, runtime, dan Npgsql telemetry;
- `morrus.api.info` dan `morrus.readiness`;
- `morrus.checkout.attempts` dan `morrus.checkout.duration`;
- `morrus.exceptions` dan `morrus.stock.integrity_errors`.
- `morrus.queue.depth`, `morrus.queue.oldest_age`, dan `morrus.queue.monitor_up`.
- `morrus.outbox.enqueue_attempts`, `morrus.outbox.deliveries`, `morrus.outbox.delivery_duration`, dan lease recovery.

GoBiz inbox berjalan dalam mode report-only secara default (`QUEUE_EVALUATE_BACKLOG=false`) karena lifecycle consumer belum diimplementasikan. Audit data existing dan aktifkan evaluasi backlog hanya setelah arti status `received` disepakati.

Transactional outbox dievaluasi aktif secara terpisah (`OUTBOX_EVALUATE_BACKLOG=true`). Ia mengirim `pos.stock.updated` secara at-least-once melalui SignalR V2; detail lifecycle dan replay ada di `docs/outbox.md` dan `docs/runbooks/outbox-delivery.md`.

Dashboard awal diprovision sebagai **MorrusPOS Platform Overview**. Alert rules berada di `ops/observability/prometheus/rules/` dan seluruh alert menyertakan runbook.

Backup dan restore drill diekspos oleh internal `backup-exporter`. Dashboard **MorrusPOS Backup & Recovery** menampilkan umur, hasil, durasi, dan ukuran. Alert critical aktif bila backup gagal atau lebih tua dari delapan jam; restore drill dan repository check wajib sukses setidaknya sekali dalam delapan hari. Setup lengkap ada di `docs/backup-recovery.md`.

## Validasi

```bash
docker compose -f docker-compose.yml -f ops/observability/docker-compose.observability.yml config
curl http://127.0.0.1:8085/health/live
curl http://127.0.0.1:8085/health/ready
```

Setelah request API, cari log berdasarkan `RequestId` atau `TraceId`, lalu gunakan derived field Grafana untuk membuka trace Tempo.
