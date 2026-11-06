# Runbook: Error Rate atau Latency Tinggi

1. Identifikasi route, status code, dan waktu mulai pada dashboard.
2. Buka trace paling lambat/error di Tempo.
3. Bedakan business rejection 4xx dari system error 5xx.
4. Periksa PostgreSQL locks/query, HttpClient partner, thread pool, CPU, dan memory.
5. Jika terkait deploy terbaru, gunakan workflow rollback dan verifikasi `/health/ready`.
