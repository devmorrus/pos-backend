#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

SNAPSHOT="${1:-latest}"
if [[ "${SNAPSHOT}" != "latest" ]]; then
  [[ "${SNAPSHOT}" =~ ^[a-fA-F0-9]+$ ]] || fail "Snapshot ID tidak valid"
fi

require_backup_config
prepare_runtime_directories
acquire_operation_lock

RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)-$$"
DATABASE_VOLUME="${RECOVERY_DATABASE_VOLUME:-morrus-pos_pgdata_restore_${RUN_ID}}"
UPLOADS_VOLUME="${RECOVERY_UPLOADS_VOLUME:-morrus-pos_uploads_restore_${RUN_ID}}"
KEEP_RECOVERY_VOLUMES="${KEEP_RECOVERY_VOLUMES:-false}"
RESTORE_SKIP_APPLICATION_SMOKE="${RESTORE_SKIP_APPLICATION_SMOKE:-false}"

[[ "${DATABASE_VOLUME}" != "${POSTGRES_DATA_VOLUME_NAME}" ]] \
  || fail "Restore menolak volume database production: ${DATABASE_VOLUME}"
[[ "${UPLOADS_VOLUME}" != "${UPLOADS_VOLUME_NAME}" ]] \
  || fail "Restore menolak volume upload production: ${UPLOADS_VOLUME}"
docker volume inspect "${DATABASE_VOLUME}" >/dev/null 2>&1 \
  && fail "Target database volume sudah ada: ${DATABASE_VOLUME}"
docker volume inspect "${UPLOADS_VOLUME}" >/dev/null 2>&1 \
  && fail "Target upload volume sudah ada: ${UPLOADS_VOLUME}"

METRICS_FILE="${BACKUP_METRICS_DIR}/restore-drill.prom"
START_EPOCH="$(date +%s)"
LAST_SUCCESS="$(read_metric_value "${METRICS_FILE}" morrus_restore_drill_last_success_timestamp_seconds || true)"
LAST_SUCCESS="${LAST_SUCCESS:-0}"
RESULT=0
RESTORE_PATH=""
NETWORK_NAME="morrus-restore-${RUN_ID}"
DATABASE_CONTAINER="morrus-restore-db-${RUN_ID}"
API_CONTAINER="morrus-restore-api-${RUN_ID}"
DATABASE_VOLUME_CREATED=false
UPLOADS_VOLUME_CREATED=false

write_metrics() {
  local now duration temporary
  now="$(date +%s)"
  duration=$((now - START_EPOCH))
  temporary="${METRICS_FILE}.tmp.$$"
  cat > "${temporary}" <<EOF
# HELP morrus_restore_drill_last_attempt_timestamp_seconds Unix time of the last isolated restore attempt.
# TYPE morrus_restore_drill_last_attempt_timestamp_seconds gauge
morrus_restore_drill_last_attempt_timestamp_seconds ${START_EPOCH}
# HELP morrus_restore_drill_last_success_timestamp_seconds Unix time of the last successful isolated restore.
# TYPE morrus_restore_drill_last_success_timestamp_seconds gauge
morrus_restore_drill_last_success_timestamp_seconds ${LAST_SUCCESS}
# HELP morrus_restore_drill_last_duration_seconds Duration of the last isolated restore attempt.
# TYPE morrus_restore_drill_last_duration_seconds gauge
morrus_restore_drill_last_duration_seconds ${duration}
# HELP morrus_restore_drill_last_result Whether the last isolated restore succeeded (1) or failed (0).
# TYPE morrus_restore_drill_last_result gauge
morrus_restore_drill_last_result ${RESULT}
EOF
  mv "${temporary}" "${METRICS_FILE}"
  chmod 644 "${METRICS_FILE}"
}

