"""
Şerif Yardımcısı'nın (Deputy) sekiz Gemini sayfasını oyuna hazır karelere çevirir.

Sayfaların zemini mor değil düz gri, bu yüzden arka plan sayfa kenarından başlayan gri bölge olarak
bulunur (kontur içindeki gri pantolon ve tabanca silinmez). Kareler satır bantlarında bağlı parçalar
olarak ayrılır; yazılar ve Gila referansı elenir. Kareler sağa bakar; sola bakış oyunda flipX ile.
Şapka, yenilgi sahnesinde ayrı süzülsün diye ayrıca kaydedilir; ölüm karelerinde (6. sayfa) bu yüzden
yalnız gövde kalır, havadaki şapka ve tabanca elenir.

Kullanım (proje kökünden):
  python tools/gemini_serif_isle.py [--preview onizleme.png]
"""
import argparse
import math
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_dusman_isle import body_center_x
from gemini_sprite_isle import apply_palette, build_palette

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "serif"
CANVAS_W, CANVAS_H = 128, 64  # ateş karesinde namlu alevi geniş
TARGET_HEIGHT = 47            # bekleme karesinde şapkadan çizmeye (oyun pikseli); Gila 34, çakal 44
BACKGROUND = 72               # sayfa zemininin gri tonu
ROW_BANDS = [(60, 263), (300, 499), (530, 722)]
# 4. ve 7. sayfada alt başlık ilk banda taşıyor
PAGE_BANDS = {4: [(85, 263), (300, 499), (530, 722)], 7: [(78, 263), (300, 499), (530, 722)],
              8: [(78, 263), (300, 499), (530, 722)]}
PAGE_BACKGROUND = {7: 64, 8: 61}  # doldurma ve hasar sayfalarının zemini biraz daha koyu
# Hasar sayfasında zemin koyu lacivert gömleğe ve gri pantolona çok yakın: dar eşik, yoksa gövde oyulur.
PAGE_TOLERANCE = {8: 9}
VICTORY_BAND = (115, 336)     # 5. sayfa tek satır, kareler küçük çizilmiş
VICTORY_SCALE = 155 / 193     # 5. sayfadaki şerifin diğer sayfalara oranı
DEATH_BANDS = [(60, 352), (480, 700)]  # 6. sayfa: iki satır ölüm karesi
DEATH_BACKGROUND = 59         # bu sayfanın zemini daha koyu
DEATH_SCALE = 219 / 47        # 6. sayfada şerif daha ince çizilmiş: şapkasız boyu oyundaki şapkalı boya eşitlenir, gövde küçülmüş görünmesin
DEATH_COUNT = 7               # 8. kare (duman tüter) 7. ile aynı duruş, kullanılmıyor
HAT_ROWS = 11                 # bekleme karesinde şapkanın kapladığı üst satırlar

FLAME_COLORS = np.array([(255, 244, 190), (255, 216, 90), (250, 150, 40), (214, 84, 30)], dtype=np.int32)

# (sayfa, satır, sıra) -> kare. Sıra koddaki indekslerle aynı (CombatSetupBuilder.BuildDeputy).
# keep_all: namlu alevi ve duman gövdeden kopuk parçalardır, atılmasın.
FRAMES = [
    ("bekleme", (1, 0, 3), False),
    ("yuruyus", (1, 0, 4), False),
    ("yuruyus", (1, 1, 0), False),
    ("yuruyus", (1, 1, 1), False),
    ("yuruyus", (1, 1, 2), False),
    ("yuruyus", (1, 1, 3), False),
    ("sapka", (2, 0, 3), False),      # 6: şapkasını iter
    ("cekis", (2, 2, 0), False),      # 7: silahı çeker
    ("nisan", (3, 0, 3), False),      # 8
    ("ates", (3, 1, 0), True),        # 9
    ("tepme", (3, 1, 1), True),       # 10
    ("duman", (3, 1, 2), True),       # 11
    # 12-20 doldurma (7. sayfa), sırayla bir kez oynar: hazırlık, kovanları boşaltma, namlu yukarı,
    # mermi çıkarma, el kemerde, yükleme 1-2, silindiri kapatma, hazır konum.
    ("doldurma", (7, 0, 2), False),
    ("doldurma", (7, 0, 3), True),
    ("doldurma", (7, 1, 0), False),
    ("doldurma", (7, 1, 1), False),
    ("doldurma", (7, 1, 2), False),
    ("doldurma", (7, 2, 0), False),
    ("doldurma", (7, 2, 1), True),
    ("doldurma", (7, 2, 2), True),
    ("doldurma", (7, 2, 3), False),
    ("hazir", (3, 2, 2), False),      # 21
    # 22-26 hasar (8. sayfa), sırayla bir kez oynar: geriye sarsılma, denge kaybı, büzülme, duraksama, doğrulma.
    ("hasar", (8, 0, 3), False),
    ("hasar", (8, 1, 0), False),
    ("hasar", (8, 1, 1), False),
    ("hasar", (8, 1, 2), False),
    ("hasar", (8, 2, 0), False),
]
VICTORY_COLUMNS = [4, 5]  # 27-28: kahkaha kareleri (boş sütunlarla ayrılan parçaların son ikisi)


