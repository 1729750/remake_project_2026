#!/usr/bin/env bash
# git 커밋/pull 때 바뀐 내용을 오라클 밸런스 시뮬레이터에 자동 반영하는 훅을 설치한다. (이 PC의 .git/hooks, 한 번만)
#   - 카드 에셋(Assets/cards, card_claude), 강화 가격표, 시작 덱(SinglePlayMode 씬)이 바뀜 → sim-sync (카드 데이터 업로드)
#   - 규칙/시뮬레이터 코드(Server/CardGame.Core, Server/BalanceSim)가 바뀜 → deploy-sim.sh (시뮬레이터 재배포, SSH 키 필요)
# 결과는 Server/tools/.sim-sync.log 에 남는다. 커밋은 기다리지 않는다 (뒤에서 실행).
# 사용법 (Git Bash):  bash Server/tools/install-sim-hooks.sh
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
HOOKS="$(git rev-parse --git-path hooks)"
MARK_BEGIN="# >>> balancesim sync >>>"
MARK_END="# <<< balancesim sync <<<"

block() {
  local files_cmd="$1"
  cat <<EOF
$MARK_BEGIN
(
  ROOT="\$(git rev-parse --show-toplevel)"
  FILES="\$($files_cmd 2>/dev/null)"
  LOG="\$ROOT/Server/tools/.sim-sync.log"
  if echo "\$FILES" | grep -qE '^Assets/(cards|card_claude)/|^Assets/Resources/Data/EffectPrices\.json|^Assets/Scenes/SinglePlayMode\.unity'; then
    node "\$ROOT/Server/tools/sim-sync.mjs" --source "git \$(git rev-parse --short HEAD)" >> "\$LOG" 2>&1
  fi
  if echo "\$FILES" | grep -qE '^Server/(CardGame\.Core|BalanceSim)/'; then
    bash "\$ROOT/Server/deploy/deploy-sim.sh" >> "\$LOG" 2>&1
  fi
) &
$MARK_END
EOF
}

install_hook() {
  local name="$1" files_cmd="$2" file="$HOOKS/$1"
  if [ -f "$file" ] && grep -qF "$MARK_BEGIN" "$file"; then
    # 이전 설치분을 지우고 새로 넣는다
    sed -i "/$MARK_BEGIN/,/$MARK_END/d" "$file"
  elif [ ! -f "$file" ]; then
    printf '#!/bin/sh\n' > "$file"
  fi
  block "$files_cmd" >> "$file"
  chmod +x "$file"
  echo "설치: $file"
}

install_hook post-commit "git diff-tree --no-commit-id --name-only -r HEAD"
install_hook post-merge "git diff --name-only ORIG_HEAD HEAD"
echo "완료. 로그: $ROOT/Server/tools/.sim-sync.log"
