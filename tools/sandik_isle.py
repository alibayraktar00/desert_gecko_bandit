"""
Kertenkele sandığı açılma animasyonunu (Part 10.2, 6 kare) sprite'lara ayırır:
  sandik_0  kapalı       sandik_1  sarsılıyor     sandik_2  aralanıyor
  sandik_3  açılıyor     sandik_4  patlama        sandik_5  açık, dinleniyor
Kareler aynı tuvale, sandığın ayak hizasından ve panel ortasından hizalanır: oyunda kare değişince
sandık yerinde kalır, yalnız kapak ve altın hareket eder. Çıktı Props klasörüne gider (alt orta pivot).

Kaynak: Assets/Sprites/_Gelen/sandik.png
Kullanım (proje kökünden):  python tools/sandik_isle.py [--preview onizleme.png]
Gereken paketler: pillow, numpy
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets" / "Sprites" / "_Gelen" / "sandik.png"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
PIXEL = 3

# Her karenin panelinin sol kenarı ve alt kenarı (kaynak pikseli). Panel genişliği hepsinde 284.
PANELS = [(42, 275), (370, 276), (699, 276), (42, 721), (370, 721), (698, 721)]
PANEL_WIDTH = 284
TOP = {275: 66, 276: 66, 721: 330}  # karenin kesilebileceği en üst satır (başlık ve yazılar dışarıda)
BACKGROUNDS = [(39, 39, 35), (33, 33, 29)]  # sayfa zemini ve kare paneli


def key(region):
    keep = np.ones(region.shape[:2], bool)
    lo, hi = region.min(2), region.max(2)
    for bg in BACKGROUNDS:
        keep &= ~((np.abs(region - np.array(bg)).sum(2) < 14) & (hi - lo < 10))
    return keep


def downscale(rgb, mask):
    h, w = rgb.shape[0] // PIXEL, rgb.shape[1] // PIXEL
    blocks = rgb[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL, 3).transpose(0, 2, 1, 3, 4).reshape(h, w, -1, 3)
    mb = mask[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL).transpose(0, 2, 1, 3).reshape(h, w, -1)
    out = np.zeros((h, w, 3))
    m = mb.mean(2) >= 0.5
    for y, x in zip(*np.nonzero(m)):
        out[y, x] = np.median(blocks[y, x][mb[y, x]], axis=0)
    return out, m


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    sheet = np.array(Image.open(SOURCE).convert("RGB"), dtype=np.float64)
    tallest = max(bottom - TOP[bottom] for _, bottom in PANELS)
    frames = []
    for x0, bottom in PANELS:
        region = sheet[TOP[bottom]:bottom, x0:x0 + PANEL_WIDTH]
        mask = key(region)
        # Ortak tuval: hepsi en uzun karenin boyuna, alttan hizalı.
        pad = tallest - region.shape[0]
        region = np.pad(region, ((pad, 0), (0, 0), (0, 0)))
        mask = np.pad(mask, ((pad, 0), (0, 0)))
        frames.append(downscale(region, mask))

    # Ressam sandığı her panelde birkaç piksel farklı yere çizmiş: ahşap gövdeden (altın değil) hizala.
    def body_anchor(rgb, m):
        r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
        wood = m & (r > g) & (g > b) & (r < 140) & (r - b < 70) & (r > 40)
        ys, xs = np.nonzero(wood)
        lower = ys > ys.max() - 18  # gövdenin alt kısmı: kapak açılınca değişmez
        return int(round(np.median(xs[lower]))), int(ys.max())

    ax, ay = body_anchor(*frames[0])
    aligned = []
    for rgb, m in frames:
        bx, by = body_anchor(rgb, m)
        dx, dy = ax - bx, ay - by
        rgb = np.roll(np.roll(rgb, dy, 0), dx, 1)
        m = np.roll(np.roll(m, dy, 0), dx, 1)
        aligned.append((rgb, m))
    frames = aligned

    # Ayak hizası: kapalı sandığın en alt dolu satırı; altı kesilir (pivot = sandığın ayağı).
    feet = int(np.where(frames[0][1].any(1))[0][-1])
    # Üstte hiçbir karede dolu olmayan satırlar ve iki yandaki boş sütunlar kesilir (hepsinde aynı).
    union = np.zeros_like(frames[0][1])
    for _, m in frames:
        union |= m
    rows, cols = np.where(union[:feet + 1].any(1))[0], np.where(union[:feet + 1].any(0))[0]
    # Yatayda tam ortalı kalsın: iki yandan eşit kes.
    left, right = cols[0], union.shape[1] - 1 - cols[-1]
    side = min(left, right)
    y0, y1, x0, x1 = rows[0], feet + 1, side, union.shape[1] - side

    opaque = np.concatenate([rgb[m] for rgb, m in frames]).astype(np.uint8)
    strip = Image.fromarray(opaque.reshape(1, -1, 3), "RGB").quantize(colors=64, method=Image.Quantize.MEDIANCUT)
    palette = np.array(strip.getpalette()[:192], dtype=np.float64).reshape(-1, 3)

    images = []
    for i, (rgb, m) in enumerate(frames):
        rgb, m = rgb[y0:y1, x0:x1], m[y0:y1, x0:x1]
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
        sheet_img = Image.new("RGBA", (w * scale * 6 + 50, h * scale), (60, 50, 50, 255))
        for i, image in enumerate(images):
            img = Image.fromarray(image, "RGBA").resize((w * scale, h * scale), Image.NEAREST)
            sheet_img.alpha_composite(img, (i * (w * scale + 10), 0))
        sheet_img.save(args.preview)


if __name__ == "__main__":
    main()
