"""
Gemini'den gelen büyük, macenta arka planlı sprite sheet'leri oyuna hazır piksel karelere çevirir.

Ne yapar:
  1. Sheet'i eşit hücrelere böler (ızgara çizgilerini kenar payıyla atar).
  2. Macenta arka planı şeffaf yapar.
  3. Tüm sheet'leri AYNI ölçekle küçültür (karakter her animasyonda aynı boyda kalsın).
  4. Renkleri ortak bir palete indirir (JPEG gürültüsünü temizler).
  5. Her kareyi ayakları aynı hizada olacak şekilde 64x64 tuvale yerleştirip PNG kaydeder.

Kullanım (proje kökünden):
  python tools/gemini_sprite_isle.py [--preview onizleme.png]

Yeni bir sheet eklemek için aşağıdaki SHEETS listesine bir satır ekle.
Gereken paketler: pillow, numpy
"""
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Player" / "Gila"

CANVAS = 64          # çıktı karesi (piksel)
FEET_ROW = 48        # ayakların oturduğu satır (yukarıdan); pivot buraya denk gelir -> 16/64 = 0.25
TARGET_HEIGHT = 34   # idle karesinde şapka tepesinden ayağa karakter yüksekliği (piksel)
PALETTE_SIZE = 24
CELL_MARGIN = 8      # hücre kenarından atılan pay (Gemini'nin çizdiği ızgara çizgileri için)

# anchor: "bottom" -> ayaklar FEET_ROW'a oturur (yerdeki kareler)
#         "top"    -> başın tepesi idle karesindeki hizaya oturur (havadaki kareler, sarkan kuyruk ayak hizasının altına inebilir)
SHEETS = [
    # Gemini ikinci satırı biraz büyük bir kopya olarak çizdi; döngüde boy zıplamasın diye sadece ilk satır.
    {"file": "gila_idle.jpg", "cols": 4, "rows": 2,
     "frames": [("idle", i, "bottom") for i in range(4)]},
    {"file": "gila_run.jpg", "cols": 4, "rows": 2,
     "frames": [("run", i, "bottom") for i in range(8)]},
    {"file": "gila_jump_fall.jpg", "cols": 4, "rows": 1,
     "frames": [("crouch", 0, "bottom"), ("rise", 1, "top"), ("apex", 2, "top"), ("fall", 3, "top")]},
]
SCALE_REFERENCE = ("gila_idle.jpg", 0)  # ölçek bu karedeki karakter boyuna göre hesaplanır