def label(mask):
    """4 komşuluklu bağlı parçalar: etiket haritası ve (x0, y0, x1, y1, alan) kutuları."""
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    boxes = []
    for y in range(h):
        for x in np.where(mask[y])[0]:
            if lab[y, x]:
                continue
            n = len(boxes) + 1
            queue = deque([(y, x)])
            lab[y, x] = n
            x0 = x1 = x
            y0 = y1 = y
            area = 0
            while queue:
                cy, cx = queue.popleft()
                area += 1
                x0, x1, y0, y1 = min(x0, cx), max(x1, cx), min(y0, cy), max(y1, cy)
                for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not lab[ny, nx]:
                        lab[ny, nx] = n
                        queue.append((ny, nx))
            boxes.append((x0, y0, x1 + 1, y1 + 1, area))
    return lab, boxes


def dilate(mask, times):
    for _ in range(times):
        grown = mask.copy()
        grown[1:] |= mask[:-1]
        grown[:-1] |= mask[1:]
        grown[:, 1:] |= mask[:, :-1]
        grown[:, :-1] |= mask[:, 1:]
        mask = grown
    return mask


def foreground(image, background=BACKGROUND, tolerance=22):
    """Zemin: griye yakın piksellerin en büyük bağlı bölgesi. Geri kalan her şey ön plan."""
    near = np.abs(image - background).max(axis=2) < tolerance
    lab, boxes = label(near)
    biggest = 1 + max(range(len(boxes)), key=lambda i: boxes[i][4])
    return lab != biggest


def band_frames(image, fg, band, keep_all):
    """Banttaki kareleri soldan sağa döndürür: (rgb, maske) çiftleri."""
    y0, y1 = band
    lab, boxes = label(dilate(fg[y0:y1], 2))
    order = sorted((i for i, b in enumerate(boxes) if b[3] - b[1] > 70 and b[2] - b[0] > 40), key=lambda i: boxes[i][0])
    frames = []
    for i in order:
        bx0, by0, bx1, by1, _ = boxes[i]
        mask = fg[y0 + by0:y0 + by1, bx0:bx1] & (lab[by0:by1, bx0:bx1] == i + 1)
        if not keep_all(len(frames)):
            # Kareye yapışan yazı ve ünlem gibi parçalar: yalnız gövde (en büyük parça) kalır.
            inner, parts = label(mask)
            mask = inner == 1 + max(range(len(parts)), key=lambda k: parts[k][4])
        frames.append((image[y0 + by0:y0 + by1, bx0:bx1], mask))
    return frames


def victory_frames(image, fg):
    """5. sayfa: kareler birbirine yakın, boş sütunlardan bölünür; kahkaha yazıları elenir."""
    y0, y1 = VICTORY_BAND
    filled = fg[y0:y1].sum(axis=0) >= 2
    spans, start = [], None
    for x, on in enumerate(filled):
        if on and start is None:
            start = x
        elif not on and start is not None:
            spans.append((start, x))
            start = None
    frames = []
    for a, b in spans:
        if b - a < 60:
            continue
        mask = fg[y0:y1, a:b]
        inner, parts = label(mask)
        mask = inner == 1 + max(range(len(parts)), key=lambda k: parts[k][4])
        frames.append((image[y0:y1, a:b], mask))
    return frames


