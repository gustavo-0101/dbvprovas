#!/bin/sh
# Fumaça do app no emulador do CI (CA-TEN-005). O Postgres e a API já estão de pé.
set -e
adb install -r src/Dbvprovas.App/bin/Debug/net10.0-android/io.github.gustavo0101.dbvprovas-Signed.apk
mkdir -p appium-logs
appium --address 127.0.0.1 --log appium-logs/appium.log &
for i in $(seq 1 60); do curl -fs http://127.0.0.1:4723/status > /dev/null && break; sleep 2; done
DBV_APPIUM_URL=http://127.0.0.1:4723/ dotnet test tests/Dbvprovas.App.Smoke --no-build
