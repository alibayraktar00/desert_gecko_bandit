"""
Gemini'den gelen macenta arka planlı düşman sheet'lerini oyuna hazır karelere çevirir.
Gila aracındaki (gemini_sprite_isle.py) adımları kullanır; farkı, kareleri başa göre değil
gövdenin altına göre hizalamasıdır: düşman eğilip silah doğrultsa da yerinde durur.

Kareler tek yöne bakar, öbür yön oyunda SpriteRenderer.flipX ile elde edilir.

Kullanım (proje kökünden):
  python tools/gemini_dusman_isle.py [--preview onizleme.png]

Yeni düşman eklemek için ENEMIES listesine bir satır ekle.
Gereken paketler: pillow, numpy
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_sprite_isle import apply_palette, background_mask, bounding_box, build_palette, cells, downscale

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies"

CANVAS = 64  # çıktı karesi (piksel); pivot alt orta, yani alt kenar yere basar

# height: ilk karedeki karakter yüksekliği (oyun pikseli). Gila 34 piksel.
ENEMIES = [
    {"name": "yilan", "file": "yilan_silahsor.jpg", "cols": 4, "rows": 2, "margin": 5, "height": 38},
]


def body_center_x(sprite):
    """Alt %25'in yatay merkezi: silah ve baş oynasa da gövde yerinde kalır."""
    alpha = sprite[..., 3] > 0
    bottom = alpha[int(alpha.shape[0] * 0.75):]
    return np.where(bottom)[1].mean()


def place_on_canvas(sprite):
    canvas = np.zeros((CANVAS, CANVAS, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    left = int(round(CANVAS / 2 - body_center_x(sprite)))
    top = CANVAS - h
    sx0, dx0 = max(0, -left), max(0, left)
    cw = min(w - sx0, CANVAS - dx0)
    if cw < w or top < 0:
        print(f"    uyarı: kare tuvale sığmadı ({w}x{h})")
    sy0, dy0 = max(0, -top), max(0, top)
    canvas[dy0:CANVAS, dx0:dx0 + cw] = sprite[sy0:, sx0:sx0 + cw]
    return canvas


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path, help="kareleri büyütülmüş tek bir görselde göster")
    args = parser.parse_args()

    previews = []
    for enemy in ENEMIES:
        image = np.array(Image.open(SOURCE_DIR / enemy["file"]).convert("RGB"), dtype=np.int32)
        sheet = list(cells(image, enemy["cols"], enemy["rows"], enemy["margin"]))
        _, top, _, bottom = bounding_box(~background_mask(sheet[0]))
        factor = (bottom - top) / enemy["height"]
        print(f"{enemy['name']}: 1 oyun pikseli = {factor:.2f} kaynak piksel")

        sprites = [downscale(cell, factor) for cell in sheet]
        palette = build_palette(sprites)

        out_dir = OUTPUT_DIR / enemy["name"]
        out_dir.mkdir(parents=True, exist_ok=True)
        for i, sprite in enumerate(sprites):
            canvas = place_on_canvas(apply_palette(sprite, palette))
            path = out_dir / f"{enemy['name']}_{i}.png"
            Image.fromarray(canvas, "RGBA").save(path)
            previews.append(canvas)
            print(f"  {path.relative_to(ROOT)}  ({sprite.shape[1]}x{sprite.shape[0]})")

    if args.preview:
        scale = 5
        out = Image.new("RGBA", (4 * CANVAS * scale, (len(previews) + 3) // 4 * CANVAS * scale), (90, 90, 110, 255))
        for i, canvas in enumerate(previews):
            tile = Image.fromarray(canvas, "RGBA").resize((CANVAS * scale, CANVAS * scale), Image.NEAREST)
            out.alpha_composite(tile, ((i % 4) * CANVAS * scale, (i // 4) * CANVAS * scale))
        out.save(args.preview)
        print(f"önizleme: {args.preview}")


if __name__ == "__main__":
    main()
