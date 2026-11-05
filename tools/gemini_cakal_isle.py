"""
Çakal Eşkıya'nın üç Gemini sayfasını oyuna hazır karelere çevirir.

Bu sayfalarda ızgara yok ve kareler eşit aralıklı değil, bu yüzden kareler satır bantları içinde
boş sütunlara göre ayrılır. Kareler sağa bakar; sola bakış oyunda flipX ile elde edilir.
Şapka, ölüm sahnesinde havada kalsın diye ayrı bir kare olarak da kaydedilir.

Kullanım (proje kökünden):
  python tools/gemini_cakal_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_dusman_isle import body_center_x
from gemini_sprite_isle import apply_palette, background_mask, build_palette, downscale

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "cakal"
CANVAS_W, CANVAS_H = 128, 64  # ateş karesinde namlu alevi geniş, yılandan daha geniş tuval gerekir
TARGET_HEIGHT = 44     # ilk karedeki karakter yüksekliği (oyun pikseli); Gila 34
ROW_BANDS = [(135, 288), (288, 434), (434, 572)]  # başlığın altındaki 3 satır
MIN_GAP = 12           # kareleri ayıran en küçük boş sütun sayısı (alev kırıntıları kareden kopmasın)
GAP_OVERRIDE = {(3, 0): 3}  # bu satırda tüfek ucu sonraki kareye çok yakın

FLAME_COLORS = np.array([(255, 244, 190), (255, 216, 90), (250, 150, 40), (214, 84, 30)], dtype=np.int32)

# (sayfa, satır, sütun) -> animasyon. Sıra LOAD_ORDER ile aynı olmalı, koddaki indeksler buna bağlı.
ANIMATIONS = {
    "devriye": [(3, 0, i) for i in range(5)],
    "fark": [(1, 2, 2)],
    "kosu": [(2, 0, i) for i in range(7)],
    "nisan": [(1, 1, 1)],
    "ates": [(1, 1, 2)],
    "doldurma": [(2, 2, i) for i in range(5)],
    "hasar": [(2, 1, 1)],
    "sevinc": [(3, 2, i) for i in range(4)],
    "yenik": [(1, 2, 3)],
}


def split_row(image, band, min_gap=MIN_GAP):
    """Satır bandını boş sütunlara göre karelere böler."""
    y0, y1 = band
    row = image[y0:y1]
    filled = (~background_mask(row)).sum(axis=0) >= 2
    frames, start, gap = [], None, 0
    for x, on in enumerate(filled):
        if on:
            start = x if start is None else start
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                frames.append((start, x - gap + 1))
                start = None
    if start is not None:
        frames.append((start, len(filled)))
    # Kırıntıları (dar parçalar) at; çevresine pay bırak ki downscale kenarı kesmesin.
    return [row[:, max(0, a - 2):b + 2] for a, b in frames if b - a > 30]


def remove_fringe(sprite):
    """JPEG'de arka plandan sızan morumsu kenar piksellerini koyu kontur rengine çevirir."""
    out = sprite.copy()
    r, g, b = out[..., 0].astype(int), out[..., 1].astype(int), out[..., 2].astype(int)
    purple = (out[..., 3] > 0) & (b > g + 12) & (r > g + 12)
    out[purple, :3] = (42, 20, 18)
    return out


# Yenik karesinde şapka ayrı süzülür, gövdede kalmamalı. Şapkanın kapattığı baş elle çizilir (x: 66, y: 50'den).
BARE_HEAD = [
    "...###............",
    "..#ddt#...........",
    "..#dtt#..##.......",
    "..#ttd###dt#......",
    ".#dtttttttt###....",
    "#dtktktttttttt##..",
    "#dttkttttttwwwww#.",
    "#dtktkttttwwwwwnn#",
    "#ddttttttwwwwwwnn#",
    ".#dddtttwwwwwwww#.",
    "..##ddttwwwwrrw##.",
    "....####www#rr#...",
    "........###.##....",
]
BARE_HEAD_COLORS = {"#": (31, 15, 11), "d": (112, 84, 54), "t": (172, 136, 88), "w": (210, 186, 136),
                    "k": (31, 15, 11), "n": (31, 15, 11), "r": (176, 64, 56)}


