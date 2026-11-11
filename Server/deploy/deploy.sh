#!/usr/bin/env bash
# 로컬에서 빌드해서 오라클 서버에 올리고 서비스를 재시작한다.
# 사용법 (Git Bash, 저장소 어디서든):  bash Server/deploy/deploy.sh
# 키 경로/주소는 환경변수로 바꿀 수 있다.
#
# 일부 네트워크(학교/공용 와이파이 등)는 SSH(22번) 연결을 중간중간 끊는다.
# 그래서 업로드와 설치를 SSH 연결 한 번에 끝내고, 연결이 끊기면 몇 번 다시 시도한다.
set -euo pipefail

HOST="${CARDGAME_HOST:-ubuntu@161.33.142.112}"
KEY="${CARDGAME_KEY:-/c/Server/RealTimeCardGame/ssh-key-2026-11-03.key}"
# SSH 포트: 서버는 22, 443, 8080에서 받는다. 자취방 와이파이 등에서 22번이 자주 끊겨 기본은 443
PORT="${CARDGAME_SSH_PORT:-443}"
SERVER_DIR="$(cd "$(dirname "$0")/.." && pwd)"
TRIES="${DEPLOY_TRIES:-6}"

echo "== publish (linux-arm64)"
rm -rf "$SERVER_DIR/publish"
dotnet publish "$SERVER_DIR/CardGameServer/CardGameServer.csproj" \
  -c Release -r linux-arm64 --self-contained false -o "$SERVER_DIR/publish" > /dev/null

# 서비스 파일과 설치 스크립트를 같은 묶음에 넣어 한 번에 보낸다
mkdir -p "$SERVER_DIR/publish/_deploy"
cp "$SERVER_DIR/deploy/cardgame.service" "$SERVER_DIR/publish/_deploy/"
cat > "$SERVER_DIR/publish/_deploy/install.sh" <<'REMOTE'
set -euo pipefail
if ! dpkg -s aspnetcore-runtime-10.0 >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo apt-get install -y -qq aspnetcore-runtime-10.0
fi
# 오라클 Ubuntu 이미지는 iptables가 22번 외에는 REJECT라서 7777을 그 앞에 연다 (OCI 보안 목록은 별도)
if ! sudo iptables -C INPUT -p tcp --dport 7777 -m state --state NEW -j ACCEPT 2>/dev/null; then
  sudo iptables -I INPUT 5 -p tcp --dport 7777 -m state --state NEW -j ACCEPT
  sudo netfilter-persistent save >/dev/null
fi
id cardgame >/dev/null 2>&1 || sudo useradd --system --no-create-home --shell /usr/sbin/nologin cardgame
sudo install -m 644 /tmp/cardgame-new/_deploy/cardgame.service /etc/systemd/system/cardgame.service
rm -rf /tmp/cardgame-new/_deploy
sudo rm -rf /opt/cardgame
sudo mv /tmp/cardgame-new /opt/cardgame
sudo chown -R root:root /opt/cardgame
sudo systemctl daemon-reload
sudo systemctl enable cardgame >/dev/null
sudo systemctl restart cardgame
sleep 2
systemctl is-active cardgame
curl -fsS http://localhost:7777/health; echo
REMOTE

echo "== upload + install"
for try in $(seq 1 "$TRIES"); do
  if tar -C "$SERVER_DIR/publish" -czf - . | ssh -p "$PORT" -i "$KEY" -o BatchMode=yes -o ConnectTimeout=10 -o ServerAliveInterval=5 "$HOST" \
      'rm -rf /tmp/cardgame-new && mkdir /tmp/cardgame-new && tar -C /tmp/cardgame-new -xzf - && bash /tmp/cardgame-new/_deploy/install.sh'; then
    exit 0
  fi
  echo "SSH 연결이 끊겼습니다. 다시 시도 ($try/$TRIES)" >&2
  sleep 3
done
echo "배포 실패: SSH($PORT번)에 계속 연결할 수 없습니다. CARDGAME_SSH_PORT=22 처럼 다른 포트로 시도해 보세요." >&2
exit 1
