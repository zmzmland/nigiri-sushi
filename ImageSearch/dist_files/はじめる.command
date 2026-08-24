#!/bin/bash
#
# にぎり寿司 — 起動
#
# 画像認識（Python）とゲーム本体を順番に立ち上げます。
# このウィンドウは、遊んでいる間は閉じないでください。
# 認識の状況やエラーがここに表示されます。

set -u

cd "$(dirname "$0")" || exit 1
BASE="$(pwd)"

VENV="$BASE/ImageSearch/.venv"
APP="$BASE/にぎり寿司.app"

# 画像認識の準備ができるまで待つ上限（秒）
READY_TIMEOUT=45

echo "================================================"
echo "  にぎり寿司"
echo "================================================"
echo ""

# ---------- セットアップ済みか ----------
if [ ! -d "$VENV" ]; then
  echo "✗ セットアップがまだです。"
  echo ""
  echo "  先に「セットアップ.command」をダブルクリックしてください。"
  echo ""
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
fi

if [ ! -d "$APP" ]; then
  echo "✗ にぎり寿司.app が見つかりません。"
  echo "  このスクリプトと同じフォルダに置いてください。"
  echo ""
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
fi

# ---------- 前回の残骸を掃除 ----------
SHARED="$HOME/TGS_ImageSearch"
mkdir -p "$SHARED"

: > "$SHARED/result.txt"
: > "$SHARED/order.txt"
rm -f "$SHARED/capture_trigger.txt"
rm -f "$SHARED/countdown.txt"

# 生存確認は「新しく書かれたこと」で準備完了を判断するので、
# 前回の残りを必ず消しておく。
rm -f "$SHARED/heartbeat.txt"

# ranking.json は消さない（記録を残すため）。
# 消したいときはゲームのタイトル画面で Ctrl+Shift+R を長押し。

echo "✓ 共有フォルダを初期化しました"

# ---------- 画像認識を起動 ----------
# shellcheck disable=SC1091
source "$VENV/bin/activate"

echo "✓ 画像認識を起動します..."
echo ""

cd "$BASE/ImageSearch" || exit 1
python3 main.py &
PY_PID=$!

# ゲームが終わったら Python も確実に落とす
cleanup() {
  echo ""
  echo "終了処理をしています..."

  kill "$PY_PID" 2>/dev/null

  # カメラの読み取りで止まっていると SIGTERM を無視することがあるので、
  # 少し待ってから強制終了する。
  for _ in 1 2 3 4 5 6; do
    kill -0 "$PY_PID" 2>/dev/null || break
    sleep 0.5
  done
  kill -9 "$PY_PID" 2>/dev/null

  wait "$PY_PID" 2>/dev/null
  rm -f "$SHARED/countdown.txt"
  echo "終了しました。"
}
trap cleanup EXIT INT TERM

# ---------- 画像認識の準備を待つ ----------
# 以前は「8秒待つ」という決め打ちだったが、連係カメラの起動時間は
# 日によって変わる。main.py が heartbeat.txt を書き始めたら準備完了
# なので、それを待つ。速い日は数秒で、遅い日でも取りこぼさない。
echo "  カメラの準備を待っています..."

WAITED=0
while [ ! -f "$SHARED/heartbeat.txt" ]; do

  # Python が落ちていたら待っても無駄
  if ! kill -0 "$PY_PID" 2>/dev/null; then
    echo ""
    echo "✗ 画像認識が起動できませんでした。"
    echo "  上に出ているエラーを確認してください。"
    echo "  カメラの問題なら「カメラを選ぶ.command」を試してください。"
    echo ""
    read -n 1 -s -r -p "何かキーを押すと閉じます..."
    exit 1
  fi

  sleep 0.5
  WAITED=$((WAITED + 1))

  if [ "$WAITED" -ge $((READY_TIMEOUT * 2)) ]; then
    echo ""
    echo "△ ${READY_TIMEOUT}秒たっても準備が終わりませんでした。"
    echo "  このままゲームを起動しますが、判定できない場合は"
    echo "  一度終了して「カメラを選ぶ.command」を試してください。"
    echo ""
    break
  fi
done

# ---------- ゲームを起動 ----------
echo ""
echo "✓ ゲームを起動します..."
echo ""
echo "------------------------------------------------"
echo "  遊び方"
echo "    寿司を並べて手を引くと、自動で判定されます"
echo "    Space     … すぐ判定したいとき"
echo "    ESC長押し … ゲームを終了"
echo ""
echo "  係員用"
echo "    Ctrl+Shift+R 長押し … 番付をリセット（タイトル画面）"
echo "    Ctrl+Shift+N        … その面を飛ばす（ゲーム画面）"
echo ""
echo "  認識が合わないとき（このカメラ画面で）"
echo "    a / s  … 手動カウンタ +1 / -1"
echo "    Enter  … 確定して送信"
echo "    t      … 自動判定の ON / OFF"
echo ""
echo "  このウィンドウは閉じないでください。"
echo "------------------------------------------------"
echo ""

# -W で、ゲームが終了するまでここで待つ
open -W "$APP"

echo ""
echo "ゲームが終了しました。"
