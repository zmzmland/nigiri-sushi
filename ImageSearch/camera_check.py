"""
カメラ診断ツール。どの番号でどのカメラが開くかを調べる。

実行:
    cd "/Users/x26086/My project/ImageSearch"
    python3 camera_check.py

連係カメラ（iPhone）は起動に1〜3秒かかるため、
「開けたのにすぐには読めない」のが正常な挙動。
このツールは最大5秒待ってから判定する。
"""

import time

import cv2

WARMUP_SEC = 5.0

print("カメラを 0〜4 まで順に調べます。")
print("（iPhone が候補にある場合、画面がロックされていないか確認してください）\n")

available = []

for idx in range(5):
    print(f"--- index {idx} ---")
    cap = cv2.VideoCapture(idx)

    if not cap.isOpened():
        print("  開けません")
        cap.release()
        continue

    print("  開けました。フレームが来るまで待機中 ...", end="", flush=True)

    start = time.monotonic()
    frames = 0
    size = None

    while time.monotonic() - start < WARMUP_SEC:
        ok, frame = cap.read()
        if ok and frame is not None and frame.size > 0:
            frames += 1
            size = (frame.shape[1], frame.shape[0])
            if frames >= 3:      # 3枚連続で取れたら安定とみなす
                break
        time.sleep(0.1)

    elapsed = time.monotonic() - start

    if frames >= 3:
        print(f" OK ({elapsed:.1f}秒)")
        print(f"  解像度: {size[0]}x{size[1]}")
        available.append((idx, size, elapsed))
    else:
        print(f" フレームが来ませんでした ({elapsed:.1f}秒)")

    cap.release()
    time.sleep(0.3)   # 次の番号を試す前に解放を待つ

print("\n" + "=" * 40)
if available:
    print("使えるカメラ:")
    for idx, size, elapsed in available:
        hint = ""
        if size[0] >= 1280:
            hint = "  ← 高解像度。iPhone の可能性が高い"
        print(f"  index {idx} : {size[0]}x{size[1]}  (起動 {elapsed:.1f}秒){hint}")
    print("\nconfig.json の camera_url にこの番号を書いてください。例:")
    print(f'  "camera_url": {available[0][0]}')
else:
    print("使えるカメラが見つかりませんでした。")
    print("確認すること:")
    print("  1. ターミナルにカメラの使用許可があるか")
    print("     システム設定 → プライバシーとセキュリティ → カメラ")
    print("  2. iPhone が Mac の近くにあり、画面がロックされていないか")
    print("  3. iPhone 側: 設定 → 一般 → AirPlayと連係 → 連係カメラ がオン")
    print("  4. 他のアプリ（Zoom、FaceTime、Photo Booth）がカメラを掴んでいないか")
