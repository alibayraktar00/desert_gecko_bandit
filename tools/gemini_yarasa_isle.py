"""
Haydut Yarasa'nın dört Gemini sayfasını oyuna hazır karelere çevirir.
Sayfalarda kareler ızgarasız, 8x3 düzende (her sütun 128 piksel). Zemin parlak macenta, şeffaf yapılır.
Kareler sağa bakar; sola bakış oyunda flipX ile elde edilir.

Hizalama: bir satırdaki tüm kareler aynı kaydırmayı alır (çizildiği gibi kanat çırpar, titremez).
 - Uçan satırlar: satırın ortalama gövde merkezi tuvalin ortasına (32, 32) oturur.
   Prefab'da Visual çocuğu 1 birim aşağı kaydırılır, böylece transform = yarasanın ortası.
 - Yenilgi satırı: satırın en alt noktası tuvalin altına oturur (yere çakılınca pivot = yer).
Dinamit bırakma karelerinde düşen dinamit gövdeden ayrılır ve ayrı sprite olur (yarasa_dinamit.png).

Kaynaklar Assets/Sprites/_Gelen/yarasa_part1..4.png
Kullanım (proje kökünden):
  python tools/gemini_yarasa_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_sprite_isle import apply_palette, build_palette

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "yarasa"
CANVAS = 64
FACTOR = 3.6  # kanat açıkken ~33 piksel (Gila 34)
COL_W = 128

# (sayfa, satır, sütunlar, hizalama). Sıra CombatSetupBuilder.BuildBanditBat ile aynı olmalı.
ANIMATIONS = [
    ("asili", 1, 0, range(8), "center"),     # 0-7   dala asılı uyuyor
    ("ucus", 1, 1, range(8), "center"),      # 8-15  kanat çırpma
    ("suzulme", 1, 2, range(8), "center"),   # 16-23 alçak süzülme / dalış
    ("dinamit", 2, 1, range(8), "bat"),      # 24-31 0-3 fitil yanıyor, 4-7 bıraktı
    ("alay", 2, 2, range(8), "center"),      # 32-39 göz kırpıp alay eder
    ("sersem", 3, 0, range(8), "center"),    # 40-47 başında yıldızlar
    ("hasar", 4, 0, range(8), "center"),     # 48-55 çarpı gözler
    ("toparlanma", 3, 1, range(8), "center"),# 56-63 yeniden havalanır
    ("yenik", 4, 2, range(8), "bottom"),     # 64-71 havada vurulur, yere çakılır, toz
]


def transparent_mask(rgb):
    """Parlak macenta zemin ve ona karışmış kenar pikselleri. Yarasanın koyu moru (b-g ~30) kalır."""
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    return (r - g > 80) & (b - g > 80)


def row_boxes(page):
    """Sayfadaki çizim satırları (başlık hariç): komşu satıra taşmayan (y0, y1) kutuları."""
    filled = (~transparent_mask(page)).any(axis=1)
    bands, start = [], None
    for y, f in enumerate(filled):
        if f and start is None:
            start = y
        elif not f and start is not None:
            bands.append((start, y))
            start = None
    if start is not None:
        bands.append((start, len(filled)))
    bands = [b for b in bands if b[1] - b[0] > 20][1:]  # ilki "HAYDUT YARASA" başlığı
    assert len(bands) == 3, bands
    return [(max(0, b[0] - 3), min(b[1] + 3, bands[i + 1][0] if i < 2 else len(filled))) for i, b in enumerate(bands)]


def downscale(cell):
    mask = transparent_mask(cell)
    h, w = int(cell.shape[0] / FACTOR), int(cell.shape[1] / FACTOR)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            y0, y1 = int(y * FACTOR), int((y + 1) * FACTOR)
            x0, x1 = int(x * FACTOR), int((x + 1) * FACTOR)
            fg = ~mask[y0:y1, x0:x1]
            if fg.mean() >= 0.5:
                out[y, x, :3] = np.median(cell[y0:y1, x0:x1][fg], axis=0)
                out[y, x, 3] = 255
    return out


def components(alpha):
    """8 komşulu bağlı bileşenler: piksel listeleri, büyükten küçüğe."""
    seen = np.zeros_like(alpha, dtype=bool)
    found = []
    h, w = alpha.shape
    for sy, sx in zip(*np.where(alpha)):
        if seen[sy, sx]:
            continue
        pixels, queue = [], deque([(sy, sx)])
        seen[sy, sx] = True
        while queue:
            y, x = queue.popleft()
            pixels.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and alpha[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
        found.append(pixels)
    return sorted(found, key=len, reverse=True)


def split_bat(sprite):
    """En büyük bileşen yarasa; geri kalan (dinamit, kıvılcım) ayrı döner."""
    parts = components(sprite[..., 3] > 0)
    bat, rest = sprite.copy(), np.zeros_like(sprite)
    for pixels in parts[1:]:
        for y, x in pixels:
            rest[y, x] = sprite[y, x]
            bat[y, x] = 0
    return bat, rest


def bbox(sprite):
    rows, cols = np.where((sprite[..., 3] > 0).any(axis=1))[0], np.where((sprite[..., 3] > 0).any(axis=0))[0]
    return rows[0], rows[-1], cols[0], cols[-1]


def crop(sprite):
    y0, y1, x0, x1 = bbox(sprite)
    return sprite[y0:y1 + 1, x0:x1 + 1]


def paste(canvas, sprite, top, left):
    h, w = sprite.shape[:2]
    for y in range(h):
        for x in range(w):
            if sprite[y, x, 3] and 0 <= top + y < CANVAS and 0 <= left + x < CANVAS:
                canvas[top + y, left + x] = sprite[y, x]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {p: np.array(Image.open(SOURCE_DIR / f"yarasa_part{p}.png").convert("RGB")) for p in (1, 2, 3, 4)}

    boxes_of = {p: row_boxes(img) for p, img in pages.items()}
    rows = []  # her animasyon için (ad, hizalama, [kare dizileri])
    dynamite = None
    for name, page, row, cols, align in ANIMATIONS:
        y0, y1 = boxes_of[page][row]
        frames = []
        for c in cols:
            frame = downscale(pages[page][y0:y1, c * COL_W:(c + 1) * COL_W])
            if align == "bat":
                frame, rest = split_bat(frame)
                if c == 6 and (rest[..., 3] > 0).sum() > 6:
                    dynamite = crop(rest)
            frames.append(frame)
        rows.append((name, align, frames))

    palette = build_palette([crop(f) for _, _, fs in rows for f in fs])

    canvases, labels = [], []
    for name, align, frames in rows:
        boxes = [bbox(f) for f in frames]
        if align == "bottom":
            dy = CANVAS - 1 - max(b[1] for b in boxes)
        else:
            dy = int(round(CANVAS / 2 - np.mean([(b[0] + b[1]) / 2 for b in boxes])))
        dx = int(round(CANVAS / 2 - np.mean([(b[2] + b[3]) / 2 for b in boxes])))
        for i, f in enumerate(frames):
            canvas = np.zeros((CANVAS, CANVAS, 4), dtype=np.uint8)
            paste(canvas, apply_palette(f, palette), dy, dx)
            if (canvas[..., 3] > 0).sum() < (f[..., 3] > 0).sum():
                print(f"  uyarı: {name} {i} tuvale tam sığmadı")
            canvases.append(canvas)
            labels.append(f"{name} {i}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    if not args.preview:
        for i, canvas in enumerate(canvases):
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"yarasa_{i}.png")
        if dynamite is not None:
            # Pivot alt orta; prefab'da sprite yarı boyu kadar aşağı kaydırılıp ortadan döndürülür.
            Image.fromarray(apply_palette(dynamite, palette), "RGBA").save(OUT_DIR / "yarasa_dinamit.png")
    print("kare:", len(canvases), "dinamit:", None if dynamite is None else dynamite.shape)

    if args.preview:
        scale, cols = 3, 8
        n = len(canvases)
        out = Image.new("RGBA", (cols * CANVAS * scale, ((n + cols - 1) // cols) * CANVAS * scale), (70, 110, 130, 255))
        draw = ImageDraw.Draw(out)
        for i, c in enumerate(canvases):
            x, y = (i % cols) * CANVAS * scale, (i // cols) * CANVAS * scale
            out.alpha_composite(Image.fromarray(c, "RGBA").resize((CANVAS * scale, CANVAS * scale), Image.NEAREST), (x, y))
            draw.line([(x + CANVAS * scale // 2, y + CANVAS * scale - 4), (x + CANVAS * scale // 2, y + CANVAS * scale)], fill=(255, 0, 0, 255))
            draw.text((x + 2, y + 2), f"{i} {labels[i]}", fill=(255, 255, 0, 255))
        if dynamite is not None:
            print("dinamit boyutu", dynamite.shape)
        out.save(args.preview)


if __name__ == "__main__":
    main()
