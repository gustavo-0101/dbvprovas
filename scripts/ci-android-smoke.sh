#!/bin/sh
# Fumaça do app no emulador do CI (CA-TEN-005). O Postgres e a API já estão de pé.
set -e
adb install -r src/Dbvprovas.App/bin/Debug/net10.0-android/io.github.gustavo0101.dbvprovas-Signed.apk
mkdir -p appium-logs
appium --address 127.0.0.1 --log appium-logs/appium.log &
ready=0
for i in $(seq 1 60); do
  if curl -fs http://127.0.0.1:4723/status > /dev/null; then ready=1; break; fi
  sleep 2
done
if [ "$ready" != 1 ]; then
  echo "O Appium não respondeu em 120 s; log abaixo." >&2
  cat appium-logs/appium.log >&2 || true
  exit 1
fi
DBV_APPIUM_URL=http://127.0.0.1:4723/ dotnet test tests/Dbvprovas.App.Smoke --no-build
