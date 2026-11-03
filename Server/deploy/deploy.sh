#!/usr/bin/env bash
# 로컬에서 빌드해서 오라클 서버에 올리고 서비스를 재시작한다.
# 사용법 (Git Bash, Server 폴더 기준):  ./deploy/deploy.sh
# 키 경로/주소는 환경변수로 바꿀 수 있다.
set -euo pipefail

HOST="${CARDGAME_HOST:-ubuntu@161.33.142.112}"
KEY="${CARDGAME_KEY:-/c/Server/RealTimeCardGame/ssh-key-2026-11-03.key}"
SERVER_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SSH=(ssh -i "$KEY" -o BatchMode=yes "$HOST")

echo "== publish (linux-arm64)"
rm -rf "$SERVER_DIR/publish"
dotnet publish "$SERVER_DIR/CardGameServer/CardGameServer.csproj" \
  -c Release -r linux-arm64 --self-contained false -o "$SERVER_DIR/publish"

echo "== upload"
tar -C "$SERVER_DIR/publish" -czf - . | "${SSH[@]}" 'rm -rf /tmp/cardgame-new && mkdir /tmp/cardgame-new && tar -C /tmp/cardgame-new -xzf -'
scp -i "$KEY" -o BatchMode=yes "$SERVER_DIR/deploy/cardgame.service" "$HOST:/tmp/cardgame.service"

echo "== install"
"${SSH[@]}" 'bash -s' <<'REMOTE'
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
sudo rm -rf /opt/cardgame
sudo mv /tmp/cardgame-new /opt/cardgame
sudo chown -R root:root /opt/cardgame
sudo install -m 644 /tmp/cardgame.service /etc/systemd/system/cardgame.service
sudo systemctl daemon-reload
sudo systemctl enable cardgame >/dev/null
sudo systemctl restart cardgame
sleep 2
systemctl is-active cardgame
curl -fsS http://localhost:7777/health; echo
REMOTE
