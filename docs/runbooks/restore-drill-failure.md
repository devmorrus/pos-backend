# Runbook: Restore Drill Gagal

1. Identifikasi tahap gagal dari `journalctl -u morrus-restore-drill.service`:
   - download/dekripsi Restic;
   - checksum atau `pg_restore --list`;
   - PostgreSQL restore;
   - role/RLS/tenant verification;
   - count tabel atau upload;
   - backend health smoke test.
2. Jangan menandai snapshot sebagai usable sampai penyebabnya dipahami.
3. Jalankan `scripts/backup/repository-maintenance.sh` bila error menunjukkan repository integrity.
4. Coba snapshot sukses sebelumnya dengan exact snapshot ID. Tetap gunakan restore drill terisolasi.
5. Jika snapshot lama lulus tetapi terbaru gagal, pertahankan semua snapshot terkait dan buka insiden backup.
6. Jika invariants stok/jurnal gagal, jangan melakukan production restore. Libatkan owner database dan owner domain sebelum repair.
7. Setelah perbaikan, buat backup baru dan jalankan ulang drill. Tutup alert hanya setelah `morrus_restore_drill_last_result=1`.

Script drill tidak memakai atau menghapus volume production. Jika resource tertinggal akibat host crash, hapus hanya container/network/volume dengan prefix `morrus-restore-` setelah memeriksa label dan nama secara manual.