def remove_hat(canvas):
    """Gövdenin sağındaki şapkayı (ve altında kalan parçaları) siler, yerine şapkasız baş çizer."""
    out = canvas.copy()
    out[:, 67:] = 0
    out[:49, 62:] = 0  # kenarın gövde üstüne taşan ucu
    for dy, row in enumerate(BARE_HEAD):
        for dx, ch in enumerate(row):
            if ch != ".":
                out[50 + dy, 66 + dx] = (*BARE_HEAD_COLORS[ch], 255)
    return out


def hat_from(sprite):
    """Şapkayı kareden ayırır: kanadın altına inmeden yalnızca üst kısım."""
    hat = sprite[:int(sprite.shape[0] * 0.31)].copy()
    ys, xs = np.where(hat[..., 3] > 0)
    return hat[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def place_on_canvas(sprite):
    """Alt orta pivot: ayaklar alt kenarda, gövde ortada."""
    canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    left = int(round(CANVAS_W / 2 - body_center_x(sprite)))
    assert left >= 0 and left + w <= CANVAS_W and h <= CANVAS_H, f"kare tuvale sığmadı: {w}x{h}"
    canvas[CANVAS_H - h:, left:left + w] = sprite
    return canvas


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {}
    for page in (1, 2, 3):
        image = np.array(Image.open(SOURCE_DIR / f"cakal_part{page}.jpg").convert("RGB"), dtype=np.int32)
        pages[page] = [split_row(image, band, GAP_OVERRIDE.get((page, r), MIN_GAP)) for r, band in enumerate(ROW_BANDS)]
        print(f"part {page}: satırlardaki kare sayısı {[len(r) for r in pages[page]]}")

    # Ölçek: birinci sayfanın ilk karesi (duran çakal) yüksekliğinden.
    first = pages[1][0][0]
    rows = np.where((~background_mask(first)).sum(axis=1) >= 3)[0]
    factor = (rows[-1] + 1 - rows[0]) / TARGET_HEIGHT
    print(f"1 oyun pikseli = {factor:.2f} kaynak piksel")

    order = [(name, loc) for name, locs in ANIMATIONS.items() for loc in locs]
    sprites = [remove_fringe(downscale(pages[p][r][c], factor)) for _, (p, r, c) in order]
    # Toprak renkleri baskın olduğu için alevin parlak renkleri palete elle eklenir.
    palette = np.vstack([build_palette(sprites), FLAME_COLORS])

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    canvases = []
    for i, ((name, _), sprite) in enumerate(zip(order, sprites)):
        canvas = place_on_canvas(apply_palette(sprite, palette))
        if i == 25:  # yenik karesi
            canvas = remove_hat(canvas)
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"cakal_{i}.png")
        canvases.append(canvas)
        print(f"  cakal_{i}: {name} ({sprite.shape[1]}x{sprite.shape[0]})")

    # Şapka alt ortadan pivotlu, ölüm sahnesinde gövdeden ayrılıp süzülür.
    hat = hat_from(apply_palette(remove_fringe(downscale(pages[1][0][0], factor)), palette))
    canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
    h, w = hat.shape[:2]
    canvas[CANVAS_H - h:, CANVAS_W // 2 - w // 2:CANVAS_W // 2 - w // 2 + w] = hat
    Image.fromarray(canvas, "RGBA").save(OUT_DIR / "cakal_sapka.png")
    canvases.append(canvas)

    if args.preview:
        scale, cols = 3, 7
        rows_n = (len(canvases) + cols - 1) // cols
        out = Image.new("RGBA", (cols * CANVAS_W * scale, rows_n * CANVAS_H * scale), (90, 90, 110, 255))
        for i, c in enumerate(canvases):
            tile = Image.fromarray(c, "RGBA").resize((CANVAS_W * scale, CANVAS_H * scale), Image.NEAREST)
            out.alpha_composite(tile, ((i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale))
        out.save(args.preview)


if __name__ == "__main__":
    main()
