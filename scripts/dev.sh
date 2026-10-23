#!/bin/bash
set -e

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

# SQLite needs no server — the database is just a file in data/

# Start API in background
echo "Starting API server..."
cd "$PROJECT_DIR/poker_web_api"
dotnet run --urls "http://localhost:5264" &
API_PID=$!

# Start public frontend in background
echo "Starting frontend..."
cd "$PROJECT_DIR/poker_frontend"
npm run dev &
FRONTEND_PID=$!

# Start admin frontend in background
echo "Starting admin..."
cd "$PROJECT_DIR/poker_admin"
npm run dev &
ADMIN_PID=$!

echo ""
echo "✅ All services running:"
echo "   API:      http://localhost:5264"
echo "   Frontend: http://localhost:5173"
echo "   Admin:    http://localhost:5174"
echo ""
echo "Press Ctrl+C to stop all"

# Trap Ctrl+C to kill all background processes
cleanup() {
  echo "Stopping..."
  kill $API_PID $FRONTEND_PID $ADMIN_PID 2>/dev/null
  sleep 1
  # Force-kill only if still alive
  for pid in $API_PID $FRONTEND_PID $ADMIN_PID; do
    kill -0 $pid 2>/dev/null && kill -9 $pid 2>/dev/null
  done
  exit 0
}
trap cleanup INT TERM

# Wait for any to exit
wait
