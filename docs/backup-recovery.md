# Backup dan Disaster Recovery MorrusPOS

## Target dan cakupan

| Data | Metode | RPO awal | RTO awal |
|---|---|---:|---:|
| PostgreSQL `morrusposs` | `pg_dump` custom format di dalam snapshot Restic | 6 jam | 2 jam pada VPS yang sama; 4 jam pada VPS baru |
| `/app/wwwroot/uploads` | Snapshot Restic langsung dari named volume | 6 jam | 2 jam |
| Source, migration, Compose, dashboard | Git remote | setiap perubahan | 1 jam |
| Secret production | Password vault, bukan repository backup | setiap perubahan | 2 jam |
| Prometheus/Loki/Tempo history | Tidak termasuk backup kritis | kehilangan history diterima | deploy ulang 1 jam |

RPO/RTO tersebut adalah target sampai hasil drill production memberikan angka nyata. Bila kehilangan transaksi hingga enam jam tidak diterima, aktifkan continuous WAL/PITR dengan WAL-G atau pgBackRest sebagai fase terpisah. Jangan mengklaim RPO sub-enam-jam hanya dari backup logical ini.

## Arsitektur

- PostgreSQL tetap memakai `postgres:15-alpine` dan named volume `morrus-pos_pgdata`.
- Upload memakai named volume `morrus-pos_uploads`; upload ditulis ke temporary file lalu di-rename secara atomic.
- Setiap backup berisi custom dump, manifest, checksum, dan file upload.
- Restic mengenkripsi data sebelum dikirim ke S3-compatible object storage.
- Bucket harus private, memakai TLS, server-side encryption, versioning, dan berada di akun/region berbeda dari VPS bila memungkinkan.
- Retensi dikelola Restic. Jangan membuat lifecycle rule yang menghapus active Restic objects berdasarkan umur karena dapat merusak repository.
- Versioning provider boleh mempertahankan noncurrent/deleted object sedikitnya 30 hari untuk perlindungan penghapusan tidak sengaja.

Backup PostgreSQL selalu logical. Jangan menyalin live `pgdata` dengan `tar`.

## Isi snapshot

```text
/data/database/morrusposs.dump
/data/database/manifest.json
/data/database/SHA256SUMS
/data/uploads/...
```

Manifest mencatat Git commit, versi PostgreSQL, migration terakhir, ukuran, checksum dump, jumlah upload, serta count tabel kritis. Password role dan connection string tidak masuk snapshot.

## Persiapan production

1. Buat bucket/prefix khusus MorrusPOS dan access key dengan akses minimum ke prefix tersebut.
2. Simpan password Restic dan credential object storage di password vault organisasi.
3. Salin template:

   ```bash
   sudo install -d -o root -g morrusdigital -m 0750 /etc/morrus-pos
   sudo install -o root -g morrusdigital -m 0640 ops/backup/backup.env.example /etc/morrus-pos/backup.env
   sudo sh -c "umask 027; printf '%s\n' 'PASSWORD-ACAK-PANJANG' > /etc/morrus-pos/restic-password"
   sudo chown root:morrusdigital /etc/morrus-pos/restic-password
   sudo chmod 0640 /etc/morrus-pos/restic-password
   sudo editor /etc/morrus-pos/backup.env
   ```

4. Buat metrics directory sebelum observability stack dijalankan:

   ```bash
   sudo install -d -o morrusdigital -g morrusdigital -m 0700 /var/lib/morrus-pos/backup-staging
   sudo install -d -o morrusdigital -g morrusdigital -m 0755 /var/lib/morrus-pos/backup-metrics
   sudo install -d -o morrusdigital -g morrusdigital -m 0700 /var/cache/morrus-pos/restic
   sudo install -d -o morrusdigital -g morrusdigital -m 0700 /var/lock/morrus-pos
   sudo install -o morrusdigital -g morrusdigital -m 0660 /dev/null /var/lock/morrus-pos/backup.lock
   ```

5. Pastikan credential recovery dapat diakses oleh sedikitnya dua operator resmi. Kehilangan password Restic berarti backup tidak dapat dibuka.

## Migrasi upload existing

Lakukan sekali sebelum backend pertama kali dijalankan dengan volume upload baru:

```bash
docker stop morruspos_backend
sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/migrate-uploads-volume.sh --confirm-writes-stopped
```

Script menolak source container yang masih berjalan, menolak target volume yang tidak kosong, lalu membandingkan file count dan byte count. Jangan hapus container lama sebelum backup offsite pertama dan restore drill pertama lulus.

Setelah deployment, verifikasi upload lama dapat dibaca dan upload baru berhasil disimpan.

## Inisialisasi dan backup pertama

Inisialisasi hanya untuk repository baru:

```bash
sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/init-repository.sh --confirm-new-repository
```

Jalankan backup pertama:

```bash
sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/create-backup.sh manual
```

Backup dianggap berhasil hanya setelah Restic mengembalikan snapshot ID dan snapshot dapat ditemukan kembali di repository.

## Restore drill

```bash
sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/restore-drill.sh latest
```

Drill membuat network internal, database volume baru, dan upload volume baru. Ia memeriksa:

