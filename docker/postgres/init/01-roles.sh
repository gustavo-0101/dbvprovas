#!/bin/sh
# Inicialização do Postgres de dev: cria os papéis e o banco (D-121).
set -e
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v owner_password="$DBV_OWNER_PASSWORD" \
  -v app_password="$DBV_APP_PASSWORD" \
  -f /dbv/roles.sql
