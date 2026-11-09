# Runbook: Backup atau Repository Check Gagal

1. Cek timer dan log tanpa mencetak environment secret:
   ```bash
   systemctl status morrus-backup.service
   journalctl -u morrus-backup.service --since -8h
   systemctl status morrus-backup-maintenance.service
   ```
2. Cek PostgreSQL dan volume upload:
   ```bash
   docker exec morruspos_db pg_isready -U morrus -d morrusposs
   docker volume inspect morrus-pos_uploads
   df -h /var/lib/morrus-pos
   ```
3. Cek DNS/TLS/object storage, expiry credential, dan permission bucket prefix. Jangan menempelkan credential ke ticket atau chat.
4. Jika Restic melaporkan lock dan tidak ada job aktif, periksa proses dahulu. Gunakan `restic unlock` hanya setelah dipastikan tidak ada backup/restore/prune berjalan.
5. Jalankan manual backup setelah akar masalah diperbaiki:
   ```bash
   sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env scripts/backup/create-backup.sh manual-retry
   ```
6. Konfirmasi snapshot ID, metric `morrus_backup_last_result=1`, dan umur backup di bawah delapan jam.
7. Jika RPO terlewati, nyatakan insiden platform dan hentikan deployment bermigrasi sampai backup kembali sehat.

Jangan menghapus Restic pack/index secara manual. Jangan menerapkan object lifecycle deletion pada active repository.
