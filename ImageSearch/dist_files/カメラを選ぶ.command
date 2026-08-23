#!/bin/bash
#
# にぎり寿司 — カメラの選択
#
# 使うカメラを1台ずつ映して選びます。
# iPhone の映像が出たら y キーを押してください。
# 選んだ結果は config.json に保存されます。

set -u

cd "$(dirname "$0")" || exit 1
BASE="$(pwd)"

VENV="$BASE/ImageSearch/.venv"

if [ ! -d "$VENV" ]; then
  echo "✗ セットアップがまだです。"
  echo "  先に「セットアップ.command」をダブルクリックしてください。"
  echo ""
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
fi

echo "================================================"
echo "  カメラを選ぶ"
echo "================================================"
echo ""
echo "  iPhone を使う場合:"
echo "    ・Mac の近くに置く"
echo "    ・画面のロックを解除しておく"
echo "    ・Bluetooth と Wi-Fi をオンにする"
echo ""
echo "  操作:  y = このカメラを使う / n = 次を見る / ESC = 中止"
echo ""

# shellcheck disable=SC1091
source "$VENV/bin/activate"

cd "$BASE/ImageSearch" || exit 1
python3 camera_pick.py

echo ""
read -n 1 -s -r -p "何かキーを押すと閉じます..."
