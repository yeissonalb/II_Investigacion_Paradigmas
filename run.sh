#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

echo "==============================================="
echo " Event Arena · Event Sourcing + CQRS"
echo "==============================================="
echo "Levantando la solucion con Docker Compose..."
echo ""

docker compose pull
docker compose build
docker compose up -d

echo ""
echo "Esperando a que Service A y Service B respondan..."
ready=0
for i in $(seq 1 40); do
  code_a=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 http://localhost:3001/health || true)
  code_b=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 http://localhost:3002/health || true)
  if [ "$code_a" = "200" ] && [ "$code_b" = "200" ]; then
    ready=1
    break
  fi
  sleep 3
done

docker compose ps
echo ""

if [ "$ready" -eq 1 ]; then
  echo "Listo. Abrir el frontend y probar el escenario:"
else
  echo "Los contenedores ya estan arriba, pero la salud tardo. Reintentar en unos segundos."
fi

echo "  Frontend:    http://localhost:8080"
echo "  Service A:   http://localhost:3001/health"
echo "  Service B:   http://localhost:3002/health"
echo "  EventStore:  http://localhost:2113"
echo ""
echo "Para detener: docker compose down"
