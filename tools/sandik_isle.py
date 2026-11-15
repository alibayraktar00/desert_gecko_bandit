"""
Kertenkele sandığının açılma sayfasını (4x2, 8 kare) sprite'lara ayırır:
  sandik_0  kapalı              sandik_1  kilitte parıltı      sandik_2  kapak aralanır, ışık sızar
  sandik_3  kapak açık          sandik_4  altın ışığı patlar   sandik_5-7  açık, altınlar pırıldar (döngü)
Kareler aynı tuvale, sandığın ayak hizasından ve gövde ortasından hizalanır: oyunda kare değişince sandık
yerinde kalır, yalnız kapak, ışık ve kıvılcımlar oynar. Kıvılcımlar gövdeden kopuk parçalardır, korunur.
Çıktı Props klasörüne gider (alt orta pivot).
Ayrıca sandik_ipucu.png: sandığın üstünde beliren "J tuşu + dil" ipucu (oyuncu kilidin dille açıldığını bilsin).

Kaynak: Assets/Sprites/_Gelen/sandik_acilis.webp
Kullanım (proje kökünden):  python tools/sandik_isle.py [--preview onizleme.png]
Gereken paketler: pillow, numpy
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_serif_isle import label

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets" / "Sprites" / "_Gelen" / "sandik_acilis.webp"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
OLD_FRAMES = 6                       # önceki sayfanın kare sayısı: fazlası yoksa silinmez, üstüne yazılır

COLUMNS = [(0, 256), (256, 512), (512, 768), (768, 1024)]
ROWS = [(40, 312), (420, 750)]       # kıvılcımlar gövdenin epey üstüne çıkıyor
TOLERANCE = 13                       # zemine yakınlık: altın ışığının soluk kenarı zemine karışır, kesilir
# Kaynakta kapalı sandık 191 piksel geniş; oyunda 38 piksel: boyu Gila'nınkiyle (34) aynı sınıfta kalsın.
FACTOR = 191 / 38
# Ortak kaynak tuvali: gövdenin ayak ortası buraya oturur.
CANVAS_W, CANVAS_H, ANCHOR = 320, 330, (160, 320)


def frame_cells(sheet):
    background = np.median(sheet.reshape(-1, 3), axis=0)
    cells = []
    for y0, y1 in ROWS:
        for x0, x1 in COLUMNS:
            region = sheet[y0:y1, x0:x1]
            mask = np.abs(region - background).max(axis=2) >= TOLERANCE
            cells.append((region, mask))
    return cells


def anchor(mask):
    """Gövde: en büyük parça. Ayak satırı ve alt 15 satırın yatay ortası (kapak açılsa da değişmez)."""
    lab, boxes = label(mask)
    body = lab == 1 + max(range(len(boxes)), key=lambda i: boxes[i][4])
    ys, xs = np.nonzero(body)
    bottom = ys.max()
    return int(round(np.median(xs[ys > bottom - 15]))), int(bottom)


def place(region, mask):
    ax, ay = anchor(mask)
    rgb = np.zeros((CANVAS_H, CANVAS_W, 3))
    m = np.zeros((CANVAS_H, CANVAS_W), bool)
    dx, dy = ANCHOR[0] - ax, ANCHOR[1] - ay
    h, w = mask.shape
    sy0, sx0 = max(0, -dy), max(0, -dx)
    sy1, sx1 = min(h, CANVAS_H - dy), min(w, CANVAS_W - dx)
    rgb[sy0 + dy:sy1 + dy, sx0 + dx:sx1 + dx] = region[sy0:sy1, sx0:sx1]
    m[sy0 + dy:sy1 + dy, sx0 + dx:sx1 + dx] = mask[sy0:sy1, sx0:sx1]
    return rgb, m


def downscale(rgb, mask):
    """Ayak hizası alt kenarda: her çıktı pikseli kaynak bloğunun ortasına bakar, çoğunluk doluysa medyan rengi."""
    out_h, out_w = int(ANCHOR[1] // FACTOR) + 1, int(CANVAS_W // FACTOR)
    out = np.zeros((out_h, out_w, 3))
    m = np.zeros((out_h, out_w), bool)
    # Gövdenin ortası çıktıda tam bir sütunun ortasına düşsün: tuvali ortadan böl.
    left = ANCHOR[0] - out_w / 2 * FACTOR
    for oy in range(out_h):
        y1 = ANCHOR[1] + 1 - (out_h - 1 - oy) * FACTOR
        ya, yb = max(0, int(y1 - FACTOR * 0.8)), max(1, int(y1 - FACTOR * 0.2))
        for ox in range(out_w):
            xa = int(left + (ox + 0.2) * FACTOR)
            xb = max(xa + 1, int(left + (ox + 0.8) * FACTOR))
            block = mask[ya:yb, xa:xb]
            if block.size == 0 or block.mean() < 0.5:
                continue
            out[oy, ox] = np.median(rgb[ya:yb, xa:xb][block], axis=0)
            m[oy, ox] = True
    return out, m


HINT = [
    ".ooooooo.........",
    "oWWWWWWWo........",
    "oWWWdddWo........",
    "oWWWWWdWo..ppp...",
    "oWdWWWdWo.prrrp.p",
    "oWWdddWWo.prrrrrr",
    "oWWWWWWWo..ppp..p",
    "ommmmmmmo........",
    ".ooooooo.........",
]
HINT_PALETTE = {"o": (4, 0, 1, 255), "W": (240, 226, 196, 255), "d": (60, 40, 30, 255), "m": (170, 150, 120, 255),
                "p": (150, 40, 70, 255), "r": (236, 96, 128, 255)}


def hint_sprite():
    """J tuş başlığı ve yanında çatal uçlu dil (Gila'nın dilinin pembesi)."""
    img = Image.new("RGBA", (len(HINT[0]), len(HINT)), (0, 0, 0, 0))
    for y, row in enumerate(HINT):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), HINT_PALETTE[ch])
    img.save(OUTPUT_DIR / "sandik_ipucu.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    hint_sprite()
    sheet = np.array(Image.open(SOURCE).convert("RGB"), dtype=np.float64)
    frames = [downscale(*place(*cell)) for cell in frame_cells(sheet)]

    # Hiçbir karede dolu olmayan üst satırlar ve iki yandaki boş sütunlar kesilir (hepsinde aynı, yatayda ortalı).
    union = np.zeros_like(frames[0][1])
    for _, m in frames:
        union |= m
    rows, cols = np.where(union.any(1))[0], np.where(union.any(0))[0]
    side = min(cols[0], union.shape[1] - 1 - cols[-1])
    y0, x0, x1 = rows[0], side, union.shape[1] - side

    opaque = np.concatenate([rgb[m] for rgb, m in frames]).astype(np.uint8)
    strip = Image.fromarray(opaque.reshape(1, -1, 3), "RGB").quantize(colors=64, method=Image.Quantize.MEDIANCUT)
    palette = np.array(strip.getpalette()[:192], dtype=np.float64).reshape(-1, 3)

    images = []
    for i, (rgb, m) in enumerate(frames):
        rgb, m = rgb[y0:, x0:x1], m[y0:, x0:x1]
        flat = rgb.reshape(-1, 3)
        snapped = palette[((flat[:, None, :] - palette[None]) ** 2).sum(2).argmin(1)].reshape(rgb.shape)
        image = np.dstack([snapped.astype(np.uint8), (m * 255).astype(np.uint8)])
        path = OUTPUT_DIR / f"sandik_{i}.png"
        Image.fromarray(image, "RGBA").save(path)
        print(f"  {path.relative_to(ROOT)}  ({image.shape[1]}x{image.shape[0]})")
        images.append(image)

    if args.preview:
        scale = 3
        h, w = images[0].shape[:2]
        out = Image.new("RGBA", ((w * scale + 10) * len(images), h * scale), (60, 50, 50, 255))
        for i, image in enumerate(images):
            img = Image.fromarray(image, "RGBA").resize((w * scale, h * scale), Image.NEAREST)
            out.alpha_composite(img, (i * (w * scale + 10), 0))
        out.save(args.preview)


if __name__ == "__main__":
    main()
