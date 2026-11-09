#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

BACKUP_REASON="${1:-scheduled}"
validate_bounded_value BACKUP_REASON "${BACKUP_REASON}"
require_backup_config
prepare_runtime_directories
acquire_operation_lock

METRICS_FILE="${BACKUP_METRICS_DIR}/backup.prom"
START_EPOCH="$(date +%s)"
STARTED_AT_UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
LAST_SUCCESS="$(read_metric_value "${METRICS_FILE}" morrus_backup_last_success_timestamp_seconds || true)"
LAST_SUCCESS="${LAST_SUCCESS:-0}"
RESULT=0
DATABASE_SIZE=0
DUMP_SIZE=0
UPLOAD_SIZE=0
STAGING_PATH=""
RESTIC_SUMMARY=""

write_metrics() {
  local now duration temporary
  now="$(date +%s)"
  duration=$((now - START_EPOCH))
  temporary="${METRICS_FILE}.tmp.$$"
  cat > "${temporary}" <<EOF
# HELP morrus_backup_last_attempt_timestamp_seconds Unix time of the last backup attempt.
# TYPE morrus_backup_last_attempt_timestamp_seconds gauge
morrus_backup_last_attempt_timestamp_seconds ${START_EPOCH}
# HELP morrus_backup_last_success_timestamp_seconds Unix time of the last successful offsite backup.
# TYPE morrus_backup_last_success_timestamp_seconds gauge
morrus_backup_last_success_timestamp_seconds ${LAST_SUCCESS}
# HELP morrus_backup_last_duration_seconds Duration of the last backup attempt.
# TYPE morrus_backup_last_duration_seconds gauge
morrus_backup_last_duration_seconds ${duration}
# HELP morrus_backup_last_result Whether the last backup succeeded (1) or failed (0).
# TYPE morrus_backup_last_result gauge
morrus_backup_last_result ${RESULT}
# HELP morrus_backup_last_database_size_bytes Logical source database size.
# TYPE morrus_backup_last_database_size_bytes gauge
morrus_backup_last_database_size_bytes ${DATABASE_SIZE}
# HELP morrus_backup_last_dump_size_bytes PostgreSQL custom dump size.
# TYPE morrus_backup_last_dump_size_bytes gauge
morrus_backup_last_dump_size_bytes ${DUMP_SIZE}
# HELP morrus_backup_last_upload_size_bytes Uploaded file size included in the snapshot.
# TYPE morrus_backup_last_upload_size_bytes gauge
morrus_backup_last_upload_size_bytes ${UPLOAD_SIZE}
EOF
  mv "${temporary}" "${METRICS_FILE}"
  chmod 644 "${METRICS_FILE}"
}

cleanup() {
  local exit_code=$?
  trap - EXIT
  write_metrics
  [[ -n "${STAGING_PATH}" ]] && rm -rf "${STAGING_PATH}"
  [[ -n "${RESTIC_SUMMARY}" ]] && rm -f "${RESTIC_SUMMARY}"
  exit "${exit_code}"
}
trap cleanup EXIT

require_database_ready
docker volume inspect "${UPLOADS_VOLUME_NAME}" >/dev/null \
  || fail "Upload volume tidak ditemukan: ${UPLOADS_VOLUME_NAME}"

DATABASE_SIZE="$(db_exec psql -X -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" -Atqc \
  "select pg_database_size(current_database());")"
[[ "${DATABASE_SIZE}" =~ ^[0-9]+$ ]] || fail "Ukuran database tidak valid: ${DATABASE_SIZE}"

AVAILABLE_BYTES="$(df -Pk "${BACKUP_STAGING_DIR}" | awk 'NR == 2 {print $4 * 1024}')"
MINIMUM_FREE_BYTES=$((DATABASE_SIZE + 536870912))
(( AVAILABLE_BYTES >= MINIMUM_FREE_BYTES )) \
  || fail "Ruang staging tidak cukup: tersedia=${AVAILABLE_BYTES}, minimal=${MINIMUM_FREE_BYTES}"