cleanup() {
  local exit_code=$?
  trap - EXIT
  docker rm -f "${API_CONTAINER}" >/dev/null 2>&1 || true
  docker rm -f "${DATABASE_CONTAINER}" >/dev/null 2>&1 || true
  docker network rm "${NETWORK_NAME}" >/dev/null 2>&1 || true
  if [[ "${KEEP_RECOVERY_VOLUMES}" != "true" ]]; then
    if [[ "${DATABASE_VOLUME_CREATED}" == "true" ]]; then
      docker volume rm "${DATABASE_VOLUME}" >/dev/null 2>&1 || true
    fi
    if [[ "${UPLOADS_VOLUME_CREATED}" == "true" ]]; then
      docker volume rm "${UPLOADS_VOLUME}" >/dev/null 2>&1 || true
    fi
  fi
  [[ -n "${RESTORE_PATH}" ]] && rm -rf "${RESTORE_PATH}"
  write_metrics
  if [[ "${KEEP_RECOVERY_VOLUMES}" == "true" ]]; then
    echo "Recovery volumes preserved: database=${DATABASE_VOLUME} uploads=${UPLOADS_VOLUME}"
  fi
  exit "${exit_code}"
}
trap cleanup EXIT

RESTORE_PATH="$(mktemp -d "${BACKUP_STAGING_DIR}/restore-${RUN_ID}.XXXXXX")"
build_restic_docker_args
RESTORE_ARGS=(restore "${SNAPSHOT}" --target /restore)
if [[ "${SNAPSHOT}" == "latest" ]]; then
  RESTORE_ARGS+=(--host "${BACKUP_HOST_ID}" --tag application:morrus-pos)
fi
docker "${RESTIC_DOCKER_ARGS[@]}" -v "${RESTORE_PATH}:/restore" "${RESTIC_IMAGE}" "${RESTORE_ARGS[@]}"

DATABASE_FILES="${RESTORE_PATH}/data/database"
UPLOAD_FILES="${RESTORE_PATH}/data/uploads"
[[ -f "${DATABASE_FILES}/morrusposs.dump" ]] || fail "Snapshot tidak memuat database dump"
[[ -f "${DATABASE_FILES}/manifest.json" ]] || fail "Snapshot tidak memuat manifest"
[[ -d "${UPLOAD_FILES}" ]] || fail "Snapshot tidak memuat direktori upload"
(cd "${DATABASE_FILES}" && sha256sum -c SHA256SUMS)
docker run --rm -v "${DATABASE_FILES}:/backup:ro" "${POSTGRES_IMAGE}" \
  pg_restore --list /backup/morrusposs.dump >/dev/null

docker volume create "${DATABASE_VOLUME}" >/dev/null
DATABASE_VOLUME_CREATED=true
docker volume create "${UPLOADS_VOLUME}" >/dev/null
UPLOADS_VOLUME_CREATED=true
docker run --rm \
  -v "${UPLOAD_FILES}:/source:ro" \
  -v "${UPLOADS_VOLUME}:/target" \
  "${ALPINE_IMAGE}" sh -c 'cp -a /source/. /target/'

docker network create --internal "${NETWORK_NAME}" >/dev/null
ROLE_PASSWORD="restore-${RUN_ID}-$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')"
ADMIN_PASSWORD="${RECOVERY_ADMIN_PASSWORD:-${ROLE_PASSWORD}}"
APP_PASSWORD="${RECOVERY_APP_PASSWORD:-${ROLE_PASSWORD}}"
SYSTEM_PASSWORD="${RECOVERY_SYSTEM_PASSWORD:-${ROLE_PASSWORD}}"
MIGRATION_PASSWORD="${RECOVERY_MIGRATION_PASSWORD:-${ROLE_PASSWORD}}"
export POSTGRES_PASSWORD="${ADMIN_PASSWORD}"
export MORRUS_APP_PASSWORD="${APP_PASSWORD}"
export MORRUS_SYSTEM_PASSWORD="${SYSTEM_PASSWORD}"
export MORRUS_MIGRATION_PASSWORD="${MIGRATION_PASSWORD}"
docker run -d \
  --name "${DATABASE_CONTAINER}" \
  --network "${NETWORK_NAME}" \
  --network-alias database \
  -e "POSTGRES_DB=${POSTGRES_DATABASE}" \
  -e "POSTGRES_USER=${POSTGRES_ADMIN_USER}" \
  -e POSTGRES_PASSWORD \
  -v "${DATABASE_VOLUME}:/var/lib/postgresql/data" \
  -v "${DATABASE_FILES}:/restore:ro" \
  "${POSTGRES_IMAGE}" >/dev/null

