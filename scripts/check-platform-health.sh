#!/usr/bin/env bash

set -euo pipefail

NETWORK="${OBSERVABILITY_NETWORK:-morrus-pos_observability}"
CURL_IMAGE="${CURL_IMAGE:-curlimages/curl:8.10.1}"

check() {
  local name="$1"
  local url="$2"
  echo "Checking ${name}: ${url}"
  docker run --rm --network "${NETWORK}" "${CURL_IMAGE}" --fail --silent --show-error --max-time 10 "${url}" >/dev/null
}

check "API live" "http://backend:8080/health/live"
check "API ready" "http://backend:8080/health/ready"
check "Queue monitor" "http://backend:8080/health/queues"
check "OTel Collector" "http://otel-collector:13133/"
check "Prometheus" "http://prometheus:9090/-/ready"
check "Loki" "http://loki:3100/ready"
check "Tempo" "http://tempo:3200/ready"
check "Alertmanager" "http://alertmanager:9093/-/ready"
check "Grafana" "http://grafana:3000/api/health"

echo "Platform health checks passed."
