#!/bin/bash
#
# にぎり寿司 — 初回セットアップ
#
# この Mac で初めて動かすときに、1回だけダブルクリックしてください。
# 画像認識に必要なライブラリを、このフォルダの中だけにインストールします。
# システムの Python 環境は変更しません。
#
# 所要時間: 5〜15分（回線速度による / 約2GB ダウンロードします）

set -u

# このスクリプトが置かれているフォルダへ移動する。
# Finder からダブルクリックすると作業フォルダが / になるため必須。
cd "$(dirname "$0")" || exit 1
BASE="$(pwd)"

echo "================================================"
echo "  にぎり寿司 — 初回セットアップ"
echo "================================================"
echo "  場所: $BASE"
echo ""

# ---------- Python の確認 ----------
if ! command -v python3 >/dev/null 2>&1; then
  echo "✗ python3 が見つかりません。"
  echo ""
  echo "  ターミナルで次を実行し、開発者ツールを入れてください:"
  echo "      xcode-select --install"
  echo ""
  echo "  完了したら、もう一度このファイルをダブルクリックしてください。"
  echo ""
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
fi

PYVER="$(python3 -c 'import sys; print("%d.%d" % sys.version_info[:2])')"
echo "✓ python3 を確認しました (バージョン $PYVER)"
echo ""

# ---------- 仮想環境 ----------
VENV="$BASE/ImageSearch/.venv"

if [ -d "$VENV" ]; then
  echo "既存の環境が見つかりました。作り直します..."
  # rm は使わず、退避してから作り直す（誤削除を避けるため）
  mv "$VENV" "$BASE/ImageSearch/.venv_old_$(date +%s)" 2>/dev/null
fi

echo "仮想環境を作成しています..."
python3 -m venv "$VENV" || {
  echo "✗ 仮想環境の作成に失敗しました。"
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
}

# shellcheck disable=SC1091
source "$VENV/bin/activate"

echo "✓ 仮想環境を作成しました"
echo ""
echo "ライブラリをインストールしています。"
echo "約2GB ダウンロードするので、5〜15分ほどかかります。"
echo "ウィンドウを閉じずにお待ちください。"
echo ""

python3 -m pip install --upgrade pip --quiet
python3 -m pip install -r "$BASE/ImageSearch/requirements.txt" || {
  echo ""
  echo "✗ インストールに失敗しました。"
  echo "  ネットワークに接続されているか確認して、もう一度お試しください。"
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
}

echo ""
echo "動作を確認しています..."
python3 - <<'PYCHECK'
import sys
try:
    import cv2
    from ultralytics import YOLO
    print(f"  ✓ opencv {cv2.__version__}")
    print("  ✓ ultralytics")
except Exception as e:
    print(f"  ✗ 読み込みに失敗しました: {e}")
    sys.exit(1)
PYCHECK

if [ $? -ne 0 ]; then
  echo ""
  echo "✗ ライブラリの読み込みに失敗しました。"
  read -n 1 -s -r -p "何かキーを押すと閉じます..."
  exit 1
fi

# ---------- 共有フォルダ ----------
mkdir -p "$HOME/TGS_ImageSearch"
echo "  ✓ 共有フォルダ $HOME/TGS_ImageSearch"

# ---------- config.json ----------
if [ ! -f "$BASE/ImageSearch/config.json" ]; then
  cp "$BASE/ImageSearch/config.example.json" "$BASE/ImageSearch/config.json"
  echo "  ✓ config.json を作成しました"
fi

echo ""
echo "================================================"
echo "  セットアップが完了しました"
echo "================================================"
echo ""
echo "  次は「はじめる.command」をダブルクリックしてください。"
echo ""
echo "  カメラが正しく選ばれていない場合は、"
echo "  「カメラを選ぶ.command」で選び直せます。"
echo ""
read -n 1 -s -r -p "何かキーを押すと閉じます..."