def downscale(cell, fg, factor):
    """Her çıktı pikseli kaynak bloğun ortasına bakar: çoğunluk ön plansa medyan rengini alır."""
    ys, xs = np.where(fg)
    cell, fg = cell[ys.min():ys.max() + 1, xs.min():xs.max() + 1], fg[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    out_w, out_h = math.ceil(cell.shape[1] / factor), math.ceil(cell.shape[0] / factor)
    rgba = np.zeros((out_h, out_w, 4), dtype=np.uint8)
    for oy in range(out_h):
        # Alt kenar hizalı: çizmeler her karede aynı satıra otursun.
        sy1 = cell.shape[0] - (out_h - 1 - oy + 0.2) * factor
        sy0 = cell.shape[0] - (out_h - 1 - oy + 0.8) * factor
        ya, yb = max(0, int(sy0)), max(int(sy1), max(0, int(sy0)) + 1)
        for ox in range(out_w):
            xa = int((ox + 0.2) * factor)
            xb = max(int((ox + 0.8) * factor), xa + 1)
            block_fg = fg[ya:yb, xa:xb]
            if block_fg.size == 0 or block_fg.mean() < 0.5:
                continue
            rgba[oy, ox, :3] = np.median(cell[ya:yb, xa:xb][block_fg], axis=0)
            rgba[oy, ox, 3] = 255
    return rgba


def place_on_canvas(sprite, center=None):
    """Alt orta pivot: çizmeler alt kenarda, gövde ortada."""
    canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    left = int(round(CANVAS_W / 2 - (body_center_x(sprite) if center is None else center)))
    assert left >= 0 and left + w <= CANVAS_W and h <= CANVAS_H, f"kare tuvale sığmadı: {w}x{h}"
    canvas[CANVAS_H - h:, left:left + w] = sprite
    return canvas


def death_frames(image, fg):
    """6. sayfa: her karede yalnız gövde (büyük parça) alınır; uçan şapka ve tabanca ayrı parçalardır."""
    frames = []
    for y0, y1 in DEATH_BANDS:
        lab, boxes = label(fg[y0:y1])
        for i in sorted((i for i, b in enumerate(boxes) if b[4] > 10000), key=lambda i: boxes[i][0]):
            bx0, by0, bx1, by1, _ = boxes[i]
            mask = lab[by0:by1, bx0:bx1] == i + 1
            if len(frames) == 3:
                # Yere çarpma karesinde yerdeki şapka ve tabanca toz bulutuyla gövdeye yapışık: kesilir.
                h, w = mask.shape
                mask[int(h * 0.64):, :int(w * 0.25)] = False
                mask[int(h * 0.72):, int(w * 0.78):] = False
            frames.append((image[y0 + by0:y0 + by1, bx0:bx1], mask))
    return frames[:DEATH_COUNT]


def hat_from(idle):
    hat = idle[:HAT_ROWS].copy()
    ys, xs = np.where(hat[..., 3] > 0)
    return hat[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {}
    for page in (1, 2, 3, 4, 7, 8):
        image = np.array(Image.open(SOURCE_DIR / f"serif_part{page}.webp").convert("RGB"), dtype=np.int32)
        fg = foreground(image, PAGE_BACKGROUND.get(page, BACKGROUND), PAGE_TOLERANCE.get(page, 22))
        wanted = {(r, c): keep for _, (p, r, c), keep in FRAMES if p == page}
        pages[page] = [band_frames(image, fg, band, lambda c, r=r: wanted.get((r, c), False))
                       for r, band in enumerate(PAGE_BANDS.get(page, ROW_BANDS))]
        print(f"part {page}: satırlardaki kare sayısı {[len(r) for r in pages[page]]}")
    image = np.array(Image.open(SOURCE_DIR / "serif_part5.webp").convert("RGB"), dtype=np.int32)
    victory = victory_frames(image, foreground(image))
    print(f"part 5: {len(victory)} kare")

    image = np.array(Image.open(SOURCE_DIR / "serif_part6.webp").convert("RGB"), dtype=np.int32)
    death = death_frames(image, foreground(image, DEATH_BACKGROUND))
    print(f"part 6: {len(death)} kare")

    # Ölçek: bekleme karesinin boyundan.
    idle_mask = pages[1][0][3][1]
    rows = np.where(idle_mask.sum(axis=1) >= 3)[0]
    factor = (rows[-1] + 1 - rows[0]) / TARGET_HEIGHT
    print(f"1 oyun pikseli = {factor:.2f} kaynak piksel")

    sprites = [downscale(*pages[p][r][c], factor) for _, (p, r, c), _ in FRAMES]
    sprites += [downscale(*victory[c], factor * VICTORY_SCALE) for c in VICTORY_COLUMNS]
    sprites += [downscale(*d, DEATH_SCALE) for d in death]  # 29-35
    palette = np.vstack([build_palette(sprites), FLAME_COLORS])
    sprites = [apply_palette(s, palette) for s in sprites]
    names = [name for name, _, _ in FRAMES] + ["sevinc"] * len(VICTORY_COLUMNS) + ["olum"] * len(death)

    idle = sprites[0]

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    canvases = []
    for i, (name, sprite) in enumerate(zip(names, sprites)):
        canvas = place_on_canvas(sprite, sprite.shape[1] / 2 if name == "olum" else None)
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"serif_{i}.png")
        canvases.append(canvas)
        print(f"  serif_{i}: {name} ({sprite.shape[1]}x{sprite.shape[0]})")

    hat = hat_from(idle)
    canvas = place_on_canvas(hat, hat.shape[1] / 2)
    Image.fromarray(canvas, "RGBA").save(OUT_DIR / "serif_sapka.png")
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
