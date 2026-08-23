print("テストモード開始")

order = "maguro"
detected = ["maguro"]

print("注文 :", order)
print("検出 :", detected)

if order in detected:

    print("OK!")

    with open(
        "result.txt",
        "w",
        encoding="utf-8"
    ) as f:
        f.write("OK")

else:

    print("NG!")

    with open(
        "result.txt",
        "w",
        encoding="utf-8"
    ) as f:
        f.write("NG")