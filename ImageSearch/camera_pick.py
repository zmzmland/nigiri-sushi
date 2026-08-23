"""
カメラを1台ずつ実際に映して、どれを使うか選ぶツール。
選んだ番号は config.json の camera_url に自動で書き込まれる。

実行:
    cd "/Users/x26086/My project/ImageSearch"
    python3 camera_pick.py

操作:
    y / Space … この映像を採用する
    n         … 次のカメラを見る
    ESC       … 何も変更せずに終了
"""

import json
import time
from pathlib import Path

import cv2

SCRIPT_DIR = Path(__file__).resolve().parent
CONFIG_PATH = SCRIPT_DIR / "config.json"

MAX_INDEX = 5
WARMUP_SEC = 6.0
WINDOW = "Camera Picker"


def find_cameras():
    """使えるカメラ番号を列挙する。"""
    found = []
    for idx in range(MAX_INDEX):
        cap = cv2.VideoCapture(idx)
        if not cap.isOpened():
            cap.release()
            continue

        start = time.monotonic()
        ok = False
        while time.monotonic() - start < WARMUP_SEC:
            got, frame = cap.read()
            if got and frame is not None and frame.size > 0:
                ok = True
                break
            time.sleep(0.1)

        cap.release()
        time.sleep(0.3)

        if ok:
            found.append(idx)
    return found


def preview(idx):
    """1台を映して、採用するかどうかの答えを返す。'yes' / 'no' / 'abort'"""
    cap = cv2.VideoCapture(idx)
    if not cap.isOpened():
        cap.release()
        return "no"

    start = time.monotonic()
    answer = "no"

    while True:
        got, frame = cap.read()

        if not got or frame is None or frame.size == 0:
            if time.monotonic() - start > WARMUP_SEC:
                break
            time.sleep(0.05)
            continue

        h, w = frame.shape[:2]

        # 上部に黒帯を敷いて案内を描く
        cv2.rectangle(frame, (0, 0), (w, 110), (0, 0, 0), -1)
        cv2.putText(frame, f"index {idx}   {w}x{h}", (20, 45),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0, 255, 255), 3)
        cv2.putText(frame, "y = use this   n = next   ESC = cancel", (20, 90),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2)

        cv2.imshow(WINDOW, frame)

        key = cv2.waitKey(30) & 0xFF
        if key in (ord('y'), ord('Y'), 32):
            answer = "yes"
            break
        if key in (ord('n'), ord('N')):
            answer = "no"
            break
        if key == 27:
            answer = "abort"
            break

    cap.release()
    return answer


def save_choice(idx):
    """config.json の camera_url だけを書き換える（他の設定は保持）。"""
    if CONFIG_PATH.exists():
        try:
            cfg = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as e:
            print(f"config.json を読めませんでした: {e}")
            print(f'手動で "camera_url": {idx} に書き換えてください。')
            return False
    else:
        cfg = {}

    cfg["camera_url"] = idx

    tmp = CONFIG_PATH.with_suffix(".json.tmp")
    tmp.write_text(json.dumps(cfg, indent=2, ensure_ascii=False) + "\n",
                   encoding="utf-8")
    tmp.replace(CONFIG_PATH)
    return True


def main():
    print("使えるカメラを探しています ...")
    cams = find_cameras()

    if not cams:
        print("カメラが見つかりませんでした。camera_check.py を実行してください。")
        return

    print(f"見つかった番号: {cams}")
    print("1台ずつ映します。iPhone の映像が出たら y を押してください。\n")

    chosen = None
    for idx in cams:
        print(f"index {idx} を表示中 ...")
        ans = preview(idx)

        if ans == "yes":
            chosen = idx
            break
        if ans == "abort":
            print("中止しました。config.json は変更していません。")
            cv2.destroyAllWindows()
            return

    cv2.destroyAllWindows()

    if chosen is None:
        print("\nどれも選ばれませんでした。config.json は変更していません。")
        print("iPhone が映らない場合の確認:")
        print("  ・iPhone の画面がロックされていないか（連係カメラはロック解除が必要）")
        print("  ・Mac の近くにあり、Bluetooth と Wi-Fi が有効か")
        print("  ・設定 → 一般 → AirPlayと連係 → 連係カメラ がオン")
        return

    if save_choice(chosen):
        print(f"\n✅ config.json の camera_url を {chosen} にしました。")
        print("   python3 main.py で起動できます。")


if __name__ == "__main__":
    main()
