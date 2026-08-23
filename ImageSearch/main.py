from ultralytics import YOLO
import cv2
import os
import json
import time
from collections import Counter
from pathlib import Path

# ----------------------------
# パス設定（Unity の GamePaths と一致させること）
# ----------------------------
SCRIPT_DIR = Path(__file__).resolve().parent

BASE_DIR = Path.home() / "TGS_ImageSearch"
BASE_DIR.mkdir(parents=True, exist_ok=True)

ORDER_PATH   = BASE_DIR / "order.txt"
RESULT_PATH  = BASE_DIR / "result.txt"
TRIGGER_PATH = BASE_DIR / "capture_trigger.txt"

print(f"共有フォルダ : {BASE_DIR}")


# ----------------------------
# 設定（config.json で上書きできる）
# ----------------------------
DEFAULTS = {
    # 数値・文字列・リストのいずれも可。リストなら先頭から順に試す。
    # 例: [0, 1, 2] / 0 / "http://..../video"
    "camera_url": [0, 1, 2],
    "model_path": "runs/detect/train-6/weights/best.pt",
    "conf": 0.5,
    "iou": 0.5,
    "agnostic_nms": True,
    "match_mode": "lcs",
    "ng_class": "NG",

    # 1 なら毎フレーム推論。重いときだけ 2〜4 に上げる（項目7）
    "preview_every": 1,

    # カメラが最初のフレームを返すまで待つ秒数。
    # 連係カメラ(iPhone)は起動が遅いので短くしすぎないこと。
    "camera_warmup": 6.0,

    # Unity 側の Space（capture_trigger.txt）を確認する間隔（秒）
    "trigger_poll": 0.1,

    # 映像が途切れたときに再接続を試みる回数と間隔（秒）
    "reconnect_attempts": 10,
    "reconnect_wait": 2.0,
}


def load_config():
    cfg = dict(DEFAULTS)
    path = SCRIPT_DIR / "config.json"
    if path.exists():
        try:
            cfg.update(json.loads(path.read_text(encoding="utf-8")))
        except (OSError, json.JSONDecodeError) as e:
            print(f"config.json を読めませんでした（既定値で続行）: {e}")
    else:
        print("config.json がありません。既定値で動作します。")
    return cfg


CFG = load_config()
MATCH_MODE = CFG["match_mode"]
NG_CLASS = CFG["ng_class"]
PREVIEW_EVERY = max(1, int(CFG["preview_every"]))

print(f"判定方式     : {MATCH_MODE}")


def safe_write(path, text):
    """一時ファイルに書いてから置き換える（Unity が書き込み途中を読むのを防ぐ）"""
    tmp = path.with_suffix(path.suffix + ".tmp")
    try:
        tmp.write_text(text, encoding="utf-8")
        os.replace(tmp, path)
    except OSError as e:
        print(f"書き込み失敗 {path}: {e}")


# ----------------------------
# 判定ロジック
# ----------------------------
def lcs_length(a, b):
    """順序を保ったまま一致する最大個数（最長共通部分列）。

    位置ごとの単純比較と違い、途中で1個の検出漏れや余分な検出があっても
    それ以降が全部ズレて不正解になることがない。
    """
    if not a or not b:
        return 0
    prev = [0] * (len(b) + 1)
    for x in a:
        cur = [0]
        for j, y in enumerate(b):
            cur.append(prev[j] + 1 if x == y else max(prev[j + 1], cur[j]))
        prev = cur
    return prev[-1]


def multiset_match(order, detected):
    """並び順を問わず、品目ごとの個数だけで一致数を数える。"""
    o, d = Counter(order), Counter(detected)
    return sum(min(o[k], d[k]) for k in o)


def judge(order, detected):
    if MATCH_MODE == "multiset":
        return multiset_match(order, detected)
    return lcs_length(order, detected)


# ----------------------------
# カメラ
# ----------------------------
def camera_candidates():
    """config の camera_url を、試す順のリストに正規化する。"""
    v = CFG["camera_url"]
    return list(v) if isinstance(v, list) else [v]


def open_camera(verbose=True):
    """候補を順に試して、最初に使えたものを返す。全滅なら None。

    連係カメラ（iPhone）は起動に1〜3秒かかり、その間 read() は失敗する。
    開いた直後に1枚読むだけだと「使えない」と誤判定するので、
    warmup_sec の間はフレームが来るのを待つ。
    """
    warmup = float(CFG["camera_warmup"])

    for src in camera_candidates():
        cap = cv2.VideoCapture(src)

        if not cap.isOpened():
            cap.release()
            if verbose:
                print(f"  カメラ {src!r} : 開けません")
            continue

        if verbose:
            print(f"  カメラ {src!r} : 開けました。フレーム待機中 ...", end="", flush=True)

        start = time.monotonic()
        frames = 0

        while time.monotonic() - start < warmup:
            ok, frame = cap.read()
            if ok and frame is not None and frame.size > 0:
                frames += 1
                if frames >= 2:
                    break
            time.sleep(0.1)

        if frames >= 2:
            if verbose:
                print(f" OK ({time.monotonic() - start:.1f}秒)")
                print(f"カメラ接続 : {src!r}")
            return cap, src

        cap.release()
        if verbose:
            print(" フレームが来ませんでした")

    return None, None


cap, camera_src = open_camera()
if cap is None:
    raise SystemExit(
        "カメラを開けませんでした。\n"
        f"  試した候補: {camera_candidates()}\n"
        "  iPhone を使う場合は、Mac の近くにあり画面がロックされていないか確認してください。\n"
        "  診断ツールを実行してください:\n"
        "    python3 camera_check.py"
    )