def background_mask(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (r > 130) & (b > 130) & (g < 120) & (np.abs(r - b) < 100)


def cells(image, cols, rows):
    w, h = image.shape[1] / cols, image.shape[0] / rows
    for row in range(rows):
        for col in range(cols):
            x0, y0 = int(col * w) + CELL_MARGIN, int(row * h) + CELL_MARGIN
            x1, y1 = int((col + 1) * w) - CELL_MARGIN, int((row + 1) * h) - CELL_MARGIN
            yield image[y0:y1, x0:x1]


def bounding_box(fg):
    # 3 pikselden az dolu satır/sütunlar JPEG kırıntısı sayılır.
    rows = np.where(fg.sum(axis=1) >= 3)[0]
    cols = np.where(fg.sum(axis=0) >= 3)[0]
    return cols[0], rows[0], cols[-1] + 1, rows[-1] + 1


def downscale(cell, factor):
    """Her çıktı pikseli için kaynak bloğun ortasına bakar: çoğunluk ön plansa medyan rengini alır."""
    fg = ~background_mask(cell)
    x0, y0, x1, y1 = bounding_box(fg)
    cell, fg = cell[y0:y1, x0:x1], fg[y0:y1, x0:x1]
    out_w, out_h = math.ceil(cell.shape[1] / factor), math.ceil(cell.shape[0] / factor)
    rgba = np.zeros((out_h, out_w, 4), dtype=np.uint8)
    for oy in range(out_h):
        for ox in range(out_w):
            # bloğun orta %60'ı: komşu piksellere taşan kenar karışımlarını dışarıda bırakır
            sx0, sx1 = (ox + 0.2) * factor, (ox + 0.8) * factor
            sy0, sy1 = (oy + 0.2) * factor, (oy + 0.8) * factor
            block_fg = fg[int(sy0):max(int(sy1), int(sy0) + 1), int(sx0):max(int(sx1), int(sx0) + 1)]
            if block_fg.size == 0 or block_fg.mean() < 0.5:
                continue
            block = cell[int(sy0):max(int(sy1), int(sy0) + 1), int(sx0):max(int(sx1), int(sx0) + 1)]
            rgba[oy, ox, :3] = np.median(block[block_fg], axis=0)
            rgba[oy, ox, 3] = 255
    return rgba


def build_palette(sprites):
    pixels = np.concatenate([s[s[..., 3] > 0][:, :3] for s in sprites])
    strip = Image.fromarray(pixels.reshape(1, -1, 3).astype(np.uint8), "RGB")
    quantized = strip.quantize(colors=PALETTE_SIZE, method=Image.Quantize.MEDIANCUT)
    return np.array(quantized.getpalette()[:PALETTE_SIZE * 3], dtype=np.int32).reshape(-1, 3)


def apply_palette(sprite, palette):
    opaque = sprite[..., 3] > 0
    colors = sprite[opaque][:, :3].astype(np.int32)
    nearest = ((colors[:, None, :] - palette[None, :, :]) ** 2).sum(axis=2).argmin(axis=1)
    out = sprite.copy()
    out[opaque, :3] = palette[nearest]
    return out


def head_center_x(sprite):
    """Başın (üst %40) yatay merkezi. Kuyruk sallansa da karakter yerinde durur."""
    alpha = sprite[..., 3] > 0
    top = alpha[: max(1, int(alpha.shape[0] * 0.4))]
    xs = np.where(top)[1]
    return xs.mean()


def place_on_canvas(sprite, anchor, reference_height):
    canvas = np.zeros((CANVAS, CANVAS, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    left = int(round(CANVAS / 2 - head_center_x(sprite)))
    top = FEET_ROW - h if anchor == "bottom" else FEET_ROW - reference_height

    # Tuvale sığmayan kısımları kırp.
    sx0, sy0 = max(0, -left), max(0, -top)
    dx0, dy0 = max(0, left), max(0, top)
    cw, ch = min(w - sx0, CANVAS - dx0), min(h - sy0, CANVAS - dy0)
    if cw < w or ch < h:
        print(f"    uyarı: kare tuvale sığmadı, {w - cw}x{h - ch} piksel kırpıldı")
    canvas[dy0:dy0 + ch, dx0:dx0 + cw] = sprite[sy0:sy0 + ch, sx0:sx0 + cw]
    return canvas


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path, help="tüm kareleri büyütülmüş tek bir görselde göster")
    args = parser.parse_args()

    images = {s["file"]: np.array(Image.open(SOURCE_DIR / s["file"]).convert("RGB"), dtype=np.int32) for s in SHEETS}

    ref_file, ref_index = SCALE_REFERENCE
    ref_sheet = next(s for s in SHEETS if s["file"] == ref_file)
    ref_cell = list(cells(images[ref_file], ref_sheet["cols"], ref_sheet["rows"]))[ref_index]
    _, top, _, bottom = bounding_box(~background_mask(ref_cell))
    factor = (bottom - top) / TARGET_HEIGHT
    print(f"ölçek: 1 oyun pikseli = {factor:.2f} kaynak piksel")

    frames = []  # (anim, index, anchor, sprite)
    for sheet in SHEETS:
        sheet_cells = list(cells(images[sheet["file"]], sheet["cols"], sheet["rows"]))
        for anim, cell_index, anchor in sheet["frames"]:
            frames.append((anim, cell_index, anchor, downscale(sheet_cells[cell_index], factor)))

    palette = build_palette([f[3] for f in frames])
    reference_height = TARGET_HEIGHT

    # Önceki çalıştırmadan kalan fazla kareleri sil (Unity .meta dosyalarıyla birlikte).
    for anim in {f[0] for f in frames}:
        for old in (OUTPUT_DIR / anim).glob(f"gila_{anim}_*.png*"):
            old.unlink()

    counters = {}
    finished = []
    for anim, _, anchor, sprite in frames:
        sprite = apply_palette(sprite, palette)
        canvas = place_on_canvas(sprite, anchor, reference_height)
        n = counters.get(anim, 0)
        counters[anim] = n + 1
        out_dir = OUTPUT_DIR / anim
        out_dir.mkdir(parents=True, exist_ok=True)
        path = out_dir / f"gila_{anim}_{n}.png"
        Image.fromarray(canvas, "RGBA").save(path)
        finished.append((anim, canvas))
        print(f"  {path.relative_to(ROOT)}  ({sprite.shape[1]}x{sprite.shape[0]})")

    if args.preview:
        scale, cols = 5, 8
        rows = math.ceil(len(finished) / cols)
        sheet = Image.new("RGBA", (cols * CANVAS * scale, rows * CANVAS * scale), (90, 90, 110, 255))
        for i, (_, canvas) in enumerate(finished):
            tile = Image.fromarray(canvas, "RGBA").resize((CANVAS * scale, CANVAS * scale), Image.NEAREST)
            x, y = (i % cols) * CANVAS * scale, (i // cols) * CANVAS * scale
            sheet.alpha_composite(tile, (x, y))
            # ayak hizası çizgisi
            for px in range(CANVAS * scale):
                sheet.putpixel((x + px, y + FEET_ROW * scale), (255, 255, 255, 120))
        sheet.save(args.preview)
        print(f"önizleme: {args.preview}")


if __name__ == "__main__":
    main()
