# にぎり寿司

カメラで寿司を認識して採点する、Unity + YOLOv8 のゲームです。

お客さんの注文どおりに寿司を並べ、Space キーで判定します。カメラが実際の
寿司を画像認識し、正解数に応じて得点が入ります。

---

## 仕組み

**Unity（ゲーム）** と **Python（画像認識）** の2つが同時に動きます。
2つはテキストファイルをやりとりして連携します。

```
~/TGS_ImageSearch/            ← ホームフォルダの直下に自動で作られます
    order.txt                 Unity が書く → Python が読む（注文リスト）
    result.txt                Python が書く → Unity が読む（正解数）
    capture_trigger.txt       Unity が書く → Python が読む（判定の合図）
```

このフォルダの場所は、Unity 側は `Assets/GamePaths.cs`、Python 側は
`ImageSearch/main.py` の `BASE_DIR` で決まっています。**片方だけ変えると
連携が切れる**ので、変更するときは必ず両方を直してください。

---

## はじめかた

初めて触る人は、まず **[チーム向け_はじめかた.md](チーム向け_はじめかた.md)** を
読んでください。GitHub Desktop を使った手順を、画面の操作から順に書いています。

慣れている人向けの要約です。

```bash
git clone <このリポジトリのURL>
cd "My project"

# 画像認識の環境を作る
cd ImageSearch
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt

# 自分のカメラを選ぶ
cp config.example.json config.json
python3 camera_pick.py
```

Unity は **Unity Hub からこのフォルダを開く**だけです。初回は `Library` の
生成に数分かかります。

---

## 遊びかた

**ターミナルで画像認識を起動します。**

```bash
cd ImageSearch
source .venv/bin/activate
python3 main.py
```

**Unity で `Scenes/SampleScene` を開いて Play** します。

| キー | 動作 |
|---|---|
| Space | 寿司を並べたら判定 |
| a / s | 手動で加点・減点（認識が不調なときの保険） |
| ESC 長押し | ビルドしたアプリを終了（2秒） |
| F9 | 【開発用】判定を偽装して次のシーンへ |

Space はゲーム画面・カメラ画面のどちらでも効きます。

---

## フォルダ構成

```
Assets/                 Unity のゲーム本体
    GamePaths.cs        共有フォルダのパスを一元管理
    Customer.cs         1面の客。注文の提示と判定待ち
    Customer2.cs        2面の客
    ResultData.cs       シーンをまたぐ得点。static なので要リセット
    CaptureTrigger.cs   ゲーム画面の Space を Python に伝える
    QuitHandler.cs      ESC 長押しで終了
    TitleButton.cs      リザルトからタイトルへ戻る
    Scenes/             SampleScene → Game Scene → WaitScene
                        → Game Scene 2 → ResultScene

ImageSearch/            画像認識（Python）
    main.py             本体。カメラ・推論・判定
    camera_pick.py      使うカメラを映像で選ぶ
    camera_check.py     カメラの診断
    compare_models.py   モデルを同じテストセットで比較
    train.py            学習
    models/best.pt      採用中のモデル
    data.yaml           クラス定義
    TGS_sushi/          学習用データセット
    config.example.json 設定のテンプレート
```

---

## 認識できる寿司

`data.yaml` で定義しています。現在は5種類 + NG です。

```
NG / ika / maguro / salmon / tai / tamago
```

### ⚠️ ネタを追加・変更するとき

**Unity のスプライトのファイル名と、YOLO のクラス名を完全に一致させてください。**
大文字小文字も区別されます。

`Customer.cs` は注文をこう書き出します。

```csharp
sb.AppendLine(s.name);   // Sprite のアセット名がそのまま order.txt に入る
```

Python 側はこれを `model.names` の値と文字列比較するので、`tako.png` を
追加してもモデルに `tako` クラスが無ければ、**何を置いても永久に不正解**に
なります。ズレていれば `main.py` が判定時に警告を出しますが、そもそも
揃えるのが前提です。

ネタを変えるときの手順は、`ImageSearch/dist_files/アプリ化手順.md` と
プロジェクトのロードマップにまとめてあります。

---

## モデルについて

学習結果は `ImageSearch/runs/` に溜まりますが、**このフォルダは Git で
共有していません**（1回数十MB あるため）。

採用したモデルだけを `ImageSearch/models/best.pt` に置いて共有します。
新しく学習したモデルを採用するときは、次の手順で入れ替えてください。

```bash
cd ImageSearch
python3 compare_models.py                       # 同じテストセットで比較
cp runs/detect/<勝ったrun>/weights/best.pt models/best.pt
```

### ⚠️ クラスの順序を必ず確認すること

モデルを差し替えたら、起動時に表示されるクラス辞書が `data.yaml` と
一致しているか確認してください。

```
{0: 'NG', 1: 'ika', 2: 'maguro', 3: 'salmon', 4: 'tai', 5: 'tamago'}
```

順序が違うモデルを使うと、**検出はできるのに名前が全部ずれます。**
過去に `train-5` がこの状態でした。学習ログ（results.csv）の数字だけで
モデルを選ばず、必ず `compare_models.py` で測ってください。

---

## 困ったとき

**カメラが映らない**
iPhone を連係カメラとして使っています。画面のロックを解除し、Mac の近くに
置いてください。それでも駄目なら `python3 camera_pick.py` で選び直します。

**判定してもゲームが進まない**
`cat ~/TGS_ImageSearch/order.txt` で注文が書かれているか確認します。
空なら Unity 側の書き込みが届いていません。

**連携だけ切り分けたい**
カメラなしで Unity 側だけ試せます。注文が出揃ってから実行してください。

```bash
echo "3" > ~/TGS_ImageSearch/result.txt
```

Unity が次のシーンへ進めば、ファイル経由の連携は正常です。

**`python` で動かない**
`python3` を使ってください。
