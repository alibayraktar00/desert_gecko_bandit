"""
Diken Mutantı'nın Gemini sayfalarını oyuna hazır karelere çevirir (gemini_yarasa_isle.py ile aynı yöntem).
Sayfalarda kareler ızgarasız, 8x3 düzende (her sütun 128 piksel). Zemin macenta, şeffaf yapılır.
Kareler sağa bakar; sola bakış oyunda flipX ile elde edilir.

Üç diken fazı: dolu (%100), yarı (%50), kel (%0). Sırtın dolgunluğu cephaneyi gösterir.

Hizalama: bir satırdaki tüm kareler aynı kaydırmayı alır (çizildiği gibi oynar, titremez).
Satırın en alt noktası tuvalin altına (pivot = yer), ilk karenin çizme ortası tuvalin ortasına oturur:
dikenler sırtta ne kadar kabarık olursa olsun gövde fazlar arasında yerinde kalır.
Yaylım ve yenilenme satırlarında gövdeden kopmuş parçalar (uçan diken, "Fııışş" yazısı) silinir;
uçan dikenler oyunda ayrı mermi olarak doğar (diken_mermi.png, burada çizilir).

Kaynaklar Assets/Sprites/_Gelen/diken_*.png
Kullanım (proje kökünden):
  python tools/gemini_diken_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_sprite_isle import apply_palette, build_palette
from gemini_yarasa_isle import bbox, components, crop, row_boxes, transparent_mask

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "diken"
CANVAS_W, CANVAS_H = 80, 64
FACTOR = 2.6  # dolu dikenle ~46 piksel (Gila 34, armadillo 46)
COL_W = 128

# (ad, sayfa, satır, sütunlar, yalnız gövde). Sıra CombatSetupBuilder.BuildSpikeMutant ile aynı olmalı.
ANIMATIONS = [
    ("bekleme", "dolu1", 0, range(8), False),       # 0-7    dolu, kabadayı duruş
    ("devriye", "dolu1", 1, range(8), False),       # 8-15   dolu, ağır adımlar
    ("comelme", "dolu1", 2, range(8), False),       # 16-23  dolu, çömelir, dikenler dikilir
    ("titreme", "dolu3", 0, range(8), False),       # 24-31  dolu, diken yığını titrer
    ("yaylim", "dolu2", 0, range(8), True),         # 32-39  dolu -> yarı -> kel ateş
    ("nefes", "dolu2", 1, range(8), False),         # 40-47  kel, dil dışarıda, ter
    ("yenilenme", "dolu2", 2, range(8), True),      # 48-55  kel -> Fııışş -> dolu
    ("kel_bekleme", "dolu3", 1, range(8), False),   # 56-63  kel, ayakta
    ("kel_yenik", "dolu3", 2, range(8), False),     # 64-71  kel, yüz üstü kapaklanır, mahmuzlar havada
    ("yari_bekleme", "yari1", 0, range(8), False),  # 72-79  yarı, ayakta
    ("yari_yuruyus", "yari1", 1, range(8), False),  # 80-87  yarı, yürür
    ("yari_sendeleme", "yari2", 1, range(8), True),  # 88-95  yarı, öne sendeler
    ("dolu_yenik", "dolu_olum", 0, range(8), False),  # 96-103  dolu, kapaklanır
    ("yari_yenik", "yari_olum", 0, range(8), False),  # 104-111 yarı, kapaklanır
    ("kel_sendeleme", "kel_olum", 0, range(8), False),  # 112-119 kel, öne sendeler
]


def downscale(cell):
    mask = transparent_mask(cell)
    h, w = int(cell.shape[0] / FACTOR), int(cell.shape[1] / FACTOR)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            y0, y1 = int(y * FACTOR), int((y + 1) * FACTOR)
            x0, x1 = int(x * FACTOR), int((x + 1) * FACTOR)
            fg = ~mask[y0:y1, x0:x1]
            if fg.mean() >= 0.5:
                out[y, x, :3] = np.median(cell[y0:y1, x0:x1][fg], axis=0)
                out[y, x, 3] = 255
    return out


def body_only(sprite):
    """En büyük bileşen gövde; kopmuş parçalar (uçan diken, yazı, kıvılcım) silinir."""
    parts = components(sprite[..., 3] > 0)
    out = np.zeros_like(sprite)
    for y, x in parts[0]:
        out[y, x] = sprite[y, x]
    return out


def foot_center_x(sprite):
    """Alttaki 6 satırın (çizmeler) yatay ortası."""
    alpha = sprite[..., 3] > 0
    rows = np.where(alpha.any(axis=1))[0]
    feet = alpha[max(0, rows[-1] - 5):rows[-1] + 1]
    return np.where(feet.any(axis=0))[0].mean()


def paste(canvas, sprite, top, left):
    h, w = sprite.shape[:2]
    for y in range(h):
        for x in range(w):
            if sprite[y, x, 3] and 0 <= top + y < CANVAS_H and 0 <= left + x < CANVAS_W:
                canvas[top + y, left + x] = sprite[y, x]


def spike_projectile():
    """Uçan diken: siyah gövde, gümüş uç. Sağa bakar, 9x3."""
    dark, mid, tip = (24, 22, 28, 255), (58, 54, 62, 255), (205, 210, 218, 255)
    img = np.zeros((3, 9, 4), dtype=np.uint8)
    for x in range(0, 6):
        img[1, x] = dark
    for x in range(2, 6):
        img[0, x] = mid
        img[2, x] = dark
    img[1, 6] = tip
    img[1, 7] = tip
    img[0, 6] = mid
    img[2, 6] = mid
    img[1, 8] = (255, 255, 255, 255)
    return img


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    names = sorted({a[1] for a in ANIMATIONS})
    pages = {n: np.array(Image.open(SOURCE_DIR / f"diken_{n}.png").convert("RGB")) for n in names}
    boxes_of = {n: row_boxes(img) for n, img in pages.items()}

    rows = []
    for name, page, row, cols, only_body in ANIMATIONS:
        y0, y1 = boxes_of[page][row]
        frames = []
        for c in cols:
            frame = downscale(pages[page][y0:y1, c * COL_W:(c + 1) * COL_W])
            if only_body:
                frame = body_only(frame)
            frames.append(frame)
        rows.append((name, frames))

    palette = build_palette([crop(f) for _, fs in rows for f in fs])

    canvases, labels = [], []
    for name, frames in rows:
        dy = CANVAS_H - 1 - max(bbox(f)[1] for f in frames)
        dx = int(round(CANVAS_W / 2 - foot_center_x(frames[0])))
        for i, f in enumerate(frames):
            canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
            paste(canvas, apply_palette(f, palette), dy, dx)
            if (canvas[..., 3] > 0).sum() < (f[..., 3] > 0).sum():
                print(f"  uyarı: {name} {i} tuvale tam sığmadı")
            canvases.append(canvas)
            labels.append(f"{name} {i}")

    print("kare:", len(canvases), "dolu boy:", bbox(rows[0][1][0])[1] - bbox(rows[0][1][0])[0] + 1)

    if not args.preview:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        for i, canvas in enumerate(canvases):
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"diken_{i}.png")
        Image.fromarray(spike_projectile(), "RGBA").save(OUT_DIR / "diken_mermi.png")
        return

    scale, cols = 3, 8
    n = len(canvases)
    out = Image.new("RGBA", (cols * CANVAS_W * scale, ((n + cols - 1) // cols) * CANVAS_H * scale), (70, 110, 130, 255))
    draw = ImageDraw.Draw(out)
    for i, c in enumerate(canvases):
        x, y = (i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale
        out.alpha_composite(Image.fromarray(c, "RGBA").resize((CANVAS_W * scale, CANVAS_H * scale), Image.NEAREST), (x, y))
        draw.line([(x + CANVAS_W * scale // 2, y + CANVAS_H * scale - 6), (x + CANVAS_W * scale // 2, y + CANVAS_H * scale)], fill=(255, 0, 0, 255))
        draw.text((x + 2, y + 2), f"{i} {labels[i]}", fill=(255, 255, 0, 255))
    out.save(args.preview)


if __name__ == "__main__":
    main()
