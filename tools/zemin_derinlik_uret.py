"""Zeminin derin kısmını, yamaç kenarlarını ve ahşap merdiveni üretir (32 PPU).

zemin_toprak.png yalnızca üst 128 satırda çizilidir; altı düz renkti. Bu araç üst kısma dokunmadan
alt kısmı aynı dokudan alınan parçalarla doldurur. Tekrar çalıştırılabilir, rastgelelik sabit tohumludur.
Çalıştırma: python tools/zemin_derinlik_uret.py
"""
import math
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
GROUND_DIR = ROOT / "Assets/Sprites/Environment/Ground"
GROUND = GROUND_DIR / "zemin_toprak.png"
LAYER_DIR = ROOT / "Assets/Sprites/Environment/Katman"

PAINTED_ROWS = 128      # zemin_toprak.png'nin elle çizilmiş üst kısmı
WIDTH = 800
TILE_ROWS = 256         # derin dokunun yüksekliği, dikeyde ve yatayda tekrar eder
RED_BAND = (40, 66)     # kırmızı toprak katmanları
ROCK_BAND = (74, 124)   # taş, kök ve kemik katmanı

SOIL = (0x2E, 0x17, 0x10)
SOIL_DARK = (0x1F, 0x0F, 0x0B)
SOIL_LIGHT = (0x3C, 0x1F, 0x15)
CRUST_LIGHT = (0x6E, 0x3A, 0x22)
RIM = (0xB8, 0x6A, 0x38)
WOOD = (0x9A, 0x5F, 0x2E)
WOOD_LIGHT = (0xC9, 0x8A, 0x48)
WOOD_DARK = (0x5A, 0x31, 0x18)
ROPE = (0xD8, 0xC0, 0x8A)


def tint(c, k):
    return tuple(min(255, int(v * k)) for v in c)


def base_soil(rng):
    """Benekli koyu toprak: düz renk gibi durmasın, ama öne çıkıp oyunu da boğmasın."""
    img = Image.new("RGB", (WIDTH, TILE_ROWS), SOIL)
    px = img.load()
    for y in range(TILE_ROWS):
        for x in range(WIDTH):
            r = rng.random()
            if r < 0.10:
                px[x, y] = SOIL_DARK
            elif r < 0.17:
                px[x, y] = SOIL_LIGHT
    return img


def wave(x, phase, amplitude):
    """800 pikselde kendini tekrar eden dalga: dikişsiz tekrar için tam katlı frekanslar."""
    t = 2 * math.pi * x / WIDTH
    return amplitude * (math.sin(3 * t + phase) + 0.5 * math.sin(7 * t + phase * 2.1))


def add_strata(img, source, rng):
    """Üstteki kırmızı katmanların koyulaşmış, dalgalı devamı."""
    px = img.load()
    band_h = RED_BAND[1] - RED_BAND[0]
    for center, thickness, shade in ((58, 20, 0.62), (176, 15, 0.5)):
        phase = rng.random() * 6.28
        for x in range(WIDTH):
            mid = center + wave(x, phase, 7)
            # Üst ve alt kenar pürüzlü olsun, cetvelle çizilmiş gibi durmasın.
            top = mid - thickness / 2 + rng.randint(-1, 1)
            bottom = mid + thickness / 2 + rng.randint(-1, 1)
            for y in range(int(top), int(bottom)):
                sy = RED_BAND[0] + int((y - top) / max(1, bottom - top) * band_h)
                px[x % WIDTH, y % TILE_ROWS] = tint(source.getpixel((x, min(sy, RED_BAND[1] - 1))), shade)


def stamp(img, source, rng, cx, cy, w, h, shade, wrap_y=True):
    """Taş katmanından düzensiz kenarlı bir parça alıp yapıştırır (yatayda sarar)."""
    sx = rng.randint(0, WIDTH - w - 1)
    sy = rng.randint(ROCK_BAND[0], ROCK_BAND[1] - h)
    px = img.load()
    rows = img.height
    bumps = [rng.uniform(0.72, 1.0) for _ in range(12)]
    for dy in range(h):
        for dx in range(w):
            nx, ny = (dx - w / 2) / (w / 2), (dy - h / 2) / (h / 2)
            angle = (math.atan2(ny, nx) + math.pi) / (2 * math.pi)
            if nx * nx + ny * ny > bumps[int(angle * 11.99)] ** 2:
                continue
            x, y = (cx + dx) % WIDTH, cy + dy
            if wrap_y:
                y %= rows
            elif not 0 <= y < rows:
                continue
            px[x, y] = tint(source.getpixel((sx + dx, sy + dy)), shade)


