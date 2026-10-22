"""
Gemini'den gelen paralaks arka plan görsellerini oyuna hazır katmanlara çevirir.

Ne yapar:
  1. Kaynak görselden katmanı keser (kolaj ise ilgili paneli, tek görsel ise tamamını).
  2. Düz renk arka planı (gündüz macenta, gece yeşil) şeffaf yapar, kenardaki renk sızmasını temizler.
  3. Gemini'nin "sahte piksellerini" gerçek piksel ızgarasına indirir (Gila ile aynı piksel boyu).
  4. Renkleri katman başına bir palete indirir (JPEG gürültüsü gider).
  5. Katmanı aynalanmış kopyasıyla yan yana koyar: kenarlar ne olursa olsun dikişsiz tekrar eder.
  6. Alt kenarı aşağı doğru uzatır, böylece kamera aşağı inince katmanın altı boş görünmez.

Kullanım (proje kökünden):
  python tools/gemini_arkaplan_isle.py [--preview onizleme.png]

Yeni görseller gelince LAYERS listesini güncelle. Gereken paketler: pillow, numpy
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Background"

PALETTE_SIZE = 32
BOTTOM_PAD = 128     # alta eklenen piksel (Unity tarafı BackgroundBuilder.BottomPadPixels ile aynı olmalı)
EDGE_INSET = 3       # kolaj panellerinin beyaz çerçevesinden kaçmak için kesim payı

# Geçici kaynak: tek kolaj görseli (2026-10-22). Paneller ayrı ayrı gelince "box" yerine None yaz.
COLLAGE = "Gemini_Generated_Image_q5z4zaq5z4zaq5z4.jpg"

# key: "magenta" | "green" | None (gökyüzü, tamamen opak)
# pixel: kaynakta bir oyun pikselinin kaç piksel olduğu
# upscale: oyunda kaç kat büyük görünsün (gökyüzü ekranı doldursun diye 2)
# mirror: dikişsiz tekrar için aynalanmış kopyayı yanına ekle
LAYERS = [
    {"name": "gunduz_gokyuzu", "file": COLLAGE, "box": (0, 0, 702, 382), "key": None, "pixel": 2, "upscale": 2, "mirror": False},
    {"name": "gunduz_uzak", "file": COLLAGE, "box": (705, 0, 1408, 190), "key": "magenta", "pixel": 2, "upscale": 1, "mirror": True},
    {"name": "gunduz_orta", "file": COLLAGE, "box": (705, 193, 1408, 382), "key": "magenta", "pixel": 2, "upscale": 1, "mirror": True},
    {"name": "gece_gokyuzu", "file": COLLAGE, "box": (0, 387, 702, 576), "key": None, "pixel": 2, "upscale": 2, "mirror": False},
    {"name": "gece_uzak", "file": COLLAGE, "box": (705, 386, 1408, 576), "key": "green", "pixel": 2, "upscale": 1, "mirror": True},
    {"name": "gece_orta", "file": COLLAGE, "box": (0, 579, 702, 768), "key": "green", "pixel": 2, "upscale": 1, "mirror": True},
    {"name": "gece_on", "file": COLLAGE, "box": (705, 579, 1408, 768), "key": "green", "pixel": 2, "upscale": 1, "mirror": True},
]


def background_mask(rgb, key):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    if key == "magenta":
        return (r > 130) & (b > 130) & (g < 120) & (np.abs(r - b) < 100)
    if key == "green":
        return (g > 150) & (r < 130) & (b < 130)
    return np.zeros(rgb.shape[:2], dtype=bool)


def despill(rgb, key):
    """Kenar piksellerine sızmış arka plan rengini nötrler."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    if key == "magenta":
        spill = (r > g + 40) & (b > g + 40)
        rgb[spill, 2] = np.minimum(b[spill], g[spill])
    elif key == "green":
        spill = (g > r + 40) & (g > b + 40)
        rgb[spill, 1] = np.maximum(r[spill], b[spill])
    return rgb


def downscale(rgb, mask, pixel):
    """Her pixel x pixel bloğun ortasına bakar: çoğunluk ön plansa medyan rengini alır."""
    h, w = rgb.shape[0] // pixel, rgb.shape[1] // pixel
    out = np.zeros((h, w, 4), dtype=np.uint8)
    lo, hi = int(pixel * 0.25), max(int(pixel * 0.75), int(pixel * 0.25) + 1)
    for y in range(h):
        for x in range(w):
            block = rgb[y * pixel + lo:y * pixel + hi, x * pixel + lo:x * pixel + hi]
            block_fg = ~mask[y * pixel + lo:y * pixel + hi, x * pixel + lo:x * pixel + hi]
            if block_fg.mean() < 0.5:
                continue
            out[y, x, :3] = np.median(block[block_fg], axis=0)
            out[y, x, 3] = 255
    return out


