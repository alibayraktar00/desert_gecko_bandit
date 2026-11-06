"""
Madenci Armadillo'nun üç Gemini sayfasını oyuna hazır karelere çevirir (gemini_cakal_isle.py ile aynı yöntem).
Kareler sağa bakar; sola bakış oyunda flipX ile elde edilir.

Kullanım (proje kökünden):
  python tools/gemini_armadillo_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_cakal_isle import place_on_canvas, remove_fringe, split_row, CANVAS_H, CANVAS_W
from gemini_sprite_isle import apply_palette, background_mask, build_palette, downscale

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "armadillo"
TARGET_HEIGHT = 46     # ilk karedeki karakter yüksekliği (oyun pikseli); Gila 34, çakal 44
ROW_BANDS = [(120, 270), (280, 425), (434, 572)]  # alt satırın mum ucu kareye girmesin
MIN_GAP = 4            # kareler sıkışık, kıvılcımlar ayrı kare sayılmasın diye aşağıda dar parçalar atılır

SPARK_COLORS = np.array([(255, 244, 190), (255, 216, 90), (250, 150, 40)], dtype=np.int32)

# (sayfa, satır, sütun) -> animasyon. Sıra CombatSetupBuilder.BuildArmadillo ile aynı olmalı.
ANIMATIONS = {
    "bekleme": [(3, 0, i) for i in range(7)],     # 0-6
    "devriye": [(1, 0, i) for i in range(7)],     # 7-13
    "fark": [(2, 0, i) for i in range(7)],        # 14-20
    "kapanma": [(1, 1, i) for i in range(3)],     # 21-23
    "top": [(1, 1, i) for i in range(2, 9)],      # 24-30
    "sersem": [(3, 1, i) for i in range(9)],      # 31-39
    "toparlanma": [(2, 1, i) for i in range(9)],  # 40-48
    "yenik": [(2, 2, i) for i in range(8)],       # 49-56
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {}
    for page in (1, 2, 3):
        image = np.array(Image.open(SOURCE_DIR / f"armadillo_part{page}.jpg").convert("RGB"), dtype=np.int32)
        pages[page] = [split_row(image, band, 3 if (page, r) == (2, 0) else MIN_GAP) for r, band in enumerate(ROW_BANDS)]
        print(f"part {page}: satırlardaki kare sayısı {[len(r) for r in pages[page]]}")

    first = pages[1][0][0]
    rows = np.where((~background_mask(first)).sum(axis=1) >= 3)[0]
    factor = (rows[-1] + 1 - rows[0]) / TARGET_HEIGHT
    print(f"1 oyun pikseli = {factor:.2f} kaynak piksel")

    order = [(name, loc) for name, locs in ANIMATIONS.items() for loc in locs]
    sprites = [remove_fringe(downscale(pages[p][r][c], factor)) for _, (p, r, c) in order]
    palette = np.vstack([build_palette(sprites), SPARK_COLORS])

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    canvases = []
    for i, ((name, _), sprite) in enumerate(zip(order, sprites)):
        canvas = place_on_canvas(apply_palette(sprite, palette))
        if not args.preview:
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"armadillo_{i}.png")
        canvases.append(canvas)

    if args.preview:
        scale, cols = 2, 8
        rows_n = (len(canvases) + cols - 1) // cols
        out = Image.new("RGBA", (cols * CANVAS_W * scale, rows_n * CANVAS_H * scale), (90, 90, 110, 255))
        draw = ImageDraw.Draw(out)
        for i, c in enumerate(canvases):
            x, y = (i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale
            tile = Image.fromarray(c, "RGBA").resize((CANVAS_W * scale, CANVAS_H * scale), Image.NEAREST)
            out.alpha_composite(tile, (x, y))
            draw.text((x + 2, y + 2), f"{i} {order[i][0]}", fill=(255, 255, 0, 255))
        out.save(args.preview)


if __name__ == "__main__":
    main()
