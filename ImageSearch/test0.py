from ultralytics import YOLO
import cv2

# ----------------------------
# 学習済みモデルを読み込む
# ----------------------------
model = YOLO("runs/detect/train-6/weights/best.pt")

# ----------------------------
# 判定したい画像
# ----------------------------
image_path = ""

# ----------------------------
# YOLOで認識
# ----------------------------
results = model.predict(
    source=image_path,
    conf=0.5,
    save=False
)

# 元画像を読み込み
image = cv2.imread(image_path)

print("=== 認識結果 ===")

for result in results:
    for box in result.boxes:
        cls = int(box.cls[0])
        name = model.names[cls]
        conf = float(box.conf[0])

        print(f"{name} : {conf:.2f}")

        x1, y1, x2, y2 = map(int, box.xyxy[0])

        # 枠を描画
        cv2.rectangle(image, (x1, y1), (x2, y2), (0, 255, 0), 2)

        # ラベルを描画
        cv2.putText(
            image,
            f"{name} {conf:.2f}",
            (x1, y1 - 10),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.7,
            (0, 255, 0),
            2
        )

# 結果を表示
cv2.imshow("Result", image)
cv2.waitKey(0)
cv2.destroyAllWindows()