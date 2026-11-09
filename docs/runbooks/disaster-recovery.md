# Runbook: Disaster Recovery MorrusPOS

## Sebelum tindakan

1. Tetapkan incident commander dan pencatat waktu.
2. Tentukan waktu awal kerusakan dan acceptable data loss.
3. Hentikan write traffic bila korupsi masih berlangsung.
4. Jangan hapus database/volume lama.
5. Ambil forensic backup bila database masih dapat dibaca.
6. Pilih snapshot berdasarkan timestamp sebelum kerusakan, bukan otomatis `latest`.

## Database/volume hilang

1. Restore exact snapshot ke volume baru dengan `restore-snapshot.sh` sesuai `docs/backup-recovery.md`.
2. Review output checksum, migration, count, RLS, stock, journal, upload, dan backend health.
3. Catat snapshot ID serta RPO/RTO aktual.
4. Stop backend, ubah nama volume pada environment production, dan start database/backend.
5. Jalankan tenant verification dan platform health check.
6. Pertahankan volume lama hingga postmortem selesai.

## Upload hilang, database sehat

Restore snapshot ke volume baru, lalu cutover hanya `UPLOADS_VOLUME_NAME`. Jangan mengembalikan database yang sehat. Identifikasi URL/file yang dibuat setelah snapshot karena berada di luar RPO.

## Penghapusan record tidak sengaja

Jangan full-restore production terlebih dahulu. Restore snapshot secara terisolasi, identifikasi record terdampak, dan buat repair/import terkontrol dengan review tenant FK, ledger stok, jurnal, dan audit trail. Full restore hanya setelah data loss pasca-snapshot disetujui incident commander dan owner bisnis.

## Migration rusak

- Tanpa write baru: simpan forensic dump, restore snapshot `pre-deploy` ke volume baru, deploy ref sebelumnya, verifikasi, lalu cutover.
- Sudah ada write baru: lakukan forward-fix atau rekonsiliasi dari database terisolasi. Jangan full restore otomatis karena transaksi baru akan hilang.

Application rollback tidak sama dengan data restore. Workflow rollback hanya mengganti Git ref.

## VPS hilang total

1. Provision host baru dan install Docker/Compose.
2. Clone Git repository.
3. Ambil secret dari password vault.
4. Buat `/etc/morrus-pos/backup.env` dan akses repository Restic.
5. Restore database/upload ke volume baru.
6. Start observability, database, backend, dan frontend.
7. Jalankan seluruh verification dan health check.
8. Update DNS/reverse proxy bila perlu dan monitor.

## Secret compromise

Rotasi password role database, JWT, GoBiz token/secret, Grafana, webhook, S3 credential, dan Restic key yang terdampak. Jangan mengembalikan secret lama dari snapshot.

## Penutupan insiden

- simpan command/output tanpa secret;
- dokumentasikan data loss dan downtime aktual;
- pastikan timer/alert kembali sehat;
- lakukan restore drill baru;
- selesaikan postmortem sebelum menghapus volume forensic/lama.
