"""
Zümrüt Kaktüs Böceği'nin sayfasını (4x3, koyu gri zemin, kare adları üstte) oyuna hazır karelere çevirir.
Kareler sağa bakar. Zemin ve karın ışığının gri zemine karışmış yumuşak halesi şeffaf yapılır:
oyunda hale ayrı bir parıltı sprite'ı (zonklayan) ve 2D ışıkla verilir.

Hizalama: tüm kareler aynı kaydırmayı alır. Merkez pivot.

Süzülme döngüsü (bocek_0..7) sayfadaki 0-7 karelerinden alınmaz: orada her karede gövdenin yeri, eğimi ve
karnın parlaklığı değişiyor, döngüde böcek titriyormuş gibi görünüyordu. Bunun yerine tek bir gövde (kare 0,
kanadı silinmiş) sabit kalır, üstüne yarı saydam iki kanat çizilir ve yalnız kanatlar yavaşça açılıp kapanır.
Ayrıca üretilir:
  bocek_hale.png   yumuşak yeşil parıltı (karın ışığı, zonklar)
  bocek_halka.png  ince beyaz halka (yutunca Gila'dan yayılan şifa dalgası, renk oyunda verilir)

Kaynak Assets/Sprites/_Gelen/zumrut_bocek.png
Kullanım (proje kökünden):
  python tools/zumrut_bocek_isle.py [--preview onizleme.png]
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
SOURCE = ROOT / "Assets" / "Sprites" / "_Gelen" / "zumrut_bocek.png"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Collectibles" / "bocek"
CANVAS = 40
FACTOR = 5.6           # gövde ~25 piksel (Gila 34)
COL_W = 256
ROW_BANDS = [(68, 262), (298, 495), (528, 765)]  # kare adlarının altından


def foreground(cell):
    """Zemin: nötr gri (~61). Hale: gri zemine karışmış soluk yeşil (g baskın, r ile b yakın, parlak değil).
    Kabuk zeytin yeşili (mavisi düşük), karın neon (g çok yüksek), bacaklar koyu gri: hepsi kalır."""
    c = cell.astype(int)
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    neutral = (c.max(axis=2) - c.min(axis=2) <= 8) & (np.abs(c.mean(axis=2) - 61) <= 10)
    glow = (g - np.maximum(r, b) >= 10) & (g < 140) & (np.abs(r - b) <= 20) & (r >= 45)
    return ~(neutral | glow)


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


def body_center(sprite):
    """En büyük bileşenin (gövde) kutusunun ortası: kıvılcımlar hizalamayı kaydırmasın."""
    pixels = np.array(components(sprite[..., 3] > 0)[0])
    return (pixels[:, 0].min() + pixels[:, 0].max()) / 2, (pixels[:, 1].min() + pixels[:, 1].max()) / 2


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
                img[y, x] = (110, 255, 140, int(255 * (1 - d) ** 2))
    return img


def ring(size=64, thickness=2.2):
    img = np.zeros((size, size, 4), dtype=np.uint8)
    c = (size - 1) / 2
    r = size / 2 - thickness
    for y in range(size):
        for x in range(size):
            d = abs(np.hypot(x - c, y - c) - r)
            if d < thickness:
                img[y, x] = (255, 255, 255, int(255 * min(1.0, (thickness - d) / 1.2)))
    return img


# Kare 0'da kanat: gövde tepesinin üstü, başın solu. Kanat kökü kabuğun ortası.
WING_ROWS_ABOVE = 17      # bu satır ve üstü
WING_MAX_X = 24           # bu sütun ve solu (sağı başın üst çizgisi)
WING_ROOT = (19.5, 18.5)  # (x, y)
# Bir çırpış, 8 kare: kanadın yataydan (geriye doğru) yukarı açısı ve o karede hız bulanıklığı.
WING_STROKE = [(8, 0), (20, 0), (40, 0), (64, 1), (82, 1), (66, 1), (42, 0), (20, 0)]
WING_FILL = (150, 182, 140, 150)
WING_EDGE = (98, 112, 92, 225)
WING_BLUR = (170, 210, 160, 60)


def wing_layer(angle, length, width, root, blur):
    """Kökünden geriye ve yukarı uzanan elips kanat (yarı saydam). angle: yataydan yukarı derece."""
    layer = np.zeros((CANVAS, CANVAS, 4), dtype=np.uint8)
    a = np.radians(180 - angle)  # geriye (sola) bakan eksen, yukarı açılır
    axis = np.array([np.cos(a), -np.sin(a)])
    normal = np.array([-axis[1], axis[0]])
    for y in range(CANVAS):
        for x in range(CANVAS):
            d = np.array([x + 0.5 - root[0], y + 0.5 - root[1]])
            u, v = d @ axis, d @ normal
            e = ((u - length / 2) / (length / 2)) ** 2 + (v / (width / 2)) ** 2
            if blur and e <= 1.9 and layer[y, x, 3] == 0:
                layer[y, x] = WING_BLUR
            if e <= 1.0:
                layer[y, x] = WING_EDGE if e > 0.45 else WING_FILL
    return layer


def over(dst, src):
    """src'yi dst'nin üstüne alfa ile karıştırır."""
    sa = src[..., 3:4] / 255.0
    da = dst[..., 3:4] / 255.0
    out_a = sa + da * (1 - sa)
    rgb = np.where(out_a > 0, (src[..., :3] * sa + dst[..., :3] * da * (1 - sa)) / np.maximum(out_a, 1e-6), 0)
    return np.dstack([rgb, out_a * 255]).round().astype(np.uint8)


def hover_loop(frame0):
    """Kare 0'ın gövdesi sabit, yalnız kanatlar çırpar. Arka kanat önce, sonra gövde, en üstte ön kanat."""
    body = frame0.copy()
    body[:WING_ROWS_ABOVE + 1, :WING_MAX_X + 1] = 0
    loop = []
    for angle, blur in WING_STROKE:
        canvas = np.zeros_like(body)
        canvas = over(canvas, wing_layer(angle + 14, 8.0, 3.2, (WING_ROOT[0] + 1.5, WING_ROOT[1]), blur))
        canvas = over(canvas, body)
        canvas = over(canvas, wing_layer(angle, 9.0, 3.6, WING_ROOT, blur))
        loop.append(canvas)
    return loop


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
    centers = [body_center(f) for f in frames[:8]]  # süzülme kareleri ortak hizayı belirler
    dy = int(round(CANVAS / 2 - np.mean([c[0] for c in centers])))
    dx = int(round(CANVAS / 2 - np.mean([c[1] for c in centers])))

    canvases = []
    for i, f in enumerate(frames):
        canvas = np.zeros((CANVAS, CANVAS, 4), dtype=np.uint8)
        paste(canvas, apply_palette(f, palette), dy, dx)
        if (canvas[..., 3] > 0).sum() < (f[..., 3] > 0).sum():
            print(f"  uyarı: kare {i} tuvale tam sığmadı")
        canvases.append(canvas)
    canvases[:8] = hover_loop(canvases[0])
    y0, y1, x0, x1 = bbox(frames[0])
    print("kare:", len(canvases), "gövde:", x1 - x0 + 1, "x", y1 - y0 + 1)

    if not args.preview:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        for i, canvas in enumerate(canvases):
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"bocek_{i}.png")
        Image.fromarray(halo(), "RGBA").save(OUT_DIR / "bocek_hale.png")
        Image.fromarray(ring(), "RGBA").save(OUT_DIR / "bocek_halka.png")
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
