order = "maguro"
detected = ["ebi"]

if order in detected:
    result = "OK"
else:
    result = "NG"

with open("result.txt", "w", encoding="utf-8") as f:
    f.write(result)

print(result)