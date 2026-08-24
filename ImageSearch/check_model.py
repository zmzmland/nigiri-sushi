"""
新しいモデルを受け取ったときの照合ツール。

画像認識班から best.pt を受け取ったら、組み込む前にこれを実行してください。
「クラス名がズレていて、検出はできるのに全部不正解」という、
気づきにくい事故を防げます。

実行:
    cd "/Users/x26086/My project/ImageSearch"
    python3 check_model.py                      # models/best.pt を調べる
    python3 check_model.py ~/Downloads/best.pt  # 受け取ったファイルを直接調べる

確認すること:
    1. モデルが持つクラス名と、その順序
    2. data.yaml のクラス定義と一致しているか
    3. Unity のシーンで実際に注文に使われているスプライトと一致しているか
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SCRIPT_DIR.parent
ASSETS_DIR = PROJECT_DIR / "Assets"
SCENES_DIR = ASSETS_DIR / "Scenes"
DATA_YAML = SCRIPT_DIR / "data.yaml"

# 寿司ではない特別扱いのクラス（スプライトが無くてよい）
SPECIAL_CLASSES = {"NG"}

# 注文リストを持つスクリプト
ORDER_SCRIPTS = {
    "ee919a47c3f464150862654811567da7": "Customer",
    "a68250f5eb155462bbdec671468a9e13": "Customer2",
}


def load_yaml_names(path: Path) -> list[str] | None:
    """data.yaml の names を、インデックス順のリストで返す。"""
    if not path.exists():
        return None
    try:
        import yaml
        data = yaml.safe_load(path.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"  data.yaml を読めませんでした: {e}")
        return None

    names = data.get("names")
    if names is None:
        return None
    if isinstance(names, dict):
        return [names[k] for k in sorted(names)]
    return list(names)


def guid_to_name() -> dict[str, str]:
    """Assets 以下の .meta を走査して、guid → ファイル名（拡張子なし）の対応を作る。"""
    table = {}
    if not ASSETS_DIR.is_dir():
        return table

    for meta in ASSETS_DIR.rglob("*.meta"):
        try:
            head = meta.read_text(encoding="utf-8", errors="replace")[:400]
        except OSError:
            continue
        m = re.search(r"^guid: ([a-f0-9]{32})", head, re.M)
        if m:
            table[m.group(1)] = meta.name[:-5].rsplit(".", 1)[0]
    return table


def order_sprites_in_scenes() -> dict[str, list[str]]:
    """各シーンの orderSprites に登録されたスプライト名を返す。"""
    result: dict[str, list[str]] = {}
    if not SCENES_DIR.is_dir():
        return result

    names = guid_to_name()

    for scene in sorted(SCENES_DIR.glob("*.unity")):
        try:
            text = scene.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue

        for block in re.split(r"\n--- ", text):
            m = re.search(r"m_Script: \{fileID: \d+, guid: ([a-f0-9]{32})", block)
            if not m or m.group(1) not in ORDER_SCRIPTS:
                continue

            # orderSprites: の直後に続く "- {fileID: ..., guid: ...}" を拾う
            om = re.search(r"^  orderSprites:\s*$", block, re.M)
            if not om:
                continue

            sprites = []
            for line in block[om.end():].splitlines()[1:]:
                gm = re.match(r"\s*-\s*\{fileID: \d+, guid: ([a-f0-9]{32})", line)
                if not gm:
                    break
                sprites.append(names.get(gm.group(1), f"(不明:{gm.group(1)[:8]})"))

            label = f"{scene.stem} / {ORDER_SCRIPTS[m.group(1)]}"
            result[label] = sprites

    return result


def main() -> int:
    weights = (
        Path(sys.argv[1]).expanduser()
        if len(sys.argv) > 1
        else SCRIPT_DIR / "models" / "best.pt"
    )

    if not weights.exists():
        print(f"✗ モデルが見つかりません: {weights}")
        return 1

    print("=" * 54)
    print("  モデル照合")
    print("=" * 54)
    print(f"  対象   : {weights}")
    print(f"  サイズ : {weights.stat().st_size / 1_000_000:.1f} MB")
    print()

    from ultralytics import YOLO

    model = YOLO(str(weights))
    model_names = [model.names[i] for i in sorted(model.names)]

    print("--- モデルが持つクラス ---")
    for i, n in enumerate(model_names):
        print(f"  {i}: {n}")
    print()

    problems = []

    # ---------------------------------------------
    # 1. data.yaml との照合
    # ---------------------------------------------
    print("--- data.yaml との照合 ---")
    yaml_names = load_yaml_names(DATA_YAML)

    if yaml_names is None:
        print("  data.yaml を読めませんでした。手で確認してください。")
    elif yaml_names == model_names:
        print("  ✅ 完全に一致しています")
    else:
        print("  ★ 一致しません")
        print(f"     data.yaml : {yaml_names}")
        print(f"     モデル     : {model_names}")
        if sorted(yaml_names) == sorted(model_names):
            print("     → 中身は同じですが【順序】が違います。")
            print("        この状態で compare_models.py を実行しても、")
            print("        評価値は意味を持ちません。")
        problems.append(
            "data.yaml とモデルのクラスが一致しません。"
            "画像認識班に、そのモデルと同じ data.yaml をもらってください。"
        )
    print()

    # ---------------------------------------------
    # 2. Unity の注文リストとの照合
    # ---------------------------------------------
    print("--- Unity の注文リストとの照合 ---")
    scenes = order_sprites_in_scenes()

    if not scenes:
        print(f"  シーンを読めませんでした: {SCENES_DIR}")
        print("  Unity 側は手で確認してください。")
    else:
        known = set(model_names)
        all_ordered: set[str] = set()

        for label, sprites in scenes.items():
            print(f"  [{label}]  {len(sprites)} 件")
            if not sprites:
                print("     （orderSprites が空です）")
            for s in sprites:
                all_ordered.add(s)
                mark = "✅" if s in known else "★ "
                print(f"     {mark} {s}")

        bad = sorted(all_ordered - known)
        if bad:
            problems.append(
                f"モデルが知らない注文があります: {bad} — "
                "何を置いても正解になりません。"
            )

        unused = sorted(n for n in model_names if n not in SPECIAL_CLASSES and n not in all_ordered)
        if unused:
            print()
            print("  ※ モデルは認識できるが、注文には使われていないクラス:")
            print(f"     {unused}")
            print("     使う予定なら、Unity の orderSprites に追加してください。")
    print()

    # ---------------------------------------------
    # まとめ
    # ---------------------------------------------
    print("=" * 54)
    if problems:
        print("  ★ 対応が必要です")
        print("=" * 54)
        for p in problems:
            print(f"  ・{p}")
        return 1

    print("  ✅ 問題なし。組み込んで大丈夫です。")
    print("=" * 54)
    print()
    print("  次の手順:")
    print(f"    1. cp \"{weights}\" models/best.pt")
    print("    2. python3 main.py を起動し、クラス辞書を目視確認")
    print("    3. 実機で判定して、警告が出ないことを確認")
    print("    4. GitHub Desktop で Commit → Push")
    return 0


if __name__ == "__main__":
    sys.exit(main())
