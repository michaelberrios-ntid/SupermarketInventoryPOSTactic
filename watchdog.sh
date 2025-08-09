#!/bin/sh
set -eu

PROJECT="${COMPOSE_PROJECT_NAME:-$(basename "$PWD")}"
TARGET="${TARGET_REPLICAS:-3}"   # <-- desired replica count (default 2)

echo "Watchdog for project: $PROJECT (target=$TARGET)"

while true; do
  COUNT=$(docker-compose -p "$PROJECT" ps -q store_api | wc -l | tr -d ' ')
  [ -z "$COUNT" ] && COUNT=0
  if [ "$COUNT" -lt "$TARGET" ]; then
    echo "$(date -u +%FT%TZ) store_api replicas: $COUNT — scaling to $TARGET..."
    docker-compose -p "$PROJECT" up -d --no-build --scale store_api="$TARGET"
  fi
  sleep 5
done
