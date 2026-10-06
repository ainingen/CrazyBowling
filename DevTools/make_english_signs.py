# 英語版（CB_LANG_EN）で使う、ゲームの中の看板の画像を作る。元の日本語の画像は変えない
#   python DevTools/make_english_signs.py
# 作るもの（Assets/Textures/Jet/）
#   JetNoBowling_EN.png … 7本目の翼の上の看板。日本語の行を「PLEASE DO NOT BOWL ON THE WING」に描き替える
#   JetScreen_EN.png    … 7本目のピンの奥の大画面。日本語の行「つぎの行き先：月」を消す（上に英語の NEXT STOP: THE MOON がある）
#   JetLogo_EN.png      … 7本目の機体のロゴ。日本語の行「ギラギラ航空」を「OFFICIAL CARRIER TO THE MOON」に描き替える（色と縁取りは元の行と同じ）
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


def logo():
    im = Image.open(os.path.join(JET, "JetLogo.png")).convert("RGBA")
    # 日本語の行「ギラギラ航空」（y 441〜618）を消す。上の社名 GIRAGIRA AIR（y 58〜360）には触れない
    clear = Image.new("RGBA", (im.width, im.height - 400), (0, 0, 0, 0))
    im.paste(clear, (0, 400))
    words = "OFFICIAL CARRIER TO THE MOON".split(" ")
    outline = (40, 5, 71, 255)
    # 元の日本語の行と同じ中心にそろえる。幅は社名（約1750ピクセル）より狭い 1400 まで、高さは元の行より低いので、社名より目立たない
    max_width, center = 1400, ((522 + 1533) / 2, (441 + 618) / 2)
    probe = ImageDraw.Draw(im)

    def measure(size):
        font = bold_font(size)
        stroke = max(4, size // 9)
        gap = size * 0.45   # 縁取りでくっつかないよう、単語の間を広めにとる
        widths = [probe.textlength(w, font=font) for w in words]
        return font, stroke, gap, widths, sum(widths) + gap * (len(words) - 1) + 2 * stroke

    size = 120
    while True:
        font, stroke, gap, widths, total = measure(size)
        if total <= max_width:
            break
        size -= 2
    box = probe.textbbox((0, 0), "OFFICIAL", font=font)
    h = box[3] - box[1]
    x = center[0] - total / 2 + stroke
    y = center[1] - h / 2 - box[1]
    places = []
    for w, ww in zip(words, widths):
        places.append((x, w))
        x += ww + gap
    # 縁取り（濃い紫）
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    for px, w in places:
        ld.text((px, y), w, font=font, fill=outline, stroke_width=stroke, stroke_fill=outline)
    im.alpha_composite(layer)
    # 中の色：元の行と同じ、左の黄色（255,186,25）から右の橙（255,132,25）へのグラデーション
    mask = Image.new("L", im.size, 0)
    md = ImageDraw.Draw(mask)
    for px, w in places:
        md.text((px, y), w, font=font, fill=255)
    grad = Image.new("RGBA", im.size)
    gd = ImageDraw.Draw(grad)
    x0, x1 = center[0] - total / 2, center[0] + total / 2
    for gx in range(im.width):
        t = min(1.0, max(0.0, (gx - x0) / (x1 - x0)))
        gd.line([(gx, 0), (gx, im.height)], fill=(255, int(186 + (132 - 186) * t), 25, 255))
    fill = Image.new("RGBA", im.size, (0, 0, 0, 0))
    fill.paste(grad, (0, 0), mask)
    im.alpha_composite(fill)
    im.save(os.path.join(JET, "JetLogo_EN.png"))
    return size


if __name__ == "__main__":
    no_bowling()
    screen()
    size = logo()
    print(f"作った：JetNoBowling_EN.png・JetScreen_EN.png・JetLogo_EN.png（ロゴの英語の行は {size}）")
