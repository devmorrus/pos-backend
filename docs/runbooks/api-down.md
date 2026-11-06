# Runbook: API atau Telemetry Hilang

1. Cek `docker compose ps` untuk backend dan `otel-collector`.
2. Cek `/health/live`, lalu `/health/ready`.
3. Periksa `docker compose logs --tail=200 backend otel-collector`.
4. Jika live gagal, restart backend dan verifikasi image/secret/env.
5. Jika API sehat tetapi telemetry hilang, cek endpoint OTLP, collector queue, Loki/Tempo, dan kapasitas disk.
6. Rollback hanya jika versi baru menyebabkan kegagalan; jangan menghapus volume database/observability.
