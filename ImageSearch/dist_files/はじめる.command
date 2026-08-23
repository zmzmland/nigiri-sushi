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
[ -f "$SHARED/capture_trigger.txt" ] && mv "$SHARED/capture_trigger.txt" "$SHARED/.trash_trigger" 2>/dev/null
echo "✓ 共有フォルダを初期化しました"

# ---------- 画像認識を起動 ----------
# shellcheck disable=SC1091
source "$VENV/bin/activate"

echo "✓ 画像認識を起動します..."
echo ""

cd "$BASE/ImageSearch" || exit 1
python3 main.py &
PY_PID=$!

# Python が終了したらゲームも閉じる、の逆も用意する
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
  echo "終了しました。"
}
trap cleanup EXIT INT TERM

# カメラの起動を待つ（連係カメラは数秒かかる）
sleep 8

# Python が既に落ちていないか確認
if ! kill -0 "$PY_PID" 2>/dev/null; then
  echo ""
  echo "✗ 画像認識が起動できませんでした。"
  echo "  上に出ているエラーを確認してください。"
  echo "  カメラの問題なら「カメラを選ぶ.command」を試してください。"
  echo ""
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
fi

# ---------- ゲームを起動 ----------
echo ""
echo "✓ ゲームを起動します..."
echo ""
echo "------------------------------------------------"
echo "  遊び方"
echo "    Space  … 寿司を並べたら判定"
echo "    a / s  … 手動で加点・減点（認識が不調なとき）"
echo "    ESC長押し … ゲームを終了"
echo ""
echo "  このウィンドウは閉じないでください。"
echo "------------------------------------------------"
echo ""

# -W で、ゲームが終了するまでここで待つ
open -W "$APP"

echo ""
echo "ゲームが終了しました。"
