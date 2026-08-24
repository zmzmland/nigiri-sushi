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

ORDER_PATH     = BASE_DIR / "order.txt"
RESULT_PATH    = BASE_DIR / "result.txt"
TRIGGER_PATH   = BASE_DIR / "capture_trigger.txt"

# 生存確認用。1秒ごとに書き換え、Unity 側がこの更新時刻を見ている。
HEARTBEAT_PATH = BASE_DIR / "heartbeat.txt"

# 自動判定のカウントダウン。"3" "2" "1" と書き、判定していない間は消す。
COUNTDOWN_PATH = BASE_DIR / "countdown.txt"

print(f"共有フォルダ : {BASE_DIR}")


# ----------------------------
# 設定（config.json で上書きできる）
# ----------------------------
DEFAULTS = {
    # 数値・文字列・リストのいずれも可。リストなら先頭から順に試す。
    "camera_url": [0, 1, 2],

    # 既定値は共有されるモデルを指す。
    "model_path": "models/best.pt",

    "conf": 0.5,
    "iou": 0.5,
    "agnostic_nms": True,
    "match_mode": "lcs",
    "ng_class": "NG",

    # 1 なら毎フレーム推論。重いときだけ 2〜4 に上げる
    "preview_every": 1,

    # カメラが最初のフレームを返すまで待つ秒数。
    "camera_warmup": 6.0,

    # Unity 側の Space（capture_trigger.txt）を確認する間隔（秒）
    "trigger_poll": 0.1,

    # 生存確認を書き込む間隔（秒）
    "heartbeat_interval": 1.0,

    # 映像が途切れたときに再接続を試みる回数と間隔（秒）
    "reconnect_attempts": 10,
    "reconnect_wait": 2.0,

    # ---- 自動判定 ----
    # 寿司を置き終わって手を引いたら、勝手に判定する。
    "auto_judge": True,

    # 検出内容がこの秒数変わらなければ「置き終わった」とみなす
    "auto_stable_seconds": 2.0,

    # そこから判定までのカウントダウン（秒）。この間に手を出せば中断。
    "auto_countdown_seconds": 3.0,

    # 注文の数だけ置かれるまで自動判定しない。
    # False にすると1個でも置いてあれば判定してしまうので、
    # 「考え込んでいたら勝手に判定された」が起きやすくなる。
    "auto_require_full_count": True,

    # 検出のちらつきを無視するための連続一致回数
    "auto_debounce": 3,
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
        print("  カメラを選び直すには: python3 camera_pick.py")
    return cfg


CFG = load_config()
MATCH_MODE = CFG["match_mode"]
NG_CLASS = CFG["ng_class"]
PREVIEW_EVERY = max(1, int(CFG["preview_every"]))

AUTO_JUDGE      = bool(CFG["auto_judge"])
AUTO_STABLE     = float(CFG["auto_stable_seconds"])
AUTO_COUNTDOWN  = float(CFG["auto_countdown_seconds"])
AUTO_FULL_COUNT = bool(CFG["auto_require_full_count"])
AUTO_DEBOUNCE   = max(1, int(CFG["auto_debounce"]))

print(f"判定方式     : {MATCH_MODE}")
print(f"自動判定     : {'ON' if AUTO_JUDGE else 'OFF'}"
      f"（静止 {AUTO_STABLE:.1f}秒 → カウントダウン {AUTO_COUNTDOWN:.0f}秒）")


def safe_write(path, text):
    """一時ファイルに書いてから置き換える（Unity が書き込み途中を読むのを防ぐ）"""
    tmp = path.with_suffix(path.suffix + ".tmp")
    try:
        tmp.write_text(text, encoding="utf-8")
        os.replace(tmp, path)
    except OSError as e:
        print(f"書き込み失敗 {path}: {e}")


def read_order():
    """order.txt の注文リストを返す。無ければ空リスト。"""
    if not ORDER_PATH.exists():
        return []
    try:
        raw = ORDER_PATH.read_text(encoding="utf-8")
    except OSError:
        return []
    return [line.strip() for line in raw.splitlines() if line.strip()]


_last_countdown = None


def set_countdown(text):
    """カウントダウン表示を Unity に伝える。値が変わったときだけ書く。"""
    global _last_countdown
    if text == _last_countdown:
        return
    _last_countdown = text

    if text:
        safe_write(COUNTDOWN_PATH, text)
    else:
        COUNTDOWN_PATH.unlink(missing_ok=True)


# ----------------------------
# 判定ロジック
# ----------------------------
def lcs_length(a, b):
    """順序を保ったまま一致する最大個数（最長共通部分列）。"""
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
    v = CFG["camera_url"]
    return list(v) if isinstance(v, list) else [v]


def open_camera(verbose=True):
    """候補を順に試して、最初に使えたものを返す。全滅なら None。"""
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
        safe_write(HEARTBEAT_PATH, str(time.time()))
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
    raise SystemExit(
        f"モデルが見つかりません: {model_path}\n"
        "  config.json の model_path を確認してください。\n"
        "  初回セットアップなら、まずこれを実行してください:\n"
        "    cp config.example.json config.json"
    )
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


def draw_hud(img, manual, order_len, det_count, auto_state, remain):
    """操作説明・手動カウンタ・自動判定の状態を画面下に出す。

    OpenCV は日本語を描けないので英字。係員が見るための情報。
    """
    h, w = img.shape[:2]

    help_text = ("SPACE=judge   a/s=manual +/-   ENTER=confirm manual   "
                 "t=auto on/off   ESC=quit")
    cv2.rectangle(img, (0, h - 34), (w, h), (0, 0, 0), -1)
    cv2.putText(img, help_text, (10, h - 12),
                cv2.FONT_HERSHEY_SIMPLEX, 0.45, (200, 200, 200), 1)

    y = h - 34

    if manual > 0:
        y -= 44
        label = f"MANUAL {manual}/{order_len}  -  press ENTER to send"
        cv2.rectangle(img, (0, y), (w, y + 44), (0, 90, 160), -1)
        cv2.putText(img, label, (10, y + 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)

    if auto_state:
        y -= 44
        if auto_state == "countdown":
            label = f"AUTO JUDGE IN {remain:.0f}"
            color = (0, 140, 220)
        elif auto_state == "waiting":
            label = f"AUTO: placed {det_count}/{order_len}"
            color = (60, 60, 60)
        else:  # off
            label = "AUTO: off"
            color = (40, 40, 40)

        cv2.rectangle(img, (0, y), (w, y + 44), color, -1)
        cv2.putText(img, label, (10, y + 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)


# ----------------------------
# 判定1回分
# ----------------------------
def run_judgement(frame, label="判定"):
    order = read_order()

    if not order:
        print("order.txt が空です。Unity 側で注文が確定していますか？")
        return False

    # Unity のスプライト名と YOLO のクラス名がズレていないか確認する。
    known = set(model.names.values())
    unknown = sorted({n for n in order if n not in known})
    if unknown:
        print()
        print("★★★ 警告: モデルが知らない注文があります ★★★")
        print(f"  注文にあってモデルに無い : {unknown}")
        print(f"  モデルが知っているクラス : {sorted(known)}")
        print("  → これらは何を置いても正解になりません。")
        print()

    # 判定時は必ずそのフレームで推論し直す（間引きの影響を受けない）
    dets = detect(frame)

    detected = [name for name, _, _ in dets]
    ng_count = detected.count(NG_CLASS)

    correct = judge(order, detected)

    print("-------------------")
    print(f"【{label}】")
    print("注文 :", order)
    print("検出 :", detected, f"(NG {ng_count} 個)")
    print(f"{correct}/{len(order)} 正解  [{MATCH_MODE}]")
    print("-------------------")

    safe_write(RESULT_PATH, str(correct))
    return True


def confirm_manual(manual):
    """手動カウンタを確定して Unity に送る（デバッグ・展示の保険用）。"""
    order = read_order()

    if not order:
        print("order.txt が空です。まだ注文が確定していません。")
        return False

    value = max(0, min(manual, len(order)))

    print("-------------------")
    print(f"【手動】{value}/{len(order)} 正解として送信しました")
    print("-------------------")

    safe_write(RESULT_PATH, str(value))
    return True


# ----------------------------
# メインループ
# ----------------------------
TRIGGER_PATH.unlink(missing_ok=True)
COUNTDOWN_PATH.unlink(missing_ok=True)

print()
print("=" * 50)
print("  操作")
print("=" * 50)
print("  （通常は操作不要。置き終われば自動で判定されます）")
print()
print("  Space  : すぐ判定する")
print("  a / s  : 手動カウンタ +1 / -1  ※デバッグ・保険用")
print("  Enter  : 手動カウンタを確定して送信")
print("  t      : 自動判定の ON / OFF")
print("  ESC    : 終了")
print("=" * 50)
print()

manual_correct = 0
dets = []
frame_no = 0
last_trigger_check = 0.0
last_heartbeat = 0.0
last_order_mtime = None

# ---- 自動判定の状態 ----
auto_enabled = AUTO_JUDGE
auto_armed = False          # この面でまだ判定していないか
auto_sig = None             # 確定している検出内容
auto_sig_since = 0.0
pending_sig = None
pending_hits = 0
countdown_started = None


def disarm_auto(reason=""):
    """判定が済んだので、次の注文が来るまで自動判定を止める。"""
    global auto_armed, countdown_started
    auto_armed = False
    countdown_started = None
    set_countdown("")
    if reason:
        print(f"自動判定を停止しました（{reason}）")


while True:

    ret, frame = cap.read()

    if not ret:
        if reconnect():
            continue
        break

    frame_no += 1
    now = time.monotonic()

    # プレビュー用の推論は preview_every フレームに1回（既定は毎フレーム）
    refreshed = (frame_no % PREVIEW_EVERY == 0)
    if refreshed:
        dets = detect(frame)

    # ---- Unity へ「生きている」と伝える ----
    if now - last_heartbeat > float(CFG["heartbeat_interval"]):
        last_heartbeat = now
        safe_write(HEARTBEAT_PATH, str(time.time()))

    order_now = read_order()

    # =========================================================
    #  自動判定
    # =========================================================
    #  検出内容が一定時間変わらない = 手が画面から出て、置き終わった。
    #  そこからカウントダウンし、途中で内容が変われば中断する。
    # =========================================================
    if refreshed:
        # 左右の位置がわずかに揺れても影響しないよう、名前の並びをそろえて比較する
        sig = tuple(sorted(name for name, _, _ in dets))

        if sig != auto_sig:
            # 1フレームだけのちらつきで中断しないよう、数回続いたら採用する
            if sig == pending_sig:
                pending_hits += 1
            else:
                pending_sig = sig
                pending_hits = 1

            if pending_hits >= AUTO_DEBOUNCE:
                auto_sig = sig
                auto_sig_since = now
                if countdown_started is not None:
                    countdown_started = None
                    set_countdown("")
                    print("内容が変わったので判定を中断しました")
        else:
            pending_sig = None
            pending_hits = 0

    auto_state = None
    remain = 0.0

    if not auto_enabled:
        auto_state = "off"
    elif auto_armed and order_now and auto_sig is not None:
        need = len(order_now)
        have = len(auto_sig)

        enough = (have >= need) if AUTO_FULL_COUNT else (have > 0)
        stable_for = now - auto_sig_since

        if enough and stable_for >= AUTO_STABLE:
            if countdown_started is None:
                countdown_started = now
                print(f"置き終わりを検知しました（{have}貫）。判定します…")

            remain = AUTO_COUNTDOWN - (now - countdown_started)

            if remain <= 0:
                set_countdown("")
                run_judgement(frame, label="自動判定")
                manual_correct = 0
                disarm_auto()
                auto_state = None
            else:
                set_countdown(str(int(remain) + 1))
                auto_state = "countdown"
        else:
            if countdown_started is not None:
                countdown_started = None
                set_countdown("")
                print("判定を中断しました")
            auto_state = "waiting"

    display = frame.copy()
    draw(display, dets)
    draw_hud(display, manual_correct, len(order_now), len(dets), auto_state, remain)
    cv2.imshow("Sushi Judge", display)

    key = cv2.waitKey(1) & 0xFF

    # ---- Unity 側の Space（capture_trigger.txt）を確認する ----
    if now - last_trigger_check > CFG["trigger_poll"]:
        last_trigger_check = now

        # 注文が変わった = 面が変わった。手動カウンタを持ち越さず、自動判定を再開する。
        try:
            mtime = ORDER_PATH.stat().st_mtime if ORDER_PATH.exists() else None
        except OSError:
            mtime = None

        if mtime != last_order_mtime:
            last_order_mtime = mtime
            if manual_correct:
                print(f"注文が変わったので手動カウンタをリセットしました（{manual_correct} → 0）")
            manual_correct = 0

            auto_armed = True
            countdown_started = None
            auto_sig = None
            pending_sig = None
            pending_hits = 0
            set_countdown("")

        if TRIGGER_PATH.exists():
            TRIGGER_PATH.unlink(missing_ok=True)
            print("ゲーム画面から撮影トリガーを受信")
            run_judgement(frame, label="Space")
            manual_correct = 0
            disarm_auto()

    # ---- このカメラウィンドウでの Space でも判定できる ----
    if key == 32:
        run_judgement(frame, label="Space")
        manual_correct = 0
        disarm_auto()

    # ---- 手動補正（デバッグ・認識が不調なときの保険）----
    #      a / s で数を作り、Enter で確定する。
    if key == ord('a'):
        manual_correct += 1
        print(f"手動カウンタ : {manual_correct}  （Enter で確定）")

    if key == ord('s'):
        manual_correct = max(manual_correct - 1, 0)
        print(f"手動カウンタ : {manual_correct}  （Enter で確定）")

    if key in (10, 13):   # Enter / Return
        if confirm_manual(manual_correct):
            disarm_auto()
        manual_correct = 0

    # ---- 自動判定の ON / OFF ----
    if key == ord('t'):
        auto_enabled = not auto_enabled
        countdown_started = None
        set_countdown("")
        print(f"自動判定を {'ON' if auto_enabled else 'OFF'} にしました")

    # ---- ESCキーで終了 ----
    if key == 27:
        break

cap.release()
cv2.destroyAllWindows()

HEARTBEAT_PATH.unlink(missing_ok=True)
COUNTDOWN_PATH.unlink(missing_ok=True)
print("終了しました")
