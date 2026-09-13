#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
camera_lock.py — カメラの自動露出・自動ホワイトバランスを調べる道具

周りの明るさが変わると、カメラは勝手に明るさと色を補正します。
そのせいで、同じ寿司でも見え方が変わり、認識がぶれます。
このスクリプトは次の2つを教えてくれます。

  1) いまのカメラで、露出や WB を手動に固定できるのか（probe）
  2) 実際に自動補正が働いているのか（watch）

────────────────────────────────────────────────
使い方
────────────────────────────────────────────────
    cd "/Users/x26086/My project/ImageSearch"

    python3 camera_lock.py            # 1と2を続けて実行
    python3 camera_lock.py probe      # 設定できるかだけ調べる
    python3 camera_lock.py watch      # 自動補正が働くか見る

    ESC か q で終了します。

────────────────────────────────────────────────
watch の見かた
────────────────────────────────────────────────
画面中央の明るさと色（R/G/B）を、数字とグラフで出し続けます。

    照明を変えずにじっとしている  → 数字が動かないのが正常
    手やライトで明るさを変える    → いったん動いて、
                                    数秒かけて元の値に戻っていく

この「戻っていく」動きが自動補正です。
戻らずに変わったままなら、すでに手動固定されています。

★ TGS では人が前を通るたびに明るさが変わります。
   そのたびにこの補正が走ると、認識結果もぶれます。
