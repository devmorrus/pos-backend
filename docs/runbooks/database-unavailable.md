# Runbook: PostgreSQL Tidak Ready

1. Cek container database dan `pg_isready`.
2. Verifikasi disk, memory, connection count, dan log PostgreSQL.
3. Pastikan runtime memakai `morrus_app`, bukan role migration/superuser.
4. Jalankan `db/tenant-verify.sql` setelah database pulih.
5. Jangan mengganti connection string ke database lain tanpa verifikasi tenant/RLS.