for attempt in {1..60}; do
  if docker exec "${DATABASE_CONTAINER}" pg_isready -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" >/dev/null 2>&1; then
    break
  fi
  [[ "${attempt}" -lt 60 ]] || fail "Restore PostgreSQL tidak ready"
  sleep 2
done

run_tenant_roles() {
  docker exec -i \
    -e MORRUS_APP_PASSWORD \
    -e MORRUS_SYSTEM_PASSWORD \
    -e MORRUS_MIGRATION_PASSWORD \
    "${DATABASE_CONTAINER}" \
    psql -X -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" -v ON_ERROR_STOP=1 \
    < "${MORRUS_PROJECT_DIR}/db/tenant-roles.sql"
}

run_tenant_roles
docker exec "${DATABASE_CONTAINER}" pg_restore \
  -U morrus_migration \
  -d "${POSTGRES_DATABASE}" \
  --exit-on-error \
  --single-transaction \
  --no-owner \
  --no-privileges \
  /restore/morrusposs.dump
run_tenant_roles

docker exec -i "${DATABASE_CONTAINER}" \
  psql -X -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" -v ON_ERROR_STOP=1 \
  < "${MORRUS_PROJECT_DIR}/db/tenant-preflight.sql"
docker exec -i "${DATABASE_CONTAINER}" \
  psql -X -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" -v ON_ERROR_STOP=1 \
  < "${MORRUS_PROJECT_DIR}/db/tenant-verify.sql"

MANIFEST_JSON="$(tr -d '\n' < "${DATABASE_FILES}/manifest.json")"
run_restore_verify() {
  docker exec -i "${DATABASE_CONTAINER}" \
    psql -X -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" \
    -v ON_ERROR_STOP=1 -v "manifest_json=${MANIFEST_JSON}" -f - \
    < "${MORRUS_PROJECT_DIR}/db/restore-verify.sql"
}
run_restore_verify

UPLOAD_EXPECTATIONS="$(
  docker exec -i "${DATABASE_CONTAINER}" psql -X -Atq -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" \
    -v "manifest_json=${MANIFEST_JSON}" -f - <<'SQL'
SELECT (:'manifest_json'::jsonb->>'upload_file_count') || ' ' ||
       (:'manifest_json'::jsonb->>'upload_size_bytes');
SQL
)"
read -r EXPECTED_UPLOAD_COUNT EXPECTED_UPLOAD_SIZE <<< "${UPLOAD_EXPECTATIONS}"
read -r ACTUAL_UPLOAD_COUNT ACTUAL_UPLOAD_SIZE <<< "$(volume_stats "${UPLOADS_VOLUME}")"
[[ "${EXPECTED_UPLOAD_COUNT}" =~ ^[0-9]+$ && "${EXPECTED_UPLOAD_SIZE}" =~ ^[0-9]+$ ]] \
  || fail "Manifest upload stats tidak valid"
(( ACTUAL_UPLOAD_COUNT >= EXPECTED_UPLOAD_COUNT )) \
  || fail "Jumlah upload hasil restore lebih kecil dari manifest: actual=${ACTUAL_UPLOAD_COUNT}, minimum=${EXPECTED_UPLOAD_COUNT}"
(( ACTUAL_UPLOAD_SIZE >= EXPECTED_UPLOAD_SIZE )) \
  || fail "Ukuran upload hasil restore lebih kecil dari manifest: actual=${ACTUAL_UPLOAD_SIZE}, minimum=${EXPECTED_UPLOAD_SIZE}"