def reconnect():
    """映像が途切れたときに再接続を試みる。成功したら True。"""
    global cap
    attempts = int(CFG["reconnect_attempts"])
    wait = float(CFG["reconnect_wait"])

    cap.release()

    for i in range(1, attempts + 1):
        print(f"映像が途切れました。再接続を試行中 ({i}/{attempts}) ...")
        time.sleep(wait)
        new_cap, src = open_camera(verbose=False)
        if new_cap is not None:
            cap = new_cap
            print(f"再接続しました : {src!r}")
            return True

    print("再接続できませんでした。終了します。")
    return False


# ----------------------------
# 学習済みモデル
# ----------------------------
model_path = SCRIPT_DIR / CFG["model_path"]
if not model_path.exists():
    raise SystemExit(f"モデルが見つかりません: {model_path}")
model = YOLO(str(model_path))
print(model.names)


def detect(frame):
    """左→右に並べた検出結果 [(名前, (x1,y1,x2,y2), 信頼度), ...] を返す。"""
    results = model(
        frame,
        conf=CFG["conf"],
        iou=CFG["iou"],
        agnostic_nms=CFG["agnostic_nms"],
        verbose=False,
    )

    out = []
    for result in results:
        for box in result.boxes:
            cls = int(box.cls[0])
            out.append((
                model.names[cls],
                tuple(map(int, box.xyxy[0])),
                float(box.conf[0]),
            ))

    out.sort(key=lambda item: item[1][0])
    return out


def draw(frame, dets):
    for name, (x1, y1, x2, y2), conf in dets:
        color = (0, 0, 255) if name == NG_CLASS else (0, 255, 0)
        cv2.rectangle(frame, (x1, y1), (x2, y2), color, 2)
        cv2.putText(
            frame,
            f"{name} {conf:.2f}",
            (x1, max(y1 - 10, 12)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.8,
            color,
            2,
        )


# ----------------------------
# 判定1回分
# ----------------------------
def run_judgement(frame):
    raw = ORDER_PATH.read_text(encoding="utf-8") if ORDER_PATH.exists() else ""
    order = [line.strip() for line in raw.splitlines() if line.strip()]

    if not order:
        print("order.txt が空です。Unity 側で注文が確定していますか？")
        return

    # Unity のスプライト名と YOLO のクラス名がズレていないか確認する。
    # ズレていると、そのネタは絶対に正解にならない（静かに失敗するので危険）。
    known = set(model.names.values())
    unknown = sorted({n for n in order if n not in known})
    if unknown:
        print()
        print("★★★ 警告: モデルが知らない注文があります ★★★")
        print(f"  注文にあってモデルに無い : {unknown}")
        print(f"  モデルが知っているクラス : {sorted(known)}")
        print("  → これらは何を置いても正解になりません。")
        print("     Unity の Sprite のファイル名と data.yaml のクラス名を")
        print("     完全に一致させてください（大文字小文字も区別されます）。")
        print()

    # 判定時は必ずそのフレームで推論し直す（間引きの影響を受けない）
    dets = detect(frame)

    # NG は捨てずに「不正解の1個」として列に残す。
    # 捨てると位置がズレて、以降が全部不正解になる。
    detected = [name for name, _, _ in dets]
    ng_count = detected.count(NG_CLASS)

    correct = judge(order, detected)

    print("-------------------")
    print("注文 :", order)
    print("検出 :", detected, f"(NG {ng_count} 個)")
    print(f"{correct}/{len(order)} 正解  [{MATCH_MODE}]")
    print("-------------------")

    safe_write(RESULT_PATH, str(correct))


# ----------------------------
# メインループ
# ----------------------------
TRIGGER_PATH.unlink(missing_ok=True)

print("テストモード開始")
print("Spaceキー：判定（ゲーム画面・カメラ画面のどちらでも可）")
print("aキー：手動で正解数+1   sキー：-1")
print("ESCキー：終了")

manual_correct = 0
dets = []
frame_no = 0
last_trigger_check = 0.0

while True:

    ret, frame = cap.read()

    if not ret:
        if reconnect():
            continue
        break

    frame_no += 1

    # プレビュー用の推論は preview_every フレームに1回（既定は毎フレーム）
    if frame_no % PREVIEW_EVERY == 0:
        dets = detect(frame)

    display = frame.copy()
    draw(display, dets)
    cv2.imshow("Sushi Judge", display)

    key = cv2.waitKey(1) & 0xFF

    # Unity 側の Space（capture_trigger.txt）を確認する。
    # フルスクリーンのビルドでは、プレイヤーはこのカメラウィンドウに
    # 触れないため、ゲーム側からも判定を起動できるようにしている。
    now = time.monotonic()
    if now - last_trigger_check > CFG["trigger_poll"]:
        last_trigger_check = now
        if TRIGGER_PATH.exists():
            TRIGGER_PATH.unlink(missing_ok=True)
            print("ゲーム画面から撮影トリガーを受信")
            run_judgement(frame)

    # このカメラウィンドウでの Space でも判定できる（動作確認用）
    if key == 32:
        run_judgement(frame)

    # 手動補正（認識が不調なときの保険）
    if key == ord('a'):
        manual_correct += 1
        safe_write(RESULT_PATH, str(manual_correct))
        print(f"手動正解 : {manual_correct}")

    if key == ord('s'):
        manual_correct = max(manual_correct - 1, 0)
        safe_write(RESULT_PATH, str(manual_correct))
        print(f"手動正解 : {manual_correct}")

    # ESCキーで終了
    if key == 27:
        break

cap.release()
cv2.destroyAllWindows()
