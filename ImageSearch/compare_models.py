"""
学習済みモデルを「同じテストセット」で評価して比較する。

results.csv の数字は学習中の検証セット（valid）に対する値で、学習設定や
データが違うと直接比較できない。本番でどれを使うかは必ずこれで決める。

実行:
    cd "/Users/x26086/My project/ImageSearch"
    python compare_models.py

出力の見方:
    mAP50     … ざっくり「ちゃんと見つけて正しく分類できたか」。実用上はこれ重視。
    mAP50-95  … 枠の位置精度まで含めた厳しい指標。
    P (適合率) … 検出したもののうち正しかった割合。低い＝誤検出が多い。
    R (再現率) … あるべきもののうち見つけられた割合。低い＝見逃しが多い。

このゲームでは「見逃し＝不正解」に直結するので、R が高いモデルを優先するとよい。
"""

from pathlib import Path

from ultralytics import YOLO

SCRIPT_DIR = Path(__file__).resolve().parent
RUNS_DIR = SCRIPT_DIR / "runs" / "detect"
DATA_YAML = SCRIPT_DIR / "data.yaml"

if not DATA_YAML.exists():
    raise SystemExit(f"data.yaml が見つかりません: {DATA_YAML}")

# best.pt を持つ run を自動で拾う
candidates = sorted(
    (d.name, d / "weights" / "best.pt")
    for d in RUNS_DIR.iterdir()
    if d.is_dir() and (d / "weights" / "best.pt").exists()
)

if not candidates:
    raise SystemExit(f"評価できる重みが見つかりません: {RUNS_DIR}")

print(f"評価対象: {[n for n, _ in candidates]}")
print(f"データ  : {DATA_YAML}  (split=test)\n")

rows = []
names_by_model = {}

for name, weights in candidates:
    print(f"\n{'=' * 40}\n  {name}\n{'=' * 40}")
    model = YOLO(str(weights))
    names_by_model[name] = tuple(model.names[i] for i in sorted(model.names))

    # split="test" = 学習にも検証にも使っていない画像で測る
    metrics = model.val(
        data=str(DATA_YAML),
        split="test",
        conf=0.001,   # 評価時は低くして PR カーブ全体を見る（本番の conf とは別物）
        iou=0.6,
        verbose=False,
    )

    box = metrics.box
    rows.append((name, box.mp, box.mr, box.map50, box.map))

    print("\n  --- クラス別 mAP50-95 ---")
    for i in sorted(model.names):
        try:
            print(f"    {model.names[i]:8s} {box.maps[i]:.3f}")
        except (IndexError, TypeError):
            pass

# ----------------------------
# クラス順の一致確認（ここがズレると全部誤判定になる）
# ----------------------------
print(f"\n{'=' * 40}\n  クラス順の確認\n{'=' * 40}")
distinct = set(names_by_model.values())
for name, ns in names_by_model.items():
    print(f"  {name:10s} {list(ns)}")
if len(distinct) > 1:
    print("\n  ★★ 警告: モデル間でクラスの順序が違います ★★")
    print("  順序が違う重みを使うと、検出はできても名前が全部ずれます。")
    print("  data.yaml と一致する方だけを使ってください。")
else:
    print("\n  ✅ 全モデルでクラス順が一致しています")

# ----------------------------
# まとめ
# ----------------------------
print(f"\n{'=' * 40}\n  まとめ\n{'=' * 40}")
print(f"  {'model':10s} {'P':>7s} {'R':>7s} {'mAP50':>8s} {'mAP50-95':>9s}")
for name, p, r, m50, m in rows:
    print(f"  {name:10s} {p:7.3f} {r:7.3f} {m50:8.3f} {m:9.3f}")

best = max(rows, key=lambda x: x[3])   # mAP50 で選ぶ
print(f"\n  → 推奨: {best[0]}")
print("     config.json の model_path を次に書き換えてください:")
print(f'     "model_path": "runs/detect/{best[0]}/weights/best.pt"')

print("\n  ※ テストセットは13枚と少ないため、差が 0.02 程度なら誤差の範囲です。")
print("     僅差なら、実機のカメラ映像で見比べて決めてください。")
