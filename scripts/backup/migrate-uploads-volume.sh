#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

SOURCE_CONTAINER="morruspos_backend"
CONFIRMED=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --from-container) SOURCE_CONTAINER="${2:-}"; shift 2 ;;
    --confirm-writes-stopped) CONFIRMED=true; shift ;;
    *) fail "Unknown argument: $1" ;;
  esac
done

[[ "${CONFIRMED}" == "true" ]] \
  || fail "Stop backend lebih dulu, lalu gunakan --confirm-writes-stopped"
docker inspect "${SOURCE_CONTAINER}" >/dev/null 2>&1 \
  || fail "Source container tidak ditemukan: ${SOURCE_CONTAINER}"
[[ "$(docker inspect -f '{{.State.Running}}' "${SOURCE_CONTAINER}")" == "false" ]] \
  || fail "Source container masih berjalan; hentikan write traffic lebih dulu"

prepare_runtime_directories
acquire_operation_lock
MIGRATION_PATH="$(mktemp -d "${BACKUP_STAGING_DIR}/uploads-migration.XXXXXX")"
trap 'rm -rf "${MIGRATION_PATH}"' EXIT

if ! docker volume inspect "${UPLOADS_VOLUME_NAME}" >/dev/null 2>&1; then
  docker volume create "${UPLOADS_VOLUME_NAME}" >/dev/null
fi

read -r TARGET_COUNT TARGET_SIZE <<< "$(volume_stats "${UPLOADS_VOLUME_NAME}")"
[[ "${TARGET_COUNT}" == "0" && "${TARGET_SIZE}" == "0" ]] \
  || fail "Target upload volume tidak kosong: ${UPLOADS_VOLUME_NAME}"

docker cp "${SOURCE_CONTAINER}:/app/wwwroot/uploads/." "${MIGRATION_PATH}/"
read -r SOURCE_COUNT SOURCE_SIZE <<< "$(
  find "${MIGRATION_PATH}" -type f ! -name '*.tmp-*' -exec stat -c '%s' {} \; \
    | awk '{count += 1; bytes += $1} END {print count + 0, bytes + 0}'
)"

docker run --rm \
  -v "${MIGRATION_PATH}:/source:ro" \
  -v "${UPLOADS_VOLUME_NAME}:/target" \
  "${ALPINE_IMAGE}" sh -c 'cp -a /source/. /target/'

read -r TARGET_COUNT TARGET_SIZE <<< "$(volume_stats "${UPLOADS_VOLUME_NAME}")"
[[ "${TARGET_COUNT}" == "${SOURCE_COUNT}" && "${TARGET_SIZE}" == "${SOURCE_SIZE}" ]] \
  || fail "Upload migration verification gagal: source=${SOURCE_COUNT}/${SOURCE_SIZE}, target=${TARGET_COUNT}/${TARGET_SIZE}"

echo "Upload migration passed. Volume=${UPLOADS_VOLUME_NAME} Files=${TARGET_COUNT} Bytes=${TARGET_SIZE}"