def deep_tile(source, rng):
    img = base_soil(rng)
    add_strata(img, source, rng)
    for _ in range(95):
        w, h = rng.randint(22, 62), rng.randint(14, 34)
        stamp(img, source, rng, rng.randint(0, WIDTH - 1), rng.randint(0, TILE_ROWS - 1), w, h, rng.uniform(0.66, 0.86))
    return img


def full_ground(source, tile, rng):
    """Üst 128 satır aynen kalır; alt kısım derin dokudur, sınır çizgisi taşlarla örtülür."""
    img = Image.new("RGB", (WIDTH, PAINTED_ROWS + TILE_ROWS))
    img.paste(source.crop((0, 0, WIDTH, PAINTED_ROWS)), (0, 0))
    img.paste(tile, (0, PAINTED_ROWS))
    px = img.load()
    # Ani geçiş olmasın: sınırın hemen altında üst dokunun koyulaşan, pürüzlü devamı.
    for x in range(WIDTH):
        reach = 8 + int(wave(x, 1.3, 3)) + rng.randint(0, 3)
        for d in range(reach):
            px[x, PAINTED_ROWS + d] = tint(source.getpixel((x, PAINTED_ROWS - 1 - d)), 0.85)
    for _ in range(46):
        w, h = rng.randint(20, 50), rng.randint(12, 24)
        stamp(img, source, rng, rng.randint(0, WIDTH - 1), PAINTED_ROWS - h // 2 + rng.randint(-2, 8), w, h,
              rng.uniform(0.72, 0.9), wrap_y=False)
    return img


