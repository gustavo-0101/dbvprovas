#!/bin/sh
# Fumaça do app no emulador do CI (CA-TEN-005). O Postgres e a API já estão de pé.
set -e
adb install -r src/Dbvprovas.App/bin/Debug/net10.0-android/io.github.gustavo0101.dbvprovas-Signed.apk
mkdir -p appium-logs
appium --address 127.0.0.1 --log appium-logs/appium.log &
appium_pid=$!

# Um Appium vivo e o crashpad_handler do emulador seguram o encerramento do emulador pela action, e o passo fica parado
# (issue pública ReactiveCircus/android-emulator-runner#385; RSK-AUD-001). Ao sair, encerra os dois.
# O Appium recebe SIGTERM, com até 15 s de espera e depois SIGKILL, para o trap não pendurar o passo.
# O kernel corta o nome do processo em 15 caracteres: o crashpad_handler aparece como crashpad_handle, por isso o -x com esse nome.
# O sinal é SIGTERM porque o SIGINT pode estar ignorado: a action sobe o emulador em segundo plano num sh -c não interativo.
# O trap não chama exit, então o código de saída do script continua sendo o do comando que o encerrou (o dotnet test).
cleanup() {
  kill "$appium_pid" 2>/dev/null || true
  n=0
  while kill -0 "$appium_pid" 2>/dev/null && [ "$n" -lt 15 ]; do sleep 1; n=$((n + 1)); done
  if kill -0 "$appium_pid" 2>/dev/null; then kill -9 "$appium_pid" 2>/dev/null || true; fi
  wait "$appium_pid" 2>/dev/null || true
  pkill -TERM -x crashpad_handle || true
}
trap cleanup EXIT

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