def quantize(rgba):
    opaque = rgba[..., 3] > 0
    pixels = rgba[opaque][:, :3]
    strip = Image.fromarray(pixels.reshape(1, -1, 3).astype(np.uint8), "RGB")
    palette_img = strip.quantize(colors=PALETTE_SIZE, method=Image.Quantize.MEDIANCUT)
    palette = np.array(palette_img.getpalette()[:PALETTE_SIZE * 3], dtype=np.int32).reshape(-1, 3)
    colors = pixels.astype(np.int32)
    nearest = ((colors[:, None, :] - palette[None, :, :]) ** 2).sum(axis=2).argmin(axis=1)
    out = rgba.copy()
    out[opaque, :3] = palette[nearest]
    return out


def trim_empty_rows(rgba):
    """Üstteki tamamen boş satırları at (katman gereksiz yere uzun olmasın)."""
    rows = np.where(rgba[..., 3].any(axis=1))[0]
    return rgba[rows[0]:] if len(rows) else rgba


def pad_bottom(rgba, rows):
    """Alt kenarın en sık rengiyle aşağı doğru düz bir dolgu ekler.
    (Son satırı tekrar etmek renk farklarını dikey çizgi olarak uzatıyordu.)"""
    bottom = rgba[-4:].reshape(-1, 4)
    bottom = bottom[bottom[:, 3] > 0]
    colors, counts = np.unique(bottom, axis=0, return_counts=True)
    fill = np.broadcast_to(colors[counts.argmax()], (rows, rgba.shape[1], 4))
    return np.concatenate([rgba, fill], axis=0)


def process(layer, images):
    src = images[layer["file"]]
    if layer["box"]:
        x0, y0, x1, y1 = layer["box"]
        src = src[y0 + EDGE_INSET:y1 - EDGE_INSET, x0 + EDGE_INSET:x1 - EDGE_INSET]
    rgb = src.copy()
    mask = background_mask(rgb, layer["key"])
    rgb = despill(rgb, layer["key"])

    rgba = quantize(downscale(rgb, mask, layer["pixel"]))
    if layer["key"]:
        rgba = trim_empty_rows(rgba)
        rgba = pad_bottom(rgba, BOTTOM_PAD)
    if layer["mirror"]:
        rgba = np.concatenate([rgba, rgba[:, ::-1]], axis=1)
    if layer["upscale"] > 1:
        rgba = np.repeat(np.repeat(rgba, layer["upscale"], axis=0), layer["upscale"], axis=1)
    return rgba


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path, help="katmanları üst üste bindirilmiş bir önizleme kaydet")
    args = parser.parse_args()

    images = {}
    for layer in LAYERS:
        if layer["file"] not in images:
            images[layer["file"]] = np.array(Image.open(SOURCE_DIR / layer["file"]).convert("RGB"), dtype=np.int32)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    results = {}
    for layer in LAYERS:
        rgba = process(layer, images)
        path = OUTPUT_DIR / f"bg_{layer['name']}.png"
        Image.fromarray(rgba, "RGBA").save(path)
        results[layer["name"]] = rgba
        print(f"  {path.relative_to(ROOT)}  ({rgba.shape[1]}x{rgba.shape[0]})")

    if args.preview:
        # Gündüz katmanlarını 640x360'lık bir ekran gibi üst üste bindir.
        screen = Image.new("RGBA", (640, 360))
        sky = Image.fromarray(results["gunduz_gokyuzu"], "RGBA")
        screen.alpha_composite(sky.crop((0, 0, 640, 360)))
        for name, ground_y in (("gunduz_uzak", 300), ("gunduz_orta", 330)):
            img = Image.fromarray(results[name], "RGBA")
            top = ground_y - (img.height - BOTTOM_PAD)
            screen.alpha_composite(img.crop((0, 0, 640, img.height)), (0, top))
        screen.resize((1920, 1080), Image.NEAREST).save(args.preview)
        print(f"önizleme: {args.preview}")


if __name__ == "__main__":
    main()
