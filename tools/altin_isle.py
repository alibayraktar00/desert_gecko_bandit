"""
Altın sayfasını oyuna hazır karelere çevirir: 4 sütun, 3 satır, koyu gri zemin, kare adları üstte.
  0     coin_00      kertenkele damgalı altın (bekler)
  1-3   coin_01..03  parıltı gezinir, 3'te kısa ışık çakar (beklerken arada bir oynar)
  4     coin_03 (2. satır)  toplandığı an: ışınlı patlama
  5     coin_04      ikiye kırılır, parçalar sıçrar
  6-7   coin_05..06  altın kırıntıları saçılır, seyrelir
  8     coin_06 (3. satır)  son kıvılcımlar
  9     coin_07      havada sönen duman
Her kare kendi gövdesinin (en büyük parça) ortasından tuvalin ortasına; saçılan kareler biraz aşağıda. Merkez pivot.
Ayrıca altin_hale.png: altının arkasında yavaşça zonklayan sıcak parıltı;
altin_ikon.png: arayüzdeki sayaç için kenar boşluğu kırpılmış altın.

Kaynak Assets/Sprites/_Gelen/altin.png
Kullanım (proje kökünden):
  python tools/altin_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_sprite_isle import apply_palette, build_palette
from gemini_yarasa_isle import bbox, components, crop

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets" / "Sprites" / "_Gelen" / "altin.png"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Collectibles" / "altin"
CANVAS = 40
FACTOR = 8.0          # altın ~18 piksel çapında (Gila 34)
COL_W = 256
# (satır, sütunlar): kare adlarının altından
CELLS = [((45, 262), range(4)), ((292, 515), range(4)), ((550, 765), range(2))]
BG = 75
FALL = [0, 0, 0, 0, 0, 0, 2, 3, 4, 3]  # kare başına aşağı kayma (piksel)


def foreground(cell):
    """Zemin nötr gri; ona karışmış soluk hale de şeffaf. Duman (son kare) gri ama zeminden açık: kalır."""
    c = cell.astype(int)
    return np.abs(c - BG).max(axis=2) > 24


def downscale(cell):
    fg_mask = foreground(cell)
    h, w = int(cell.shape[0] / FACTOR), int(cell.shape[1] / FACTOR)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            y0, y1 = int(y * FACTOR), int((y + 1) * FACTOR)
            x0, x1 = int(x * FACTOR), int((x + 1) * FACTOR)
            fg = fg_mask[y0:y1, x0:x1]
            if fg.mean() >= 0.4:
                out[y, x, :3] = np.median(cell[y0:y1, x0:x1][fg], axis=0)
                out[y, x, 3] = 255
    return out


def paste(canvas, sprite, top, left):
    h, w = sprite.shape[:2]
    for y in range(h):
        for x in range(w):
            if sprite[y, x, 3] and 0 <= top + y < CANVAS and 0 <= left + x < CANVAS:
                canvas[top + y, left + x] = sprite[y, x]


def halo(size=32):
    img = np.zeros((size, size, 4), dtype=np.uint8)
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = np.hypot(x - c, y - c) / (size / 2)
            if d < 1:
                img[y, x] = (255, 214, 110, int(255 * (1 - d) ** 2))
    return img


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    page = np.array(Image.open(SOURCE).convert("RGB"))
    frames = []
    for (y0, y1), cols in CELLS:
        for c in cols:
            frames.append(downscale(page[y0:y1, c * COL_W:(c + 1) * COL_W]))

    palette = build_palette([crop(f) for f in frames if (f[..., 3] > 0).any()])
    canvases = []
    for i, f in enumerate(frames):
        canvas = np.zeros((CANVAS, CANVAS, 4), dtype=np.uint8)
        if (f[..., 3] > 0).any():
            # Sayfada her hücre farklı yere çizilmiş: her kare kendi ortasından tuvalin ortasına,
            # dökülen parçalar (3-5) yerçekimiyle biraz aşağıda.
            main = np.array(components(f[..., 3] > 0)[0])
            big = len(main) > 40  # altın gövdesi varsa onu, yoksa (kırıntılar) hepsini ortala
            fy0, fy1, fx0, fx1 = (main[:, 0].min(), main[:, 0].max(), main[:, 1].min(), main[:, 1].max()) if big else bbox(f)
            dy = int(round(CANVAS / 2 - (fy0 + fy1) / 2)) + FALL[i]
            dx = int(round(CANVAS / 2 - (fx0 + fx1) / 2))
            paste(canvas, apply_palette(f, palette), dy, dx)
            if (canvas[..., 3] > 0).sum() < (f[..., 3] > 0).sum():
                print(f"  uyarı: kare {i} tuvale tam sığmadı")
        canvases.append(canvas)
    y0, y1, x0, x1 = bbox(frames[0])
    print("kare:", len(canvases), "altın:", x1 - x0 + 1, "x", y1 - y0 + 1)

    if not args.preview:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        for i, canvas in enumerate(canvases):
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"altin_{i}.png")
        Image.fromarray(halo(), "RGBA").save(OUT_DIR / "altin_hale.png")
        # Arayüz ikonu: 0. karenin yalnız altını (kenar boşluğu yok), sayaç kutusunu doldursun.
        main = np.array(components(canvases[0][..., 3] > 0)[0])
        y0, y1, x0, x1 = main[:, 0].min(), main[:, 0].max(), main[:, 1].min(), main[:, 1].max()
        Image.fromarray(canvases[0][y0:y1 + 1, x0:x1 + 1], "RGBA").save(OUT_DIR / "altin_ikon.png")
        return

    scale = 6
    out = Image.new("RGBA", (4 * CANVAS * scale, 3 * CANVAS * scale), (40, 46, 70, 255))
    draw = ImageDraw.Draw(out)
    for i, c in enumerate(canvases):
        x, y = (i % 4) * CANVAS * scale, (i // 4) * CANVAS * scale
        out.alpha_composite(Image.fromarray(c, "RGBA").resize((CANVAS * scale, CANVAS * scale), Image.NEAREST), (x, y))
        draw.text((x + 2, y + 2), str(i), fill=(255, 255, 0, 255))
    out.save(args.preview)


if __name__ == "__main__":
    main()
