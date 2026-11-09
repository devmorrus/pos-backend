# Runbook: Queue Backlog dan Queue Monitor

1. Buka dashboard **MorrusPOS Queue Operations** dan tentukan queue yang terdampak.
2. Jika `monitor_up=0`, cek koneksi system database dan log `QueueMonitoringFailed`.
3. Jika GoBiz inbox menumpuk, audit count/status/oldest age tanpa membuka `RawPayloadJson`.
4. Pastikan apakah `QUEUE_EVALUATE_BACKLOG` memang sudah diaktifkan setelah lifecycle consumer disetujui.
5. Untuk OTel queue tinggi, cek Loki, Tempo, collector retry, persistent queue volume, dan kapasitas disk.
6. Untuk ThreadPool queue tinggi, korelasikan dengan CPU, database latency, active request, dan trace lambat.
7. Jangan menghapus inbox atau persistent queue untuk menghilangkan alert. Simpan bukti dan tentukan replay/backfill yang aman.
