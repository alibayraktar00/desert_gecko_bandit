"""
Maden/mağara görsel paketini (düz zeminli, etiketli sayfalar) tek tek sprite'lara ayırır.

Ne yapar:
  1. Sayfanın düz zemin rengini şeffaf yapar (objelerin içindeki boşluklar dahil).
  2. Sahte pikselleri gerçek piksel ızgarasına indirir (platformlarla aynı piksel boyu).
  3. Beyaz sayfa etiketlerini atar, yakın parçaları tek obje sayar.
  4. OBJECTS tablosundaki adlarla Props klasörüne kaydeder; ada verilmeyen obje atlanır.

Kullanım (proje kökünden):
  python tools/magara_paketi_isle.py --preview onizleme.png   (numaralı önizleme, dosya yazmaz)
  python tools/magara_paketi_isle.py                          (sprite'ları yazar)

Gereken paketler: pillow, numpy
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Props"

PIXEL = 3         # kaynakta bir oyun pikselinin kaç piksel olduğu
MERGE = 2         # bu kadar oyun pikseli yakındaki parçalar aynı obje sayılır
MIN_PIXELS = 40   # bundan küçük parçalar kırıntı sayılır

# Önizlemedeki numara -> dosya adı. "tavan_" ile başlayanlar üst kenarından asılır (SpriteImportRules).
OBJECTS = {
    0: "mgr_kristal_siyah", 1: "tavan_kristal", 2: "mgr_kristal_kucuk", 3: "mgr_diken", 4: "mgr_mantar_kutuk",
    5: "tavan_damla", 6: "mgr_su", 7: "mgr_yikik_iskele", 8: "mgr_cark", 9: "tahta_kafes", 10: "mgr_matkap",
    11: "mgr_sandiklar", 12: "mgr_cadir", 13: "mgr_iskelet", 14: "mgr_boru", 15: "tavan_ip_merdiven", 16: "mgr_erzak",
    17: "tavan_sarkit_1", 18: "tavan_sarkit_2", 20: "tavan_sarkit_3", 19: "mgr_dikit_1", 21: "mgr_dikit_2",
    22: "mgr_cevher_mavi", 23: "mgr_cevher_turuncu", 24: "tavan_kok", 25: "tavan_sarmasik", 26: "mgr_mantar",
    27: "mgr_vagon", 28: "mgr_kiris", 29: "mgr_tnt", 30: "mgr_varil", 31: "mgr_fener", 32: "tavan_zincir",
    33: "mgr_kemik", 34: "mgr_kamp", 35: "tavan_ag",
    37: "ada_1", 38: "ada_2", 40: "ada_3", 41: "ada_4", 42: "tahta_zincir", 39: "tahta_tekerlek", 43: "tahta_ray",
    44: "iskele_kirmizi", 45: "tahta_korkuluk", 46: "iskele_cift", 47: "kaya_cikinti", 48: "kaya_destek",
}


def background_mask(rgb, tolerance):
    corners = np.concatenate([rgb[300:330, :4].reshape(-1, 3), rgb[300:330, -4:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    return np.abs(rgb.astype(np.int32) - bg).max(axis=2) < tolerance


def label_mask(rgb):
    """Sayfa etiketleri: objelerin üstündeki satırlarda beyaz yazı."""
    lum = rgb.min(axis=2)
    whitish = (lum > 130) & (rgb.max(axis=2).astype(np.int32) - lum < 45)
    bands = np.zeros(rgb.shape[:2], dtype=bool)
    for y0, y1 in ((0, 19), (110, 130), (208, 256), (458, 522)):
        bands[y0:y1] = True
    # Yazının JPEG hâlesi de gitsin
    hit = whitish & bands
    grown = hit.copy()
    for dy in (-2, -1, 0, 1, 2):
        for dx in (-2, -1, 0, 1, 2):
            grown |= np.roll(np.roll(hit, dy, axis=0), dx, axis=1)
    return grown


def downscale(rgb, mask):
    h, w = rgb.shape[0] // PIXEL, rgb.shape[1] // PIXEL
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            fg = ~mask[y * PIXEL:y * PIXEL + PIXEL, x * PIXEL:x * PIXEL + PIXEL]
            if fg.mean() < 0.5:
                continue
            block = rgb[y * PIXEL:y * PIXEL + PIXEL, x * PIXEL:x * PIXEL + PIXEL]
            out[y, x, :3] = np.median(block[fg], axis=0)
            out[y, x, 3] = 255
    return out


def remove_lamp_halo(crop):
    """Fenerin çevresindeki düz renkli ışık halkası: oyunda yerine gerçek parıltı çizilir."""
    out = crop.copy()
    upper = out[:out.shape[0] * 6 // 10]
    distance = np.abs(upper[..., :3].astype(np.int32) - np.array([140, 112, 78])).max(axis=2)
    upper[(distance < 34) & (upper[..., 3] > 0)] = 0
    return out


def components(alpha):
    h, w = alpha.shape
    grown = alpha.copy()
    for dy in range(-MERGE, MERGE + 1):
        for dx in range(-MERGE, MERGE + 1):
            grown |= np.roll(np.roll(alpha, dy, axis=0), dx, axis=1)
    labels = np.zeros((h, w), dtype=np.int32)
    found = []
    for sy, sx in zip(*np.nonzero(grown)):
        if labels[sy, sx]:
            continue
        label = len(found) + 1
        labels[sy, sx] = label
        queue = deque([(sy, sx)])
        cells = []
        while queue:
            y, x = queue.popleft()
            cells.append((y, x))
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and grown[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = label
                    queue.append((ny, nx))
        found.append(cells)
    return labels, found


def objects(rgba):
    alpha = rgba[..., 3] > 0
    labels, found = components(alpha)
    result = []
    for i, cells in enumerate(found):
        ys, xs = zip(*cells)
        y0, y1, x0, x1 = min(ys), max(ys) + 1, min(xs), max(xs) + 1
        region = (labels[y0:y1, x0:x1] == i + 1) & alpha[y0:y1, x0:x1]
        if region.sum() < MIN_PIXELS:
            continue
        colors = rgba[y0:y1, x0:x1, :3][region].astype(np.int32)
        whitish = (colors.min(axis=1) > 150) & (colors.max(axis=1) - colors.min(axis=1) < 40)
        if y1 - y0 <= 6 and whitish.mean() > 0.3:
            continue  # sayfa etiketi
        rows, cols = np.where(region.any(axis=1))[0], np.where(region.any(axis=0))[0]
        crop = rgba[y0:y1, x0:x1].copy()
        crop[~region] = 0
        crop = crop[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]
        result.append(((x0 + cols[0], y0 + rows[0]), crop))
    # Sıralama: yukarıdan aşağı satırlar, soldan sağa
    result.sort(key=lambda o: (o[0][1] // 80, o[0][0]))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    sheets = [("magara_paket_1.jpg", 26), ("magara_paket_2.jpg", 26), ("magara_paket_3.jpg", 26)]
    canvases = []
    number = 0
    for name, tolerance in sheets:
        rgb = np.array(Image.open(SOURCE_DIR / name).convert("RGB"))
        rgba = downscale(rgb, background_mask(rgb, tolerance) | label_mask(rgb))
        canvas = Image.new("RGBA", (rgba.shape[1] * 3, rgba.shape[0] * 3), (40, 50, 60, 255))
        draw = ImageDraw.Draw(canvas)
        for (x, y), crop in objects(rgba):
            big = Image.fromarray(crop, "RGBA").resize((crop.shape[1] * 3, crop.shape[0] * 3), Image.NEAREST)
            canvas.alpha_composite(big, (x * 3, y * 3))
            draw.text((x * 3, y * 3), str(number), fill=(255, 255, 0, 255))
            target = OBJECTS.get(number)
            if target and not args.preview:
                if target == "mgr_fener":
                    crop = remove_lamp_halo(crop)
                Image.fromarray(crop, "RGBA").save(OUTPUT_DIR / f"{target}.png")
                # Yürünen yüzey: en geniş satırın %60'ına ulaşan ilk satır (platform tablosu için)
                widths = (crop[..., 3] > 0).sum(axis=1)
                surface = int(np.argmax(widths >= widths.max() * 0.6))
                print(f"{target}: {crop.shape[1]}x{crop.shape[0]} yuzey={surface}")
            number += 1
        canvases.append(canvas)
    if args.preview:
        for i, canvas in enumerate(canvases):
            canvas.save(args.preview.with_name(f"{args.preview.stem}_{i}.png"))
    print("obje sayısı:", number)


if __name__ == "__main__":
    main()
