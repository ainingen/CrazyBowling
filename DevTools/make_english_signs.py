# 英語版（CB_LANG_EN）で使う、ゲームの中の看板の画像を作る。元の日本語の画像は変えない
#   python DevTools/make_english_signs.py
# 作るもの（Assets/Textures/Jet/）
#   JetNoBowling_EN.png … 7本目の翼の上の看板。日本語の行を「PLEASE DO NOT BOWL ON THE WING」に描き替える
#   JetScreen_EN.png    … 7本目のピンの奥の大画面。日本語の行「つぎの行き先：月」を消す（上に英語の NEXT STOP: THE MOON がある）
# 文字は元の日本語の行と同じ Noto Sans JP の太字（Windows に入っているもの）で描く。
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
JET = os.path.join(HERE, "..", "Assets", "Textures", "Jet")
FONT = r"C:\Windows\Fonts\NotoSansJP-VF.ttf"


def bold_font(size):
    font = ImageFont.truetype(FONT, size)
    font.set_variation_by_name("Bold")
    return font


def no_bowling():
    im = Image.open(os.path.join(JET, "JetNoBowling.png")).convert("RGBA")
    d = ImageDraw.Draw(im)
    yellow = im.getpixel((100, 250))
    ink = (0x24, 0x10, 0x30, 255)
    # 日本語の行（上の段）を黄色で塗りつぶす（縞模様と下の英語の行には触れない）
    d.rectangle((40, 88, im.width - 40, 215), fill=yellow)
    text = "PLEASE DO NOT BOWL ON THE WING"
    size = 104
    while True:
        font = bold_font(size)
        box = d.textbbox((0, 0), text, font=font)
        if box[2] - box[0] <= im.width - 2 * 120:
            break
        size -= 2
    w, h = box[2] - box[0], box[3] - box[1]
    # 元の日本語の行と同じ高さの中心（y≈148）にそろえる
    d.text(((im.width - w) / 2 - box[0], 148 - h / 2 - box[1]), text, font=font, fill=ink)
    im.save(os.path.join(JET, "JetNoBowling_EN.png"))


def screen():
    im = Image.open(os.path.join(JET, "JetScreen.png")).convert("RGBA")
    # 日本語の行（y 312〜408）を、方眼の並び（32ピクセルごと）が同じ下の空いた場所から写して消す
    x0, x1, y0, y1 = 288, 768, 312, 408
    patch = im.crop((x0, y0 + 128, x1, y1 + 128))
    im.paste(patch, (x0, y0))
    im.save(os.path.join(JET, "JetScreen_EN.png"))


if __name__ == "__main__":
    no_bowling()
    screen()
    print("作った：JetNoBowling_EN.png・JetScreen_EN.png")
