#!/usr/bin/env bash
# 밸런스 시뮬레이터(BalanceSim 웹 모드)를 오라클 서버에 올리고 재시작한다.
# 사용법 (Git Bash, 어디서든):  bash Server/deploy/deploy-sim.sh
# 처음 설치하면 접속 비밀번호를 만들어 Server/tools/.sim-password 에 저장한다 (git에 안 올라감).
# 브라우저: http://161.33.142.112:7777/sim/  (게임 서버가 넘겨 줌)
set -euo pipefail

HOST="${CARDGAME_HOST:-ubuntu@161.33.142.112}"
KEY="${CARDGAME_KEY:-/c/Server/RealTimeCardGame/ssh-key-2026-11-03.key}"
# SSH 포트: 서버는 22, 443, 8080에서 받는다. 자취방 와이파이 등에서 22번이 자주 끊겨 기본은 443
PORT="${CARDGAME_SSH_PORT:-443}"
SERVER_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PASSWORD_FILE="$SERVER_DIR/tools/.sim-password"
SSH=(ssh -p "$PORT" -i "$KEY" -o BatchMode=yes -o ConnectTimeout=10 "$HOST")

echo "== publish BalanceSim (linux-arm64)"
rm -rf "$SERVER_DIR/BalanceSim/publish"
dotnet publish "$SERVER_DIR/BalanceSim/BalanceSim.csproj" \
  -c Release -r linux-arm64 --self-contained false -o "$SERVER_DIR/BalanceSim/publish" > /dev/null

echo "== upload"
tar -C "$SERVER_DIR/BalanceSim/publish" -czf - . | "${SSH[@]}" 'rm -rf /tmp/balancesim-new && mkdir /tmp/balancesim-new && tar -C /tmp/balancesim-new -xzf -'
scp -q -P "$PORT" -i "$KEY" -o BatchMode=yes "$SERVER_DIR/deploy/balancesim.service" "$HOST:/tmp/balancesim.service"

echo "== install"
"${SSH[@]}" 'bash -s' <<'REMOTE'
set -euo pipefail
if ! dpkg -s aspnetcore-runtime-10.0 >/dev/null 2>&1; then
  sudo apt-get update -qq && sudo apt-get install -y -qq aspnetcore-runtime-10.0
fi
id cardgame >/dev/null 2>&1 || sudo useradd --system --no-create-home --shell /usr/sbin/nologin cardgame
if [ ! -f /etc/balancesim.env ]; then
  PASS=$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 12)
  printf 'SIM_PASSWORD=%s\nSIM_AUTO_GAMES=1500\nSIM_AUTO_AIS=random,default,greedy\n' "$PASS" | sudo tee /etc/balancesim.env > /dev/null
  sudo chmod 600 /etc/balancesim.env
  echo "새 비밀번호를 만들었습니다."
fi
sudo rm -rf /opt/balancesim
sudo mv /tmp/balancesim-new /opt/balancesim
sudo chown -R root:root /opt/balancesim
sudo install -m 644 /tmp/balancesim.service /etc/systemd/system/balancesim.service
sudo systemctl daemon-reload
sudo systemctl enable balancesim >/dev/null
sudo systemctl restart balancesim
for i in $(seq 1 20); do
  code=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5080/api/status || true)
  [ "$code" = "401" ] || [ "$code" = "200" ] && break
  sleep 1
done
echo "balancesim: $(systemctl is-active balancesim) (api $code)"
REMOTE

# 비밀번호를 PC에 저장 (sim-sync, 브라우저 접속에 씀)
"${SSH[@]}" 'sudo grep ^SIM_PASSWORD= /etc/balancesim.env | cut -d= -f2' > "$PASSWORD_FILE"
echo "== 완료: http://161.33.142.112:7777/sim/  (사용자 이름 아무거나, 비밀번호는 $PASSWORD_FILE)"
