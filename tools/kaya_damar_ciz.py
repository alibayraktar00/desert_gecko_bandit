"""
Altın damarlı kaya: kazma ya da dinamitle kırılan, içinden kristal çıkan kaya yığını.
İki kare: sağlam ve çatlak (ilk kazma darbesinden sonra). Oyundaki kaya renkleri (kaya_destek.png) ve altın.

Çıktı: Assets/Sprites/Environment/Props/kaya_damar_0.png, kaya_damar_1.png (alt orta pivot, 32 PPU)
Çalıştırma: python tools/kaya_damar_ciz.py
"""
import math
import random
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "Assets/Sprites/Environment/Props"
W, H = 40, 44

OUTLINE = (52, 24, 20, 255)
DARK = (108, 49, 41, 255)
MID = (148, 67, 46, 255)
LIGHT = (207, 141, 80, 255)
HIGH = (226, 168, 104, 255)
GOLD = (232, 184, 72, 255)
GOLD_HI = (255, 236, 160, 255)
CRACK = (40, 16, 14, 255)


def inside(x, y):
    """Yere oturmuş, tepesi yuvarlak iri bir kaya: alt kenar düz, üstü iki tümsek."""
    cx = W / 2
    if y > H - 1:
        return False
    # alt yarı: geniş elips gövde
    body = ((x - cx) / (W / 2 - 1)) ** 2 + ((y - (H - 14)) / 16) ** 2 <= 1 or (y >= H - 14 and abs(x - cx) <= W / 2 - 2)
    # üst: iki tümsek
    bump1 = ((x - (cx - 6)) / 11) ** 2 + ((y - 14) / 12) ** 2 <= 1
    bump2 = ((x - (cx + 7)) / 10) ** 2 + ((y - 19) / 11) ** 2 <= 1
    return body or bump1 or bump2


def shade(x, y):
    """Üst-sol ışık, alt-sağ gölge; biraz kırık doku."""
    nx = (x - W / 2) / (W / 2)
    ny = (y - H / 2) / (H / 2)
    light = -nx * 0.6 - ny * 0.9 + (random.random() - 0.5) * 0.35
    if light > 0.75:
        return HIGH
    if light > 0.15:
        return LIGHT
    if light > -0.55:
        return MID
    return DARK


def draw(cracked):
    random.seed(4)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            if inside(x, y):
                img.putpixel((x, y), shade(x, y))
    # altın damarları: eğri çizgiler, uçlarında parlama
    random.seed(9)
    veins = [((9, 30), (17, 24), (24, 27)), ((22, 14), (27, 19), (31, 17)), ((14, 37), (20, 35))]
    for vein in veins:
        for (x0, y0), (x1, y1) in zip(vein, vein[1:]):
            steps = max(abs(x1 - x0), abs(y1 - y0))
            for i in range(steps + 1):
                x = round(x0 + (x1 - x0) * i / steps)
                y = round(y0 + (y1 - y0) * i / steps)
                if inside(x, y):
                    img.putpixel((x, y), GOLD)
        hx, hy = vein[-1]
        if inside(hx, hy):
            img.putpixel((hx, hy), GOLD_HI)
    for x, y in ((12, 22), (28, 31), (19, 18)):
        if inside(x, y):
            img.putpixel((x, y), GOLD_HI)
    # çatlak: tepeden aşağı kırık bir çizgi ve iki dal
    if cracked:
        path = [(21, 6), (20, 11), (22, 15), (19, 21), (21, 26), (18, 31)]
        branches = [[(22, 15), (26, 18), (29, 17)], [(19, 21), (15, 23), (12, 22)], [(21, 26), (25, 30)]]
        for line in [path] + branches:
            for (x0, y0), (x1, y1) in zip(line, line[1:]):
                steps = max(abs(x1 - x0), abs(y1 - y0))
                for i in range(steps + 1):
                    x = round(x0 + (x1 - x0) * i / steps)
                    y = round(y0 + (y1 - y0) * i / steps)
                    if inside(x, y):
                        img.putpixel((x, y), CRACK)
                        if inside(x + 1, y):
                            img.putpixel((x + 1, y), DARK)
    # dış çizgi
    out = img.copy()
    for y in range(H):
        for x in range(W):
            if img.getpixel((x, y))[3]:
                continue
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < W and 0 <= ny < H and img.getpixel((nx, ny))[3]:
                    out.putpixel((x, y), OUTLINE)
                    break
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    draw(False).save(OUT / "kaya_damar_0.png")
    draw(True).save(OUT / "kaya_damar_1.png")
    print("kaya_damar_0/1.png yazıldı")


if __name__ == "__main__":
    main()
