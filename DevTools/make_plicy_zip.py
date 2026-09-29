# PLiCy に上げる zip を作る
#   python DevTools/make_plicy_zip.py
# Builds/WebGL を、フォルダごと Builds/CrazyBowling_plicy.zip にまとめる（zip の一番上が WebGL フォルダ）。
# PLiCy の解説どおり「ビルドしたフォルダをエクスプローラーで zip にした」のと同じ形。
# ★更新のときは、前と同じ形（フォルダ名・ファイル名）にすること（PLiCy の FAQ：差分の更新は構成が完全に一致している必要がある）。
# PowerShell 5.1 の Compress-Archive は区切りを「\」で書くことがあるので使わない。
import os
import sys
import zipfile

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = os.path.join(ROOT, "Builds", "WebGL")
OUT = os.path.join(ROOT, "Builds", "CrazyBowling_plicy.zip")

if not os.path.isfile(os.path.join(SRC, "index.html")):
    sys.exit("Builds/WebGL/index.html が無い。先に WebGL でビルドすること")

names = []
with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for folder, _, files in os.walk(SRC):
        for f in sorted(files):
            full = os.path.join(folder, f)
            rel = os.path.relpath(full, os.path.dirname(SRC)).replace(os.sep, "/")
            # 英数字・記号以外の名前は PLiCy で文字化けすることがあるので止める
            if not rel.isascii():
                sys.exit(f"英数字以外の名前がある：{rel}")
            # すでに gzip のファイルは縮まないので、そのまま入れる
            kind = zipfile.ZIP_STORED if f.endswith(".gz") else zipfile.ZIP_DEFLATED
            z.write(full, rel, compress_type=kind)
            names.append((rel, os.path.getsize(full)))

print(f"作った：{os.path.abspath(OUT)}（{os.path.getsize(OUT) / 1048576:.2f}MB）")
for rel, size in sorted(names):
    print(f"  {rel}  {size / 1048576:.2f}MB")
