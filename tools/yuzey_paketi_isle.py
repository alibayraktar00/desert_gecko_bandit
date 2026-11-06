"""
Yandan çizilmiş (2D) yüzey dekor paketini sprite'lara ayırır. Objelerin yeri elle verilir:
her görsel bir kaç nesneden oluştuğu için gerekli parçaları dikdörtgenle seçmek bağlı bileşen aramaktan güvenilir.

Kullanım (proje kökünden):  python tools/yuzey_paketi_isle.py [--preview onizleme.png]
Gereken paketler: pillow, numpy
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

from magara_paketi_isle import OUTPUT_DIR, SOURCE_DIR, background_mask, downscale

# Kaynak piksel: (x0, y0, x1, y1)
SHEETS = {
    "yuzey_paket_1.jpg": {
        "labels": [(100, 44, 172, 64), (378, 18, 450, 38), (627, 86, 702, 107), (853, 86, 927, 107), (93, 233, 167, 254),
                   (400, 266, 474, 288), (840, 319, 914, 340), (93, 563, 167, 584), (349, 563, 423, 584), (604, 563, 678, 584),
                   (857, 541, 931, 562)],
        "objects": {
            "yuz_kaktuslar": (20, 40, 255, 202), "yuz_agac_1": (275, 25, 420, 202), "yuz_agac_2": (425, 30, 555, 202),
            "yuz_kurukafa": (790, 108, 992, 202),
            "yuz_kaktus_a": (25, 40, 102, 202), "yuz_kaktus_b": (108, 140, 165, 202), "yuz_kaktus_c": (160, 85, 255, 202),
            "yuz_direk": (78, 255, 186, 505),
            "yuz_araba": (290, 305, 625, 505), "yuz_degirmen": (670, 230, 970, 505), "yuz_kamp": (18, 600, 264, 735),
            "yuz_cit_direk": (285, 600, 492, 735), "yuz_sandik_varil": (525, 600, 757, 735),
            "yuz_tabela_yon": (798, 568, 897, 735), "yuz_tabela_aranan": (903, 568, 1005, 735),
        },
    },
    "yuzey_paket_2.jpg": {
        "labels": [(100, 44, 172, 64), (378, 18, 450, 38), (627, 86, 702, 107), (853, 86, 927, 107), (121, 343, 195, 364),
                   (401, 343, 475, 364), (664, 341, 738, 362), (876, 267, 950, 288), (93, 563, 167, 584), (389, 563, 463, 584),
                   (629, 563, 703, 584), (857, 541, 931, 562)],
        "objects": {
            "yuz_cali": (28, 72, 252, 202), "yuz_cali_a": (28, 105, 112, 202), "yuz_cali_b": (105, 72, 192, 202), "yuz_cali_c": (190, 150, 252, 202),
            "yuz_kaya_1": (278, 45, 425, 202), "yuz_kaya_2": (428, 60, 552, 202),
            "yuz_kutuk": (575, 122, 762, 202), "yuz_cit": (94, 383, 298, 507),  # uçtan uca direkten direğe: yan yana dizilince dikişsiz "yuz_kova_direk": (335, 372, 552, 512),
            "yuz_cuval": (578, 378, 837, 507), "yuz_tabela_yon_2": (860, 308, 977, 507), "yuz_cadir": (18, 580, 272, 735),
            "yuz_kazma": (303, 595, 552, 735), "yuz_mezar": (585, 595, 752, 735), "yuz_aranan_kavanoz": (793, 568, 997, 735),
        },
    },
}
PIXEL = 3

# Tek görsellik fener: büyük çizilmiş, kendi piksel boyu var. Çevresindeki düz ışık halkası oyunda gerçek parıltıyla çizilir.
LANTERN = {"file": "fener_direk.jpg", "pixel": 9, "halo": (415, 285, 130), "keep": [(362, 195, 470, 372), (395, 90, 435, 200), (0, 0, 300, 765)]}


def lantern():
    rgb = np.array(Image.open(SOURCE_DIR / LANTERN["file"]).convert("RGB"))
    mask = background_mask(rgb, 22)
    cx, cy, radius = LANTERN["halo"]
    yy, xx = np.mgrid[0:rgb.shape[0], 0:rgb.shape[1]]
    halo = (xx - cx) ** 2 + (yy - cy) ** 2 <= radius ** 2
    for x0, y0, x1, y1 in LANTERN["keep"]:
        halo[y0:y1, x0:x1] = False
    mask |= halo
    pixel = LANTERN["pixel"]
    h, w = rgb.shape[0] // pixel, rgb.shape[1] // pixel
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            fg = ~mask[y * pixel:y * pixel + pixel, x * pixel:x * pixel + pixel]
            if fg.mean() >= 0.5:
                out[y, x, :3] = np.median(rgb[y * pixel:y * pixel + pixel, x * pixel:x * pixel + pixel][fg], axis=0)
                out[y, x, 3] = 255
    alpha = out[..., 3] > 0
    rows, cols = np.where(alpha.any(axis=1))[0], np.where(alpha.any(axis=0))[0]
    return out[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]


# Kamp ateşi: alevler kamp görselinden silinir, yerine dalgalanan ve duman tüten kareler üretilir.
FIRE_ERASE = (0, 0, 25, 36)  # x0, y0, x1, y1 (oyun pikseli)
FIRE_FRAMES, FIRE_W, FIRE_H = 12, 32, 54
FLAME_BANDS = [(255, 244, 190), (255, 216, 90), (250, 150, 40), (214, 84, 30), (120, 34, 22)]
SMOKE_COLOR = (180, 168, 162)


def flame(canvas, cx, height, half_width, t, phase):
    """Tek alev dili. Halkalar içten dışa renklenir; t tam döngüde başa döndüğü için animasyon dikişsizdir."""
    for row in range(height):
        v = row / height
        width = half_width * (1 - v ** 1.4) * (0.75 + 0.5 * min(1, v * 4))
        sway = np.sin(2 * np.pi * (2 * t + phase) - row * 0.55) * (0.3 + 2.2 * v)
        center = cx + sway
        y = FIRE_H - 1 - row
        for x in range(int(center - width - 1), int(center + width + 2)):
            distance = abs(x + 0.5 - center) / max(width, 0.6)
            if distance > 1 or not 0 <= x < FIRE_W:
                continue
            level = distance + v * 0.75
            band = 0 if level < 0.35 else 1 if level < 0.65 else 2 if level < 1.0 else 3
            if distance > 0.8:
                band = max(band, 3)
            current = canvas[y, x]
            if current[3] == 0 or band < FLAME_BANDS.index(tuple(current[:3])):
                canvas[y, x] = (*FLAME_BANDS[band], 255)


def fire_frames():
    frames = []
    for i in range(FIRE_FRAMES):
        t = i / FIRE_FRAMES
        canvas = np.zeros((FIRE_H, FIRE_W, 4), dtype=np.uint8)
        cx = FIRE_W / 2
        tall = 15 + int(round(2.5 * np.sin(2 * np.pi * t * 3)))
        flame(canvas, cx - 4.5, 8 + int(round(1.5 * np.sin(2 * np.pi * (t * 2 + 0.3)))), 2.6, t, 0.35)
        flame(canvas, cx + 4.5, 7 + int(round(1.5 * np.sin(2 * np.pi * (t * 2 + 0.7)))), 2.4, t, 0.7)
        flame(canvas, cx, tall, 4.2, t, 0.0)
        # Duman: alevin üstünden süzülen, gittikçe büyüyüp solan kümeler
        for puff in range(4):
            age = (t + puff / 4) % 1
            y = FIRE_H - 1 - (17 + age * 33)
            x = cx + np.sin(2 * np.pi * (age * 1.2 + puff * 0.3)) * (0.5 + age * 2.5)
            radius = 0.8 + age * 1.6
            alpha = int(210 * (1 - age) ** 0.8)
            for py in range(int(y - radius), int(y + radius) + 2):
                for px in range(int(x - radius), int(x + radius) + 2):
                    if (px + 0.5 - x) ** 2 + (py + 0.5 - y) ** 2 <= radius ** 2 and 0 <= px < FIRE_W and 0 <= py < FIRE_H and canvas[py, px, 3] == 0:
                        canvas[py, px] = (*SMOKE_COLOR, alpha)
        frames.append(canvas)
    return frames


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    sprites = {}
    for file, sheet in SHEETS.items():
        rgb = np.array(Image.open(SOURCE_DIR / file).convert("RGB"))
        mask = background_mask(rgb, 24)
        for x0, y0, x1, y1 in sheet["labels"]:
            mask[y0:y1, x0:x1] = True
        rgba = downscale(rgb, mask)
        for name, (x0, y0, x1, y1) in sheet["objects"].items():
            crop = rgba[y0 // PIXEL:y1 // PIXEL, x0 // PIXEL:x1 // PIXEL]
            alpha = crop[..., 3] > 0
            rows, cols = np.where(alpha.any(axis=1))[0], np.where(alpha.any(axis=0))[0]
            sprites[name] = crop[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]

    sprites["mgr_fener"] = lantern()

    camp = sprites["yuz_kamp"].copy()
    x0, y0, x1, y1 = FIRE_ERASE
    camp[y0:y1, x0:x1] = 0
    sprites["yuz_kamp"] = camp
    for i, frame in enumerate(fire_frames()):
        sprites[f"yuz_ates_{i}"] = frame

    if args.preview:
        canvas = Image.new("RGBA", (1280, 760), (90, 130, 150, 255))
        x = y = row = 6
        row = 0
        for sprite in sprites.values():
            image = Image.fromarray(sprite, "RGBA").resize((sprite.shape[1] * 2, sprite.shape[0] * 2), Image.NEAREST)
            if x + image.width > 1270:
                x = 6
                y += row + 8
                row = 0
            canvas.alpha_composite(image, (x, y))
            x += image.width + 10
            row = max(row, image.height)
        canvas.save(args.preview)
        return

    for name, sprite in sprites.items():
        Image.fromarray(sprite, "RGBA").save(OUTPUT_DIR / f"{name}.png")
        print(f"{name}: {sprite.shape[1]}x{sprite.shape[0]}")


if __name__ == "__main__":
    main()