def cliff_profile(rows, rng, low, high):
    """Dikeyde tekrar eden pürüzlü çıkıntı derinliği: her satır için piksel."""
    points = sorted(rng.sample(range(rows), rows // 11))
    depths = [rng.randint(low, high) for _ in points]
    out = []
    for y in range(rows):
        for i, p in enumerate(points):
            nxt = points[(i + 1) % len(points)]
            span = (nxt - p) % rows or rows
            if (y - p) % rows < span:
                t = ((y - p) % rows) / span
                out.append(round(depths[i] + (depths[(i + 1) % len(depths)] - depths[i]) * t))
                break
    return out


CLIFF_W, CLIFF_FACE = 48, 12  # yamaç görseli genişliği, duvar yüzeyinin soldan uzaklığı


def paint_cliff(img, source, rng, row_offset, profile, inner):
    """Kaya sütunu: dışa taşan pürüzlü siluet, içeride taş dokusu, zemine bakan yanda koyu gölge."""
    px = img.load()
    band = ROCK_BAND[1] - ROCK_BAND[0]
    origin = rng.randint(0, WIDTH - CLIFF_W - 1)
    for i, out in enumerate(profile):
        y = i + row_offset
        edge = CLIFF_FACE - out
        for x in range(max(0, edge), min(CLIFF_W, inner[i])):
            # Git-gel okuma: görsel dikeyde tekrar ederken dikiş görünmez.
            fold = (i * 2) % (2 * band)
            sy = ROCK_BAND[0] + (fold if fold < band else 2 * band - 1 - fold)
            d, back = x - edge, inner[i] - 1 - x
            if d == 0:
                c = CRUST_LIGHT
            elif back == 0:
                c = SOIL_DARK
            else:
                c = tint(source.getpixel((origin + x, sy)), 0.66 if d < 3 or back < 3 else 0.92)
            px[x, y] = (*c, 255)


def cliff_face(source, rng):
    img = Image.new("RGBA", (CLIFF_W, 128), (0, 0, 0, 0))
    paint_cliff(img, source, rng, 0, cliff_profile(128, rng, 1, 10), [CLIFF_FACE + v for v in cliff_profile(128, rng, 16, 34)])
    return img


def cliff_top(source, rng):
    """Yamacın tepesi: keskin köşe yerine kademeli inen omuz. Yürünen yüzeyin üstüne taşmaz."""
    img = Image.new("RGBA", (CLIFF_W, 24), (0, 0, 0, 0))
    profile = [min(i // 2, 3 + i // 3, 9) for i in range(24)]
    paint_cliff(img, source, rng, 0, profile, [CLIFF_FACE + 10 + i for i in range(24)])
    px = img.load()
    for x in range(CLIFF_W):  # üst sıra: zeminin açık kenar rengi
        if px[x, 0][3]:
            px[x, 0] = (*RIM, 255)
    return img


def rubble(source, rng):
    """Duvar dibine yığılmış taşlar: yamaç ile alt zemin arasındaki keskin birleşimi örter."""
    w, h = 112, 26
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    # Sağ uç duvara dayalı, sola doğru alçalan yığın.
    stones = []
    for _ in range(52):
        t = rng.random() ** 1.5
        cx = w - 4 - t * (w - 14)
        peak = (1 - t) ** 1.2 * (h - 8) + 4
        stones.append((cx, h - rng.uniform(0, peak), rng.uniform(2.5, 7 - 3 * t), rng.uniform(1.8, 5 - 2 * t), rng.uniform(0.75, 1.1)))
    stones.sort(key=lambda s: s[1])  # önce arkadakiler
    for cx, cy, rx, ry, k in stones:
        for y in range(int(cy - ry), min(h, int(cy + ry) + 1)):
            for x in range(int(cx - rx), min(w, int(cx + rx) + 1)):
                if x < 0 or y < 0:
                    continue
                nx, ny = (x - cx) / rx, (y - cy) / ry
                r = nx * nx + ny * ny
                if r > 1:
                    continue
                if ny < -0.45 and r > 0.45:
                    c = RIM if nx < 0 else CRUST_LIGHT  # ışık soldan üstten
                elif ny > 0.5 or r > 0.8:
                    c = SOIL
                else:
                    c = CRUST_LIGHT if (x * 3 + y * 5) % 11 == 0 else (0x4E, 0x29, 0x1A)
                px[x, y] = (*tint(c, k), 255)
    return img


def ladder():
    """Dikey tekrar eden ahşap merdiven (32x32): iki direk, iki basamak, ip bağlı."""
    w, h = 32, 32
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()

    def rung(y):
        for x in range(4, 28):
            knot = (x * 7 + y * 3) % 9 == 0
            px[x, y] = (*(WOOD if knot else WOOD_LIGHT), 255)
            px[x, y + 1] = (*WOOD, 255)
            px[x, y + 2] = (*WOOD_DARK, 255)

    def rail(x0):
        for y in range(h):
            grain = (y * 5 + x0) % 13 == 0
            px[x0, y] = (*WOOD_LIGHT, 255)
            px[x0 + 1, y] = (*(WOOD_DARK if grain else WOOD), 255)
            px[x0 + 2, y] = (*WOOD, 255)
            px[x0 + 3, y] = (*WOOD_DARK, 255)

    rung(6)
    rung(22)
    rail(3)
    rail(25)
    for ry in (6, 22):  # basamak-direk bağları
        for x0 in (3, 25):
            for x in range(x0 - 1, x0 + 5):
                px[x, ry] = (*ROPE, 255)
                px[x, ry + 2] = (*tint(ROPE, 0.7), 255)
    return img


def main():
    rng = random.Random(1923)
    source = Image.open(GROUND).convert("RGB")
    tile = deep_tile(source, rng)
    full_ground(source, tile, rng).save(GROUND)
    tile.save(GROUND_DIR / "zemin_derin.png")
    cliff_face(source, rng).save(LAYER_DIR / "zemin_yamac.png")
    cliff_top(source, rng).save(LAYER_DIR / "zemin_yamac_ust.png")
    rubble(source, rng).save(LAYER_DIR / "zemin_yamac_dip.png")
    ladder().save(LAYER_DIR / "merdiven.png")
    print("tamam")


if __name__ == "__main__":
    main()
