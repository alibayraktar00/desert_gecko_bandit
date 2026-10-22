"""
Gemini'den gelen "obje sayfalarını" (macenta zemin üzerinde dağınık duran kaktüs, platform vb.)
tek tek sprite'lara ayırır. Objelerin yerini kendisi bulur; ızgara gerekmez.

Ne yapar:
  1. Görseldeki macenta alanı bulur, dışındaki çerçeveyi (ekran görüntüsü kenarı vb.) atar.
  2. Macentayı şeffaf yapar, kenardaki renk sızmasını temizler.
  3. Gemini'nin sahte piksellerini gerçek piksel ızgarasına indirir (Gila ile aynı piksel boyu).
  4. Birbirine yakın parçaları tek obje sayar (kaktüs dikeni, sarkan ip vb. kopmasın).
  5. Her objeyi ayrı PNG olarak, soldan sağa / yukarıdan aşağı sırayla kaydeder.

Kullanım (proje kökünden):
  python tools/gemini_obje_isle.py [--preview onizleme.png]

Yeni sayfa eklemek için SHEETS listesine bir satır ekle. Gereken paketler: pillow, numpy
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Props"

# pixel: kaynakta bir oyun pikselinin kaç piksel olduğu
# merge: bu kadar oyun pikseli yakındaki parçalar aynı obje sayılır
SHEETS = [
    {"file": "props_kaktusler.jpg", "prefix": "kaktus", "pixel": 4, "merge": 1, "palette": 40},
    # Sarkan ipler alttaki sıraya çok yakın; birleştirme payı 0 olmalı yoksa sıralar yapışıyor.
    {"file": "platformlar.jpg", "prefix": "platform", "pixel": 3, "merge": 0, "palette": 32},
]
MIN_PIXELS = 30  # bundan küçük parçalar kırıntı sayılır


def magenta_mask(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (r > 130) & (b > 130) & (g < 120) & (np.abs(r - b) < 100)


def crop_to_magenta(rgb, inset=4):
    """Görsel bir ekran görüntüsünden geldiyse macenta alanın dışını at."""
    mask = magenta_mask(rgb)
    rows = np.where(mask.mean(axis=1) > 0.3)[0]
    cols = np.where(mask.mean(axis=0) > 0.3)[0]
    return rgb[rows[0] + inset:rows[-1] - inset, cols[0] + inset:cols[-1] - inset]


def downscale(rgb, pixel):
    mask = magenta_mask(rgb)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    spill = (r > g + 40) & (b > g + 40)
    rgb = rgb.copy()
    rgb[spill, 2] = np.minimum(b[spill], g[spill])

    h, w = rgb.shape[0] // pixel, rgb.shape[1] // pixel
    out = np.zeros((h, w, 4), dtype=np.uint8)
    lo, hi = int(pixel * 0.25), max(int(pixel * 0.75), int(pixel * 0.25) + 1)
    for y in range(h):
        for x in range(w):
            block = rgb[y * pixel + lo:y * pixel + hi, x * pixel + lo:x * pixel + hi]
            fg = ~mask[y * pixel + lo:y * pixel + hi, x * pixel + lo:x * pixel + hi]
            if fg.mean() < 0.5:
                continue
            out[y, x, :3] = np.median(block[fg], axis=0)
            out[y, x, 3] = 255
    return out


def quantize(rgba, colors):
    opaque = rgba[..., 3] > 0
    pixels = rgba[opaque][:, :3]
    strip = Image.fromarray(pixels.reshape(1, -1, 3).astype(np.uint8), "RGB")
    palette_img = strip.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
    palette = np.array(palette_img.getpalette()[:colors * 3], dtype=np.int32).reshape(-1, 3)
    nearest = ((pixels.astype(np.int32)[:, None, :] - palette[None, :, :]) ** 2).sum(axis=2).argmin(axis=1)
    out = rgba.copy()
    out[opaque, :3] = palette[nearest]
    return out


def find_objects(alpha, merge):
    """Yakın parçaları birleştirerek bağlı bileşenleri bul; (x0, y0, x1, y1) kutuları döner."""
    h, w = alpha.shape
    # Genişletilmiş maske: merge pikseli kadar yakındaki parçalar birleşir.
    grown = alpha.copy()
    for dy in range(-merge, merge + 1):
        for dx in range(-merge, merge + 1):
            grown |= np.roll(np.roll(alpha, dy, axis=0), dx, axis=1)

    labels = np.zeros((h, w), dtype=np.int32)
    boxes = []
    for sy, sx in zip(*np.nonzero(grown)):
        if labels[sy, sx]:
            continue
        label = len(boxes) + 1
        labels[sy, sx] = label
        queue = deque([(sy, sx)])
        x0 = x1 = sx
        y0 = y1 = sy
        while queue:
            y, x = queue.popleft()
            x0, x1, y0, y1 = min(x0, x), max(x1, x), min(y0, y), max(y1, y)
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and grown[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = label
                    queue.append((ny, nx))
        # Kutuyu asıl (genişletilmemiş) piksellere daralt.
        region = alpha[y0:y1 + 1, x0:x1 + 1]
        if region.sum() < MIN_PIXELS:
            boxes.append(None)
            continue
        rows, cols = np.where(region.any(axis=1))[0], np.where(region.any(axis=0))[0]
        boxes.append((x0 + cols[0], y0 + rows[0], x0 + cols[-1] + 1, y0 + rows[-1] + 1))
    return [b for b in boxes if b]


def reading_order(boxes):
    """Satırlara ayır (alt kenara göre), her satırı soldan sağa sırala."""
    boxes = sorted(boxes, key=lambda b: b[3])
    rows, current = [], []
    for box in boxes:
        if current and box[3] - current[-1][3] > 6:
            rows.append(current)
            current = []
        current.append(box)
    if current:
        rows.append(current)
    return [b for row in rows for b in sorted(row, key=lambda b: b[0])]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path, help="numaralı objelerin önizlemesini kaydet")
    args = parser.parse_args()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    previews = []
    for sheet in SHEETS:
        rgb = crop_to_magenta(np.array(Image.open(SOURCE_DIR / sheet["file"]).convert("RGB"), dtype=np.int32))
        rgba = quantize(downscale(rgb, sheet["pixel"]), sheet["palette"])
        alpha = rgba[..., 3] > 0

        for old in OUTPUT_DIR.glob(f"{sheet['prefix']}_*.png*"):
            old.unlink()

        boxes = reading_order(find_objects(alpha, sheet["merge"]))
        for i, (x0, y0, x1, y1) in enumerate(boxes):
            sprite = rgba[y0:y1, x0:x1].copy()
            # Kutuya giren komşu obje kırıntılarını değil, sadece bu kutuyu al.
            path = OUTPUT_DIR / f"{sheet['prefix']}_{i:02d}.png"
            Image.fromarray(sprite, "RGBA").save(path)
            print(f"  {path.relative_to(ROOT)}  ({x1 - x0}x{y1 - y0})")
        previews.append((sheet["prefix"], rgba, boxes))

    if args.preview:
        scale = 2
        height = sum(r.shape[0] * scale + 20 for _, r, _ in previews)
        width = max(r.shape[1] * scale for _, r, _ in previews)
        sheet_img = Image.new("RGBA", (width, height), (70, 70, 90, 255))
        draw = ImageDraw.Draw(sheet_img)
        y = 0
        for prefix, rgba, boxes in previews:
            img = Image.fromarray(rgba, "RGBA").resize((rgba.shape[1] * scale, rgba.shape[0] * scale), Image.NEAREST)
            sheet_img.alpha_composite(img, (0, y))
            for i, (x0, y0, x1, y1) in enumerate(boxes):
                draw.rectangle([x0 * scale, y + y0 * scale, x1 * scale, y + y1 * scale], outline=(255, 255, 0, 255))
                draw.text((x0 * scale + 2, y + y0 * scale + 1), f"{prefix[0]}{i}", fill=(255, 255, 255, 255))
            y += rgba.shape[0] * scale + 20
        sheet_img.save(args.preview)
        print(f"önizleme: {args.preview}")


if __name__ == "__main__":
    main()
