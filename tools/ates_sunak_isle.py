"""
Ateş sunağı sayfasını (4x2, koyu gri zemin, kare adları üstte, her karenin altında zemin çizgisi)
oyuna hazır karelere çevirir: boynuzlu ahşap direğin tepesinde demir sepette yanan ateş.
  0     yeni tutuşan kor (kullanılmıyor: döngü 1-7)
  1-7   alev dalgalanır, kıvılcım ve duman yükselir
Zemin çizgisi atılır; her karenin direk dibi tuvalin altına ve ortasına oturur (alt orta pivot,
Props klasöründe). Sepetin ağzından aşağısı tüm döngü karelerinde aynıdır (lock_post): yalnız alev oynar.

Kaynak Assets/Sprites/_Gelen/ates_sunak.png
Çıktı  Assets/Sprites/Environment/Props/ates_sunak_0..7.png
Kullanım (proje kökünden):
  python tools/ates_sunak_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_sprite_isle import apply_palette, build_palette
from gemini_yarasa_isle import bbox, crop

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets" / "Sprites" / "_Gelen" / "ates_sunak.png"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
CANVAS_W, CANVAS_H = 40, 72
FACTOR = 4.8          # direk ~52 piksel (Gila 34): uzaktan görünen bir işaret
COL_W = 256
ROW_BANDS = [(60, 359), (425, 726)]  # kare adlarının altından, zemin çizgisinin üstüne kadar
BG = np.array([66, 69, 68])


def foreground(cell):
    return np.abs(cell.astype(int) - BG).max(axis=2) > 22


def downscale(cell):
    fg_mask = foreground(cell)
    h, w = int(cell.shape[0] / FACTOR), int(cell.shape[1] / FACTOR)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            y0, y1 = int(y * FACTOR), int((y + 1) * FACTOR)
            x0, x1 = int(x * FACTOR), int((x + 1) * FACTOR)
            fg = fg_mask[y0:y1, x0:x1]
            if fg.mean() >= 0.45:
                out[y, x, :3] = np.median(cell[y0:y1, x0:x1][fg], axis=0)
                out[y, x, 3] = 255
    return out


def base_center_x(sprite):
    """Direğin dibinin (alttaki 6 satır) yatay ortası."""
    alpha = sprite[..., 3] > 0
    rows = np.where(alpha.any(axis=1))[0]
    base = alpha[max(0, rows[-1] - 5):rows[-1] + 1]
    return np.where(base.any(axis=0))[0].mean()


def paste(canvas, sprite, top, left):
    h, w = sprite.shape[:2]
    for y in range(h):
        for x in range(w):
            if sprite[y, x, 3] and 0 <= top + y < CANVAS_H and 0 <= left + x < CANVAS_W:
                canvas[top + y, left + x] = sprite[y, x]


def rim_row(canvas):
    """Demir sepetin ağzı: üstten inerken genişliği sepet genişliğine ulaşan ilk satır (alev daha dar)."""
    widths = (canvas[..., 3] > 0).sum(axis=1)
    top = np.where(widths > 0)[0][0]
    band = widths[top:top + 40]
    basket = band.max()
    for r in range(top, top + 40):
        if widths[r] >= basket * 0.85:
            return r
    return top


def lock_post(canvases):
    """
    Sayfada direk ve sepet her karede biraz farklı çizilmiş: döngüde sunak titriyormuş gibi görünüyordu.
    Sepetin ağzından aşağısı (sepet, boynuzlar, direk) tek kareden (1) alınıp hepsine konur;
    yalnız ağzın üstü (alev, kıvılcım, duman) karelere göre değişir.
    """
    base = canvases[1]
    r = rim_row(base)
    for c in canvases[1:]:
        c[r:] = base[r:]
    print("sabit kısım satır", r, "ve aşağısı")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    page = np.array(Image.open(SOURCE).convert("RGB"))
    frames = []
    for y0, y1 in ROW_BANDS:
        for c in range(4):
            frames.append(downscale(page[y0:y1, c * COL_W:(c + 1) * COL_W]))

    palette = build_palette([crop(f) for f in frames])
    canvases = []
    for i, f in enumerate(frames):
        canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
        dy = CANVAS_H - 1 - bbox(f)[1]
        dx = int(round(CANVAS_W / 2 - base_center_x(f)))
        paste(canvas, apply_palette(f, palette), dy, dx)
        if (canvas[..., 3] > 0).sum() < (f[..., 3] > 0).sum():
            print(f"  uyarı: kare {i} tuvale tam sığmadı")
        canvases.append(canvas)
    lock_post(canvases)
    y0, y1, x0, x1 = bbox(canvases[1])
    print("kare:", len(canvases), "boy:", y1 - y0 + 1, "en:", x1 - x0 + 1)

    if not args.preview:
        for i, canvas in enumerate(canvases):
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"ates_sunak_{i}.png")
        return

    scale = 4
    out = Image.new("RGBA", (8 * CANVAS_W * scale, CANVAS_H * scale), (30, 34, 60, 255))
    draw = ImageDraw.Draw(out)
    for i, c in enumerate(canvases):
        out.alpha_composite(Image.fromarray(c, "RGBA").resize((CANVAS_W * scale, CANVAS_H * scale), Image.NEAREST), (i * CANVAS_W * scale, 0))
        draw.text((i * CANVAS_W * scale + 2, 2), str(i), fill=(255, 255, 0, 255))
    out.save(args.preview)


if __name__ == "__main__":
    main()
