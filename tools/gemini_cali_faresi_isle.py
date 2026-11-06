"""
Kamuflajlı Çalı Faresi'nin üç Gemini sayfasını oyuna hazır karelere çevirir.
Bu sayfalarda kareler 7x3 eşit kutuda; kutu zemini ve ayak altındaki mor gölge şeffaf yapılır.
Kareler sağa bakar; sola bakış oyunda flipX ile elde edilir.

Kullanım (proje kökünden):
  python tools/gemini_cali_faresi_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_dusman_isle import body_center_x
from gemini_sprite_isle import apply_palette, build_palette

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "cali_faresi"
CANVAS_W, CANVAS_H = 96, 64
FACTOR = 2.9  # 1 oyun pikseli = bu kadar kaynak piksel; fare ~36 piksel (Gila 34), top ~36 piksel
COLUMNS = [8, 153, 298, 444, 589, 735, 880]
ROWS = [91, 266, 442]
CELL = 137
INSET = 3

# (sayfa, satır, sütun). Sıra CombatSetupBuilder.BuildTumbleweedMouse ile aynı olmalı.
ANIMATIONS = {
    "nefes": [(1, 0, c) for c in (1, 4, 5, 6)],   # 0-3  duran çalı
    "goz": [(1, 0, 2), (1, 0, 3)],                # 4-5  içinden parlayan göz
    "top": [(2, 0, c) for c in range(7)],         # 6-12 yuvarlanan çalı
    "cikis": [(1, 1, c) for c in range(1, 6)],    # 13-17 çalıdan çıkış
    "ziplama": [(3, 0, 1)],                       # 18
    "kosu": [(2, 1, c) for c in range(7)],        # 19-25
    "bicak": [(2, 2, c) for c in range(7)],       # 26-32
    "yorgun": [(3, 0, c) for c in range(3, 7)],   # 33-36
    "saklanma": [(3, 1, c) for c in range(1, 7)], # 37-42
    "yenik": [(3, 2, c) for c in range(2, 7)],    # 43-47
}


def transparent_mask(rgb):
    """Parlak ve koyu her macenta/mor ton: kutu zemini ve gölge. Karakterin kırmızı ve pembeleri kalır."""
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    return (r - g > 35) & (b - g > 35)


def to_sprite(cell):
    mask = transparent_mask(cell)
    h, w = int(cell.shape[0] / FACTOR), int(cell.shape[1] / FACTOR)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            y0, y1 = int(y * FACTOR), int((y + 1) * FACTOR)
            x0, x1 = int(x * FACTOR), int((x + 1) * FACTOR)
            foreground = ~mask[y0:y1, x0:x1]
            if foreground.mean() >= 0.5:
                out[y, x, :3] = np.median(cell[y0:y1, x0:x1][foreground], axis=0)
                out[y, x, 3] = 255
    rows, cols = np.where((out[..., 3] > 0).any(axis=1))[0], np.where((out[..., 3] > 0).any(axis=0))[0]
    return out[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]


def place(sprite):
    canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    left = int(round(CANVAS_W / 2 - body_center_x(sprite)))
    left = max(0, min(CANVAS_W - w, left))
    assert h <= CANVAS_H, f"kare tuvale sığmadı: {w}x{h}"
    canvas[CANVAS_H - h:, left:left + w] = sprite
    return canvas


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {p: np.array(Image.open(SOURCE_DIR / f"cali_faresi_part{p}.jpg").convert("RGB")) for p in (1, 2, 3)}
    order = [(name, loc) for name, locs in ANIMATIONS.items() for loc in locs]
    sprites = []
    for _, (p, r, c) in order:
        x, y = COLUMNS[c], ROWS[r]
        sprites.append(to_sprite(pages[p][y + INSET:y + CELL - INSET, x + INSET:x + CELL - INSET]))
    palette = build_palette(sprites)

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    canvases = []
    for i, sprite in enumerate(sprites):
        canvas = place(apply_palette(sprite, palette))
        if not args.preview:
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"cali_faresi_{i}.png")
        canvases.append(canvas)
    print("kare:", len(canvases))

    # Sahnedeki sabit çalı topları farenin gizli haliyle aynı çizim olsun: oyuncu ayırt edemesin.
    if not args.preview:
        props = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
        for name, frame in (("yuz_top_a", 0), ("yuz_top_b", 1), ("yuz_top_c", 2)):
            alpha = canvases[frame][..., 3] > 0
            rows, cols = np.where(alpha.any(axis=1))[0], np.where(alpha.any(axis=0))[0]
            crop = canvases[frame][rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]
            Image.fromarray(crop, "RGBA").save(props / f"{name}.png")

    if args.preview:
        scale, cols = 2, 8
        rows_n = (len(canvases) + cols - 1) // cols
        out = Image.new("RGBA", (cols * CANVAS_W * scale, rows_n * CANVAS_H * scale), (70, 110, 130, 255))
        draw = ImageDraw.Draw(out)
        for i, c in enumerate(canvases):
            x, y = (i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale
            out.alpha_composite(Image.fromarray(c, "RGBA").resize((CANVAS_W * scale, CANVAS_H * scale), Image.NEAREST), (x, y))
            draw.text((x + 2, y + 2), f"{i} {order[i][0]}", fill=(255, 255, 0, 255))
        out.save(args.preview)


if __name__ == "__main__":
    main()
