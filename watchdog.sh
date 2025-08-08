#!/bin/sh
set -eu

: "${COMPOSE_PROJECT_NAME:?COMPOSE_PROJECT_NAME not set}"

echo "Watchdog for project: $COMPOSE_PROJECT_NAME"

while true; do
  COUNT=$(docker-compose -p "$COMPOSE_PROJECT_NAME" ps -q store_api | wc -l | tr -d ' ')
  [ -z "$COUNT" ] && COUNT=0
  if [ "$COUNT" -lt 2 ]; then
    echo "$(date -u +%FT%TZ) store_api replicas: $COUNT — scaling to 2..."
    docker-compose -p "$COMPOSE_PROJECT_NAME" up -d --no-build --scale store_api=2
  fi
  sleep 5
done