UPLOAD_REFERENCES="${RESTORE_PATH}/upload-references.txt"
docker exec -i "${DATABASE_CONTAINER}" \
  psql -X -At -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" -f - \
  < "${MORRUS_PROJECT_DIR}/db/upload-references.sql" > "${UPLOAD_REFERENCES}"
docker run --rm \
  -v "${UPLOADS_VOLUME}:/uploads:ro" \
  -v "${UPLOAD_REFERENCES}:/references.txt:ro" \
  "${ALPINE_IMAGE}" sh -c '
    if find /uploads -type l | grep -q .; then
      echo "Symbolic link is not allowed in restored uploads" >&2
      exit 1
    fi
    while IFS= read -r reference; do
      relative="${reference#/uploads/}"
      case "$relative" in ""|/*|*../*|../*) echo "Unsafe upload reference: $reference" >&2; exit 1;; esac
      test -f "/uploads/$relative" || { echo "Missing upload: $reference" >&2; exit 1; }
    done < /references.txt
  '

if [[ "${RESTORE_SKIP_APPLICATION_SMOKE}" != "true" ]]; then
  BACKEND_IMAGE="${BACKEND_IMAGE:-$("${COMPOSE[@]}" images -q backend | head -n 1)}"
  [[ -n "${BACKEND_IMAGE}" ]] || fail "Backend image tidak ditemukan untuk restore smoke test"
  export ConnectionStrings__DefaultConnection="Host=database;Port=5432;Database=${POSTGRES_DATABASE};Username=morrus_app;Password=${APP_PASSWORD}"
  export ConnectionStrings__SystemConnection="Host=database;Port=5432;Database=${POSTGRES_DATABASE};Username=morrus_system;Password=${SYSTEM_PASSWORD}"
  export ConnectionStrings__MigrationConnection="Host=database;Port=5432;Database=${POSTGRES_DATABASE};Username=morrus_migration;Password=${MIGRATION_PASSWORD}"
  docker run -d \
    --name "${API_CONTAINER}" \
    --network "${NETWORK_NAME}" \
    --network-alias backend \
    -e ASPNETCORE_ENVIRONMENT=Production \
    -e ConnectionStrings__DefaultConnection \
    -e ConnectionStrings__SystemConnection \
    -e ConnectionStrings__MigrationConnection \
    -e Jwt__Secret=restore-drill-only-secret-minimum-32-characters \
    -e Jwt__Issuer=MorrusPOS \
    -e Jwt__Audience=MorrusPOS.Client \
    -e Frontend__Url=http://restore.invalid \
    -e Observability__Enabled=false \
    -e QueueMonitoring__Enabled=true \
    -v "${UPLOADS_VOLUME}:/app/wwwroot/uploads:ro" \
    "${BACKEND_IMAGE}" >/dev/null

  for attempt in {1..60}; do
    if docker run --rm --network "${NETWORK_NAME}" curlimages/curl:8.10.1 \
      --fail --silent --max-time 5 http://backend:8080/health/ready >/dev/null 2>&1; then
      break
    fi
    [[ "${attempt}" -lt 60 ]] || {
      docker logs --tail=120 "${API_CONTAINER}" >&2 || true
      fail "Restored backend tidak ready"
    }
    sleep 2
  done
  docker run --rm --network "${NETWORK_NAME}" curlimages/curl:8.10.1 \
    --fail --silent --max-time 10 http://backend:8080/health/live >/dev/null
  docker run --rm --network "${NETWORK_NAME}" curlimages/curl:8.10.1 \
    --fail --silent --max-time 10 http://backend:8080/health/queues >/dev/null
  docker exec -i "${DATABASE_CONTAINER}" \
    psql -X -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" -v ON_ERROR_STOP=1 \
    < "${MORRUS_PROJECT_DIR}/db/tenant-verify.sql"
  run_restore_verify
fi

RESULT=1
LAST_SUCCESS="$(date +%s)"
echo "Restore verification passed. Snapshot=${SNAPSHOT} DatabaseVolume=${DATABASE_VOLUME} UploadsVolume=${UPLOADS_VOLUME}"