- SHA-256 dump dan manifest;
- `pg_restore --list`;
- restore transaction tunggal;
- role, ownership, RLS/FORCE RLS;
- `tenant-preflight.sql`, `tenant-verify.sql`, dan `restore-verify.sql`;
- migration dan count tabel kritis terhadap manifest;
- index/constraint validity;
- count/ukuran upload tidak boleh lebih kecil dari manifest, dan seluruh local upload reference wajib tersedia;
- startup backend serta `/health/live`, `/health/ready`, dan `/health/queues`.

Temporary container, network, dan volume dihapus setelah drill. Tidak ada port restore yang dipublish ke host.

## Retensi dan repository check

```bash
sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/repository-maintenance.sh
```

Kebijakan:

- semua snapshot dalam tujuh hari dari snapshot terbaru;
- 35 daily;
- 12 weekly;
- 12 monthly;
- weekly prune dan pembacaan 5% data packs.

Naikkan `RESTIC_CHECK_SUBSET` untuk full check terjadwal bila biaya dan ukuran repository memungkinkan.

## Systemd timers

```bash
sudo cp ops/backup/systemd/* /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now morrus-backup.timer
sudo systemctl enable --now morrus-restore-drill.timer
sudo systemctl enable --now morrus-backup-maintenance.timer
systemctl list-timers 'morrus-*'
```

- backup: 00:15, 06:15, 12:15, 18:15 UTC;
- restore drill: Minggu 02:30 UTC;
- retention/check: Minggu 05:00 UTC.

`Persistent=true` menjalankan timer yang terlewat setelah host kembali aktif. Semua job memakai shared lock agar backup, restore, dan prune tidak berjalan bersamaan.

## Deployment gate

`deploy.sh`:

- membuat snapshot baru sebelum perubahan EF migration, persistence migration SQL, atau `tenant-roles.sql`;
- untuk deployment lain, menolak deployment bila snapshot terbaru lebih tua dari delapan jam;
- menjalankan gate sebelum backend baru memicu `Database.MigrateAsync()`.
- menyimpan ref yang benar-benar lulus health check di `.deploy-state/deployed-ref`; ref ini tetap ada setelah `git reset` dan menjadi dasar deteksi migration deployment berikutnya.

Siapkan `/etc/morrus-pos/backup.env`, repository, upload volume, dan backup pertama **sebelum** deployment yang mengaktifkan gate ini.

Application rollback hanya mengganti Git ref. Ia tidak mengembalikan data. Migration production harus memakai pola expand/contract agar app lama tetap kompatibel dengan schema baru selama rollback.

## Recovery ke volume baru

Ambil exact full snapshot ID, bukan short ID:

```bash
sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/check-backup-freshness.sh 8
sudo BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/list-snapshots.sh
```

Export password database dari password vault hanya untuk proses recovery:

```bash
export MORRUS_APP_PASSWORD='...'
export MORRUS_SYSTEM_PASSWORD='...'
export MORRUS_MIGRATION_PASSWORD='...'

sudo --preserve-env=MORRUS_APP_PASSWORD,MORRUS_SYSTEM_PASSWORD,MORRUS_MIGRATION_PASSWORD \
  BACKUP_ENV_FILE=/etc/morrus-pos/backup.env \
  scripts/backup/restore-snapshot.sh \
    --snapshot FULL_HEXADECIMAL_SNAPSHOT_ID \
    --database-volume morrus-pos_pgdata_recovery_YYYYMMDDHHMM \
    --uploads-volume morrus-pos_uploads_recovery_YYYYMMDDHHMM \
    --confirm-create-only
```

Script hanya membuat dan memverifikasi volume baru. Ia menolak nama volume production dan tidak melakukan cutover.

Setelah approval incident commander:

1. hentikan frontend/backend write traffic;
2. pertahankan volume lama;
3. set `POSTGRES_DATA_VOLUME_NAME` dan `UPLOADS_VOLUME_NAME` ke volume recovery;
4. start database/backend;
5. jalankan `db/tenant-verify.sql` dan `scripts/check-platform-health.sh`;
6. monitor error, stock, journal, dan queue;
7. jangan hapus volume lama sebelum postmortem ditutup.

## Secrets dan konfigurasi

Git menjadi backup untuk source, migration, Compose, dashboard, dan runbook. Password vault menjadi source of truth untuk:

- `MORRUS_APP_PASSWORD`;
- `MORRUS_SYSTEM_PASSWORD`;
- `MORRUS_MIGRATION_PASSWORD`;
- `JWT_SECRET`;
- GoBiz credentials/tokens;
- Grafana password;
- alert webhook;
- S3 credential;
- Restic password.

Jangan memasukkan `.env`, private key, password file, atau connection string ke snapshot/database manifest. Secret yang bocor harus dirotasi, bukan dipulihkan dari backup lama.

## Observability

`backup-exporter` membaca Prometheus textfiles dari `/var/lib/morrus-pos/backup-metrics`. Dashboard **MorrusPOS Backup & Recovery** menampilkan umur, hasil, durasi, dan ukuran backup/drill. Alert rules berada di `ops/observability/prometheus/rules/backup.yml`.

Volume Prometheus, Loki, Tempo, Grafana, Alertmanager, dan OTel queue tidak termasuk backup kritis. Dashboard dan rules dapat diprovision kembali dari Git; kehilangan history observability diterima pada baseline ini.
