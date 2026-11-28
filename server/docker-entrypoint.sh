#!/bin/sh
# A volume mounted at /data is often owned by root, and SQLite then cannot create the database file.
# Fix the owner, then drop to the unprivileged "node" user for the server itself.
set -e
DATA_DIR="$(dirname "${DB_PATH:-/data/cargo.db}")"
mkdir -p "$DATA_DIR"
if [ "$(id -u)" = "0" ]; then
  chown -R node:node "$DATA_DIR" 2>/dev/null || chmod -R a+rwX "$DATA_DIR" 2>/dev/null || true
  exec su-exec node "$@"
fi
exec "$@"