"""

import sys
import json
import time
from pathlib import Path

try:
    import cv2
    import numpy as np
except ImportError:
    print("cv2 と numpy が要ります。 pip3 install opencv-python numpy")
    sys.exit(1)


HERE = Path(__file__).resolve().parent
CONFIG_PATH = HERE / "config.json"


# =====================================================
#  設定の読み込み
# =====================================================
def load_camera_url():
    """config.json から camera_url を読む。無ければ 0。"""
    try:
        with open(CONFIG_PATH, encoding="utf-8") as f:
            cfg = json.load(f)
        return cfg.get("camera_url", 0)
    except Exception as e:
        print(f"config.json を読めませんでした（0番を使います）: {e}")
        return 0


def open_camera(url):
    print(f"カメラを開いています: {url!r}")
    cap = cv2.VideoCapture(url)

    if not cap.isOpened():
        print("開けませんでした。camera_pick.py で番号を選び直してください。")
        return None

    # 最初の数枚は捨てる（起動直後は真っ黒なことがある）
    for _ in range(5):
        cap.read()
        time.sleep(0.05)

    try:
        print(f"バックエンド: {cap.getBackendName()}")
    except Exception:
        pass

    return cap


# =====================================================
#  1) probe — 何が設定できるか
# =====================================================

# (表示名, OpenCV の定数名, 手動にするために入れる候補値)
#
# 自動露出の値は OS ごとに約束事が違うので、候補を順に試します。
#   Linux(V4L2)   : 1 = 手動 / 3 = 自動
#   Windows(DShow): 0.25 = 手動 / 0.75 = 自動
PROBE_ITEMS = [
    ("自動露出",           "CAP_PROP_AUTO_EXPOSURE",  [0.25, 1, 0]),
    ("露出",               "CAP_PROP_EXPOSURE",       [-6, 100]),
    ("自動ホワイトバランス", "CAP_PROP_AUTO_WB",        [0]),
    ("色温度",             "CAP_PROP_WB_TEMPERATURE", [5000]),
    ("ゲイン",             "CAP_PROP_GAIN",           [0]),
    ("明るさ",             "CAP_PROP_BRIGHTNESS",     [128, 0.5]),
]


def probe(cap):
    print()
    print("=" * 58)
    print(" 1) この カメラ で何が設定できるか")
    print("=" * 58)

    results = []

    for label, prop_name, candidates in PROBE_ITEMS:
        prop = getattr(cv2, prop_name, None)

        if prop is None:
            results.append((label, "この OpenCV に項目が無い", None))
            continue

        before = cap.get(prop)

        # -1 は「その項目に対応していない」の意味で返ることが多い
        if before == -1:
            results.append((label, "対応していない", None))
            continue

        worked = None

        for value in candidates:
            try:
                accepted = cap.set(prop, value)
            except Exception:
                accepted = False

            if not accepted:
                continue

            # set が True を返しても実際には変わらないことがあるので読み直す
            time.sleep(0.15)
            cap.read()
            after = cap.get(prop)

            if abs(after - before) > 1e-6:
                worked = (value, before, after)
                break

        if worked:
            value, b, a = worked
            results.append((label, f"設定できた（{b:.3g} → {a:.3g}）", value))
        else:
            results.append((label, f"変わらなかった（{before:.3g} のまま）", None))

    print()
    ok = 0
    for label, message, value in results:
        mark = "○" if value is not None else "×"
        if value is not None:
            ok += 1
        print(f"  {mark} {label:<22} {message}")

    print()
    if ok == 0:
        print("  → このカメラは Python からは固定できません。")
        print("    下の「Mac のアプリで固定する」を使ってください。")
    else:
        print(f"  → {ok} 項目が設定できました。main.py から固定できる見込みがあります。")
        print("    設定したい値が決まったら教えてください。main.py に組み込みます。")
    print()

    return ok


# =====================================================
#  2) watch — 自動補正が働いているか
# =====================================================
def watch(cap):
    print()
    print("=" * 58)
    print(" 2) 自動補正が働いているか見る")
    print("=" * 58)
    print()
    print("  まな板の上に寿司を置いて、しばらく眺めてください。")
    print()
    print("  ・何もしないのに数字が動く      → 自動補正が働いています")
    print("  ・手をかざして戻すと元の値に戻る → 自動補正が働いています")
    print("  ・変えた分だけ変わって戻らない   → すでに手動固定されています")
    print()
    print("  ESC か q で終了します。")
    print()

    history = []          # (明るさ, R, G, B)
    MAX_HISTORY = 200

    while True:
        ok, frame = cap.read()
        if not ok:
            print("フレームを読めませんでした。")
            break

        h, w = frame.shape[:2]

        # 中央の 1/3 だけを測る（まな板の中心あたり）
        y0, y1 = h // 3, h * 2 // 3
        x0, x1 = w // 3, w * 2 // 3
        center = frame[y0:y1, x0:x1]

        b, g, r = [float(center[:, :, i].mean()) for i in range(3)]
        lum = 0.299 * r + 0.587 * g + 0.114 * b

        history.append((lum, r, g, b))
        if len(history) > MAX_HISTORY:
            history.pop(0)

        view = frame.copy()

        # 測っている範囲を四角で示す
        cv2.rectangle(view, (x0, y0), (x1, y1), (0, 255, 255), 2)

        lines = [
            f"akarusa (luminance) : {lum:6.1f}",
            f"R {r:6.1f}   G {g:6.1f}   B {b:6.1f}",
        ]

        # 直近の振れ幅。自動補正が働いていると大きくなる
        if len(history) >= 30:
            recent = [x[0] for x in history[-30:]]
            swing = max(recent) - min(recent)
            lines.append(f"furehaba (last 30) : {swing:5.1f}")

        for i, text in enumerate(lines):
            y = 30 + i * 28
            cv2.putText(view, text, (12, y),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 0), 4)
            cv2.putText(view, text, (12, y),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 1)

        # 明るさの推移をグラフに
        draw_graph(view, [x[0] for x in history])

        cv2.imshow("camera_lock  --  ESC or q to quit", view)

        key = cv2.waitKey(30) & 0xFF
        if key in (27, ord("q")):
            break

    # まとめ
    if len(history) >= 30:
        lums = [x[0] for x in history]
        swing = max(lums) - min(lums)
        print()
        print(f"  明るさの振れ幅: {swing:.1f}")
        if swing < 5:
            print("  → ほとんど動いていません。安定しています。")
        elif swing < 20:
            print("  → 少し動いています。照明を固定すれば十分かもしれません。")
        else:
            print("  → かなり動いています。自動補正を切る価値があります。")
        print()


def draw_graph(view, values):
    """右下に明るさの推移を折れ線で描く。"""
    if len(values) < 2:
        return

    h, w = view.shape[:2]
    gw, gh = 240, 90
    gx, gy = w - gw - 16, h - gh - 16

    overlay = view.copy()
    cv2.rectangle(overlay, (gx, gy), (gx + gw, gy + gh), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.45, view, 0.55, 0, view)

    lo, hi = min(values), max(values)
    span = max(hi - lo, 1.0)

    points = []
    n = len(values)
    for i, v in enumerate(values):
        px = gx + int(gw * i / max(n - 1, 1))
        py = gy + gh - int((gh - 8) * (v - lo) / span) - 4
        points.append((px, py))

    cv2.polylines(view, [np.array(points, dtype=np.int32)],
                  False, (0, 255, 255), 1)


# =====================================================
#  入口
# =====================================================
def main():
    mode = sys.argv[1].lower() if len(sys.argv) > 1 else "all"

    cap = open_camera(load_camera_url())
    if cap is None:
        return

    try:
        if mode in ("probe", "all"):
            probe(cap)

        if mode in ("watch", "all"):
            watch(cap)
    finally:
        cap.release()
        cv2.destroyAllWindows()

    print("終了しました。")


if __name__ == "__main__":
    main()
