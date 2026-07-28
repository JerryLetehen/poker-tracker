#!/bin/bash
set -e

echo "Dropping staging database..."
dropdb -h localhost poker-sessions-staging --if-exists

echo "Creating staging database..."
createdb -h localhost poker-sessions-staging

echo "Copying dev data to staging..."
pg_dump -h localhost poker-sessions | psql -h localhost poker-sessions-staging

echo "Done. Staging is now a copy of dev."