BACKUP_ID="$(date -u +%Y%m%dT%H%M%SZ)"
STAGING_PATH="$(mktemp -d "${BACKUP_STAGING_DIR}/backup-${BACKUP_ID}.XXXXXX")"
DUMP_PART="${STAGING_PATH}/morrusposs.dump.part"
DUMP_FILE="${STAGING_PATH}/morrusposs.dump"

echo "Creating PostgreSQL backup ${BACKUP_ID} (${BACKUP_REASON})"
db_exec pg_dump \
  -U "${POSTGRES_ADMIN_USER}" \
  -d "${POSTGRES_DATABASE}" \
  --format=custom \
  --compress="${PG_DUMP_COMPRESSION}" \
  --no-owner \
  --no-acl > "${DUMP_PART}"
mv "${DUMP_PART}" "${DUMP_FILE}"

docker run --rm -v "${STAGING_PATH}:/backup:ro" "${POSTGRES_IMAGE}" \
  pg_restore --list /backup/morrusposs.dump >/dev/null

DUMP_SIZE="$(stat -c %s "${DUMP_FILE}")"
DUMP_SHA256="$(sha256sum "${DUMP_FILE}" | awk '{print $1}')"
read -r UPLOAD_FILE_COUNT UPLOAD_SIZE <<< "$(volume_stats)"
COMPLETED_AT_UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
SOURCE_HOST="$(hostname)"
GIT_COMMIT="$(git -C "${MORRUS_PROJECT_DIR}" rev-parse HEAD 2>/dev/null || echo unknown)"
SERVICE_VERSION="${BACKUP_SOURCE_SERVICE_VERSION:-${SERVICE_VERSION:-${GIT_COMMIT}}}"
POSTGRES_CLIENT_VERSION="$(db_exec pg_dump --version)"

db_exec psql -X -At -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" \
  -v ON_ERROR_STOP=1 \
  -v "backup_id=${BACKUP_ID}" \
  -v "backup_reason=${BACKUP_REASON}" \
  -v "started_at_utc=${STARTED_AT_UTC}" \
  -v "completed_at_utc=${COMPLETED_AT_UTC}" \
  -v "source_host=${SOURCE_HOST}" \
  -v "git_commit=${GIT_COMMIT}" \
  -v "service_version=${SERVICE_VERSION}" \
  -v "postgres_client_version=${POSTGRES_CLIENT_VERSION}" \
  -v "dump_size_bytes=${DUMP_SIZE}" \
  -v "dump_sha256=${DUMP_SHA256}" \
  -v "upload_file_count=${UPLOAD_FILE_COUNT}" \
  -v "upload_size_bytes=${UPLOAD_SIZE}" \
  -f - < "${MORRUS_PROJECT_DIR}/db/backup-manifest.sql" \
  > "${STAGING_PATH}/manifest.json"

(cd "${STAGING_PATH}" && sha256sum morrusposs.dump manifest.json > SHA256SUMS)

build_restic_docker_args
RESTIC_SUMMARY="${STAGING_PATH}.restic.json"
docker "${RESTIC_DOCKER_ARGS[@]}" \
  -v "${STAGING_PATH}:/data/database:ro" \
  -v "${UPLOADS_VOLUME_NAME}:/data/uploads:ro" \
  "${RESTIC_IMAGE}" backup /data/database /data/uploads \
  --host "${BACKUP_HOST_ID}" \
  --tag application:morrus-pos \
  --tag environment:production \
  --tag "reason:${BACKUP_REASON}" \
  --tag "backup-id:${BACKUP_ID}" \
  --exclude '*.tmp-*' \
  --json > "${RESTIC_SUMMARY}"

restic_run snapshots \
  --host "${BACKUP_HOST_ID}" \
  --tag "backup-id:${BACKUP_ID}" \
  --latest 1 >/dev/null

SNAPSHOT_ID="$(sed -n 's/.*"snapshot_id":"\([^"]*\)".*/\1/p' "${RESTIC_SUMMARY}" | tail -n 1)"
[[ -n "${SNAPSHOT_ID}" ]] || fail "Restic tidak mengembalikan snapshot ID"

RESULT=1
LAST_SUCCESS="$(date +%s)"
echo "Backup completed. BackupId=${BACKUP_ID} SnapshotId=${SNAPSHOT_ID}"
