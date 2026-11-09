#!/usr/bin/env bash

set -euo pipefail

EVENT_ID="${1:-}"
OPERATOR="${2:-}"
REASON="${3:-}"
if [[ ! "${EVENT_ID}" =~ ^[0-9a-fA-F-]{36}$ ]] || [[ -z "${OPERATOR}" ]] || [[ ${#REASON} -lt 5 ]]; then
  echo "Usage: $0 EVENT_UUID OPERATOR 'reason minimal 5 karakter'" >&2
  exit 1
fi

docker compose exec -T database psql -X -U morrus -d morrusposs \
  -v ON_ERROR_STOP=1 \
  -v "event_id=${EVENT_ID}" \
  -v "operator=${OPERATOR}" \
  -v "reason=${REASON}" \
  -f - < db/outbox-requeue.sql
