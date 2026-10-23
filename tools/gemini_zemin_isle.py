"""
Gemini'den gelen zemin (yer altı toprak katmanları) şeridini oyunda yan yana tekrar eden
zemin dokusuna çevirir.

Ne yapar:
  1. Görseldeki beyaz alanın altındaki toprak şeridini keser.
  2. Gemini'nin dokunun üstüne çizdiği koyu dikey ızgara çizgilerini bulup komşu piksellerle siler.
  3. Sahte pikselleri gerçek piksel ızgarasına indirir (Gila ile aynı piksel boyu).
  4. Aynalanmış kopyasıyla yan yana koyar: dikişsiz tekrar eder.
  5. Altını en sık renkle doldurur; doku zemin blokları kadar (4 birim) derin olur.

Kullanım (proje kökünden):
  python tools/gemini_zemin_isle.py [--preview onizleme.png]

Gereken paketler: pillow, numpy
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets" / "Sprites" / "_Gelen" / "zemin_toprak.png"
OUTPUT = ROOT / "Assets" / "Sprites" / "Environment" / "Ground" / "zemin_toprak.png"

PIXEL = 4            # kaynakta bir oyun pikseli
DEPTH = 128          # çıktı yüksekliği (oyun pikseli) = 4 birim, zemin bloklarının derinliği
GRID_THRESHOLD = 10  # sütun, çevresinin medyanından bu kadar koyuysa ızgara çizgisi sayılır
PALETTE = 32


def find_strip_top(rgb):
    white = rgb.min(axis=2) > 230
    return int(np.where(white.mean(axis=1) < 0.5)[0][0])


def grid_columns(lum, window=15):
    """Yerel medyana göre belirgin koyu sütun grupları (ızgara çizgileri)."""
    profile = lum[40:-40].mean(axis=0)
    padded = np.pad(profile, window, mode="edge")
    median = np.array([np.median(padded[i:i + 2 * window + 1]) for i in range(len(profile))])
    dark = np.where(median - profile > GRID_THRESHOLD)[0]
    groups = []
    for x in dark:
        if groups and x - groups[-1][1] <= 1:
            groups[-1][1] = x
        else:
            groups.append([x, x])
    return groups


def erase_columns(rgb, groups):
    """Çizginin sol yarısını soldaki, sağ yarısını sağdaki temiz sütunla doldurur."""
    out = rgb.copy()
    w = rgb.shape[1]
    for x0, x1 in groups:
        left, right = max(0, x0 - 2), min(w - 1, x1 + 2)
        mid = (x0 + x1) / 2
        for x in range(max(0, x0 - 1), min(w, x1 + 2)):
            out[:, x] = rgb[:, left] if x <= mid else rgb[:, right]
    return out


def downscale(rgb, pixel):
    h, w = rgb.shape[0] // pixel, rgb.shape[1] // pixel
    out = np.zeros((h, w, 3), dtype=np.uint8)
    lo, hi = pixel // 4, max(pixel * 3 // 4, pixel // 4 + 1)
    for y in range(h):
        for x in range(w):
            block = rgb[y * pixel + lo:y * pixel + hi, x * pixel + lo:x * pixel + hi].reshape(-1, 3)
            out[y, x] = np.median(block, axis=0)
    return out


def quantize(rgb, colors):
    img = Image.fromarray(rgb, "RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
    return np.array(img.convert("RGB"))


def extend_down(tile, total_rows, steps=4):
    """Dokunun altını, derinleştikçe birkaç basamakta koyulaşan düz toprakla doldurur.
    (Alttaki katmanı tekrar etmek ya da aynalamak göze batan yapay desenler oluşturuyordu.)
    Basamak geçişleri dama deseniyle yumuşatılır, piksel sanat görünümü bozulmaz."""
    if tile.shape[0] >= total_rows:
        return tile[:total_rows]
    bottom = tile[-6:].reshape(-1, 3)
    colors, counts = np.unique(bottom, axis=0, return_counts=True)
    base = colors[counts.argmax()].astype(float)

    rows = total_rows - tile.shape[0]
    width = tile.shape[1]
    fill = np.zeros((rows, width, 3), dtype=np.uint8)
    step_h = max(1, rows // steps)
    for y in range(rows):
        level = min(steps - 1, y // step_h)
        # Basamağın ilk 2 satırında bir önceki tonla dama deseni
        checker = (y % step_h) < 2 and level > 0
        for parity in (0, 1):
            shade = level - (1 if checker and parity else 0)
            color = (base * (1 - 0.12 * shade)).astype(np.uint8)
            fill[y, parity::2] = color
    return np.concatenate([tile, fill], axis=0)


def from_upscaled(path, factor, depth):
    """Önceden büyütülmüş (ör. elle düzenlenip 6.25x büyütülmüş) bir dokuyu oyun piksel boyuna indirir."""
    img = Image.open(path).convert("RGB")
    size = (round(img.width / factor), round(img.height / factor))
    small = np.array(img.resize(size, Image.NEAREST))  # NEAREST büyük piksel bloklarının ortasından örnekler
    small = quantize(small.astype(np.uint8), PALETTE)
    return extend_down(small, depth)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    parser.add_argument("--buyutulmus", type=Path, help="önceden büyütülmüş hazır doku (Gemini şeridi yerine)")
    parser.add_argument("--carpan", type=float, default=6.25, help="--buyutulmus dokunun büyütme oranı")
    parser.add_argument("--derinlik", type=int, default=DEPTH, help="çıktı yüksekliği (oyun pikseli)")
    args = parser.parse_args()

    if args.buyutulmus:
        tile = from_upscaled(args.buyutulmus, args.carpan, args.derinlik)
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        Image.fromarray(tile.astype(np.uint8), "RGB").save(OUTPUT)
        print(f"{OUTPUT.relative_to(ROOT)}  ({tile.shape[1]}x{tile.shape[0]})")
        if args.preview:
            Image.fromarray(tile.astype(np.uint8), "RGB").resize((tile.shape[1] * 2, tile.shape[0] * 2), Image.NEAREST).save(args.preview)
        return

    rgb = np.array(Image.open(SOURCE).convert("RGB"), dtype=np.int32)
    strip = rgb[find_strip_top(rgb):]
    lum = strip.mean(axis=2)
    groups = grid_columns(lum)
    print(f"silinen ızgara çizgisi: {len(groups)}")
    strip = erase_columns(strip, groups)

    small = quantize(downscale(strip, PIXEL), PALETTE)
    # Gemini alt kenarı kesik bırakmış; son birkaç satırı at, gerisini düz renkle doldur.
    small = small[:-2]
    bottom = small[-3:].reshape(-1, 3)
    colors, counts = np.unique(bottom, axis=0, return_counts=True)
    fill = colors[counts.argmax()]
    if small.shape[0] < DEPTH:
        pad = np.broadcast_to(fill, (DEPTH - small.shape[0], small.shape[1], 3))
        small = np.concatenate([small, pad], axis=0)
    small = small[:DEPTH]

    tile = np.concatenate([small, small[:, ::-1]], axis=1)  # dikişsiz tekrar
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(tile.astype(np.uint8), "RGB").save(OUTPUT)
    print(f"{OUTPUT.relative_to(ROOT)}  ({tile.shape[1]}x{tile.shape[0]})")

    if args.preview:
        Image.fromarray(tile.astype(np.uint8), "RGB").resize((tile.shape[1] * 3, tile.shape[0] * 3), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
