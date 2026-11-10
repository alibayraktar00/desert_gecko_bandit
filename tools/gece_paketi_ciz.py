"""
Level 3 (Ayaz Gecesi) için eksik görselleri çizer: kaynayan çamur, gayzer sütunu, fırtına duvarı, kanca feneri,
kor sepeti, termometre simgesi. Fener mevcut mgr_fener görselinden kesilir; diğerleri oyunun paletiyle piksel piksel üretilir.

Çıktılar sabit tohumludur, her çalıştırmada aynı görsel çıkar.

Kullanım (proje kökünden):
  python tools/gece_paketi_ciz.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
PROPS = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
LAYER = ROOT / "Assets" / "Sprites" / "Environment" / "Katman"
OUTLINE = (31, 15, 11, 255)


def wrap_noise(width, height, cells_x, cells_y, seed):
    """Yan yana ve alt alta dikişsiz tekrar eden yumuşak gürültü (0-1)."""
    grid = np.random.default_rng(seed).random((cells_y, cells_x))
    ys, xs = np.mgrid[0:height, 0:width]
    gx, gy = xs / width * cells_x, ys / height * cells_y
    x0, y0 = gx.astype(int), gy.astype(int)
    fx, fy = gx - x0, gy - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    x1, y1 = (x0 + 1) % cells_x, (y0 + 1) % cells_y
    x0, y0 = x0 % cells_x, y0 % cells_y
    return (grid[y0, x0] * (1 - fx) + grid[y0, x1] * fx) * (1 - fy) + (grid[y1, x0] * (1 - fx) + grid[y1, x1] * fx) * fy


def layered_noise(width, height, seed, base=(4, 4)):
    out = np.zeros((height, width))
    weight, total = 1.0, 0.0
    for octave in range(3):
        out += wrap_noise(width, height, base[0] << octave, base[1] << octave, seed + octave) * weight
        total += weight
        weight *= 0.5
    return out / total


def palette(value, colors):
    """0-1 değeri sıralı renk basamaklarına çevirir: yumuşak geçiş yerine piksel sanat bantları."""
    index = np.clip((value * len(colors)).astype(int), 0, len(colors) - 1)
    return np.array(colors, dtype=np.uint8)[index]


def mud_tile():
    """Kaynayan çamur: kabuklu koyu yüzey, çatlaklarından kor sızar. Üst kenarı parlak, 64x64 dikişsiz."""
    size = 64
    noise = layered_noise(size, size, 11, (5, 5))
    cracks = 1 - np.abs(layered_noise(size, size, 29, (4, 4)) - 0.5) * 2
    rgb = palette(np.clip((noise - 0.3) / 0.4, 0, 0.999), [(38, 18, 14), (58, 27, 18), (80, 38, 22), (104, 52, 26)])
    image = np.dstack([rgb, np.full((size, size), 255, dtype=np.uint8)])
    glow = cracks > 0.93
    image[glow] = (240, 120, 40, 255)
    image[cracks > 0.975] = (255, 196, 92, 255)
    Image.fromarray(image, "RGBA").save(LAYER / "gece_camur.png")

    # Yüzey şeridi: dalgalı parlak köpük. Çamurun tepesine ayrı tekrar eder.
    width, height = 64, 10
    strip = np.zeros((height, width, 4), dtype=np.uint8)
    waves = (np.sin(np.arange(width) * 2 * np.pi / width * 3) + np.sin(np.arange(width) * 2 * np.pi / width * 7 + 1)) * 0.9 + 2.5
    for x in range(width):
        top = int(round(waves[x]))
        strip[top:, x] = (104, 52, 26, 255)
        strip[top:top + 1, x] = (255, 196, 92, 255)
        strip[top + 1:top + 3, x] = (240, 120, 40, 255)
    Image.fromarray(strip, "RGBA").save(LAYER / "gece_camur_ust.png")


FRAMES = 4
WATER = [(26, 70, 92), (52, 128, 150), (104, 190, 208), (170, 228, 238), (226, 248, 252), (255, 255, 255)]


def geyser_tile():
    """
    Gayzer sütunu: kenarı koyu konturlu su, ortası köpüklü beyaz akış, yanlarından kopan damlalar.
    64x64 (2 birim), dikeyde dikişsiz. 4 kare: desen her karede yukarı kayar, oyunda akıyor görünür.
    """
    width, height = 64, 64
    noise = layered_noise(width, height, 41, (4, 4))
    streaks = wrap_noise(width, height, 16, 2, 53)
    fine = wrap_noise(width, height, 24, 6, 61)
    drops = np.random.default_rng(67).random((height, width))
    center = np.abs(np.arange(width) - (width - 1) / 2) / (width / 2)
    for frame in range(FRAMES):
        shift = -frame * (height // FRAMES)
        n, s, f, d = (np.roll(x, shift, axis=0) for x in (noise, streaks, fine, drops))
        edge = np.tile(center, (height, 1)) + (n - 0.5) * 0.32 + (f - 0.5) * 0.12
        # Ortadan kenara: beyaz köpük, açık su, koyu su, kontur.
        light = np.clip(1.0 - edge * 0.9 + (s - 0.5) * 0.7 + (f - 0.5) * 0.25, 0, 0.999)
        rgb = palette(light, WATER)
        alpha = np.where(edge < 0.86, 255, 0).astype(np.uint8)
        rim = (edge >= 0.8) & (edge < 0.86)
        rgb[rim] = WATER[0]
        # Kenardan kopan damlacıklar
        spray = (edge >= 0.86) & (edge < 1.05) & (d > 0.93)
        rgb[spray] = WATER[3]
        alpha[spray] = 255
        image = np.dstack([rgb, alpha])
        Image.fromarray(image, "RGBA").save(LAYER / f"gece_gayzer_{frame}.png")


def geyser_crown():
    """Sütunun tepesindeki fışkırma: yay çizerek düşen damlalar ve köpük başlığı. 72x44, alt orta pivotlu, 4 kare."""
    width, height = 72, 44
    rng = np.random.default_rng(79)
    jets = [(rng.uniform(-1, 1), rng.uniform(0.55, 1.0), rng.uniform(0, 1)) for _ in range(46)]
    for frame in range(FRAMES):
        image = np.zeros((height, width, 4), dtype=np.uint8)

        def put(x, y, color):
            x, y = int(round(x)), int(round(y))
            if 0 <= x < width and 0 <= y < height:
                image[y, x] = (*color, 255)

        # Damlalar: merkezden çıkıp parabolle iki yana düşer; her damla kendi zamanında.
        for side, power, offset in jets:
            t = (offset + frame / FRAMES) % 1.0
            x = width / 2 + side * 30 * t
            y = height - 10 - power * 34 * t + 30 * t * t
            size = 2 if t < 0.5 else 1
            color = WATER[4] if t < 0.4 else WATER[3] if t < 0.75 else WATER[2]
            for dx in range(size):
                for dy in range(size):
                    put(x + dx, y + dy, color)
        # Köpük başlığı: sütunun ağzında kabaran yarım bulut.
        for x in range(width):
            dx = (x - width / 2) / 17
            if abs(dx) >= 1:
                continue
            wobble = np.sin(x * 0.9 + frame * 1.7) * 1.2
            top = int(height - 10 - (1 - dx * dx) ** 0.5 * 8 + wobble)
            for y in range(max(0, top), height):
                depth = y - top
                color = WATER[0] if depth == 0 else WATER[5] if depth < 3 else WATER[4] if depth < 6 else WATER[3]
                image[y, x] = (*color, 255)
        Image.fromarray(image, "RGBA").save(PROPS / f"gece_gayzer_tepe_{frame}.png")


def geyser_vent():
    """Gayzer ağzı: mineral kabuklu taş halka, ortasında buharlı koyu delik. 64x20, alt orta pivotlu."""
    width, height = 64, 20
    rng = np.random.default_rng(83)
    image = np.zeros((height, width, 4), dtype=np.uint8)
    rock = [(58, 40, 36), (84, 60, 50), (112, 84, 66), (146, 114, 88)]
    crust = [(150, 198, 196), (196, 228, 222), (236, 246, 240)]
    noise = layered_noise(width, height, 89, (4, 2))
    for y in range(height):
        for x in range(width):
            dx = (x - width / 2 + 0.5) / (width / 2)
            hump = (1 - dx * dx) ** 0.5 if abs(dx) < 1 else 0
            top = height - 2 - hump * 15 + (noise[y, x] - 0.5) * 4
            if y < top:
                continue
            hole = abs(dx) < 0.32 and y < height - 6
            if hole:
                image[y, x] = (20, 26, 34, 255)
                continue
            depth = y - top
            if depth < 1:
                image[y, x] = OUTLINE
            elif depth < 3 and noise[y, x] > 0.42:
                image[y, x] = (*crust[min(2, int(noise[y, x] * 3))], 255)
            else:
                image[y, x] = (*rock[min(3, int((1 - depth / 16) * 4 * noise[y, x] + 1))], 255)
    # Deliğin kenarına kontur
    for y in range(height):
        for x in range(1, width - 1):
            if image[y, x, 3] and tuple(image[y, x, :3]) != (20, 26, 34):
                if tuple(image[y, x - 1, :3]) == (20, 26, 34) or tuple(image[y, x + 1, :3]) == (20, 26, 34):
                    image[y, x] = OUTLINE
    # Dibe taşan sıcak su birikintisi
    for x in range(8, width - 8):
        if rng.random() < 0.8:
            image[height - 1, x] = (*WATER[2], 255)
    Image.fromarray(image, "RGBA").save(PROPS / "gece_gayzer_agiz.png")


def storm_tile():
    """Fırtınanın gövdesi: koyu kum ve buz bulutu. 256x256, her yönde dikişsiz."""
    size = 256
    noise = layered_noise(size, size, 71, (6, 6))
    swirl = layered_noise(size, size, 83, (9, 5))
    value = np.clip(noise * 0.7 + swirl * 0.5 - 0.1, 0, 0.999)
    # Gövde koyu ve az karşıtlıklı: tekrar eden desen göze batmasın. Parlak bulut sadece ön yüzde.
    rgb = palette(value, [(42, 40, 90), (48, 52, 98), (54, 62, 104), (60, 71, 112), (70, 80, 120), (80, 90, 128)])  # ön yüzün renkleriyle aynı aile
    alpha = np.full((size, size), 255, dtype=np.uint8)
    Image.fromarray(np.dstack([rgb, alpha]), "RGBA").save(LAYER / "gece_firtina.png")


def storm_front():
    """
    Fırtınanın ön yüzü: koyu kum ve buzdan kıvrılan bulut, sağ kenarı açık renkli yırtık bir kenarla biter ve
    sağa doğru dağılır. 96x128, dikeyde dikişsiz. 4 kare: bulut her karede kayar ve kenar kıpırdar.
    """
    width, height = 96, 128
    noise = layered_noise(width, height, 97, (3, 4))
    swirl = layered_noise(width, height, 113, (6, 8))
    grit = np.random.default_rng(127).random((height, width))
    colors = [(26, 26, 48), (40, 38, 66), (62, 58, 86), (92, 84, 104), (128, 116, 124), (172, 160, 156), (214, 208, 206)]
    along = np.tile(np.arange(width) / (width - 1), (height, 1))
    for frame in range(FRAMES):
        shift = -frame * (height // FRAMES)
        n = np.roll(noise, shift, axis=0)
        w = np.roll(swirl, shift // 2, axis=0)
        g = np.roll(grit, shift, axis=0)
        edge = along + (n - 0.5) * 0.85 + (w - 0.5) * 0.4
        # Kabarık bulutun tepesi aydınlık, içi koyu: kenara yaklaştıkça açılır.
        value = np.clip(n * 0.55 + w * 0.45 + edge * 0.35 - 0.25, 0, 0.999)
        rgb = palette(value, colors)
        alpha = np.where(edge < 0.55, 255, 0).astype(np.uint8)
        rim = (edge >= 0.5) & (edge < 0.55)
        rgb[rim] = colors[6]
        # Kenarın ötesinde savrulan kum taneleri
        dust = (edge >= 0.55) & (edge < 0.95) & (g > 0.9 + (edge - 0.55) * 0.2)
        rgb[dust] = colors[5]
        alpha[dust] = 255
        Image.fromarray(np.dstack([rgb, alpha]), "RGBA").save(LAYER / f"gece_firtina_on_{frame}.png")


def lantern_hook():
    """Kanca feneri: mgr_fener'in fenerini keser, tepesine dilin tutunacağı halka ekler. Alt orta pivotlu."""
    source = np.array(Image.open(PROPS / "mgr_fener.png").convert("RGBA"))
    # Fenerin yeri mgr_fener içinde elle ölçüldü: direğin kolundan sarkan fener.
    x0, x1, y0, y1 = 32, 44, 18, 36
    crop = source[y0:y1, x0:x1].copy()
    # Kaynaktaki köşelerde kalan açık zemin artıklarını sil.
    rgb = crop[..., :3].astype(int)
    tan = (rgb[..., 0] > 140) & (rgb[..., 1] > 120) & (rgb[..., 2] > 80) & (rgb[..., 2] < 140) & (np.abs(rgb[..., 0] - rgb[..., 1]) < 45)
    corner = np.zeros(crop.shape[:2], dtype=bool)
    corner[:4, :3] = corner[:4, -3:] = corner[-3:, :3] = corner[-3:, -3:] = True
    crop[tan & corner] = 0
    height, width = crop.shape[:2]
    canvas = np.zeros((height + 8, 21, 4), dtype=np.uint8)
    offset = (21 - width) // 2
    canvas[8:, offset:offset + width] = crop
    cx = 10
    for y, half in ((0, 1), (1, 2), (2, 3), (3, 3), (4, 3), (5, 2), (6, 1)):
        canvas[y, cx - half] = OUTLINE
        canvas[y, cx + half] = OUTLINE
    canvas[0, cx] = OUTLINE
    canvas[6, cx] = OUTLINE
    canvas[7, cx] = OUTLINE
    Image.fromarray(canvas, "RGBA").save(PROPS / "gece_kanca_fener.png")


def brazier():
    """Kor sepeti: arabaya ve taş halkaya konan demir mangal. 24x18, alt orta pivotlu."""
    rows = [
        "      x x x x x x      ",
        "     xOoOoOoOoOoOx     ",
        "    xOYoOYoOYoOYoOx    ",
        "   xxxxxxxxxxxxxxxxx   ",
        "   xddxddxddxddxddxx   ",
        "    xddxddxddxddxdx    ",
        "    xxxxxxxxxxxxxxx    ",
        "     xddxddxddxddx     ",
        "     xxxxxxxxxxxxx     ",
        "        xx   xx        ",
        "       xx     xx       ",
        "      xxx     xxx      ",
    ]
    colors = {"x": OUTLINE, "d": (86, 62, 54, 255), "o": (240, 120, 40, 255), "O": (190, 70, 30, 255), "Y": (255, 196, 92, 255)}
    width = max(len(r) for r in rows)
    canvas = np.zeros((len(rows), width, 4), dtype=np.uint8)
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c in colors:
                canvas[y, x] = colors[c]
    Image.fromarray(canvas, "RGBA").save(PROPS / "gece_mangal.png")


def thermometer_icon():
    """Isı çubuğunun yanındaki küçük alev simgesi. 12x12."""
    rows = [
        "     xx     ",
        "    xOx     ",
        "    xOOx    ",
        "   xOOOx    ",
        "  xOOYOOx   ",
        "  xOYYYOOx  ",
        " xOOYWYYOx  ",
        " xOYWWWYOx  ",
        " xOYWWWYOx  ",
        "  xOYWYOx   ",
        "   xOOOx    ",
        "    xxx     ",
    ]
    colors = {"x": OUTLINE, "O": (226, 92, 36, 255), "Y": (255, 180, 70, 255), "W": (255, 236, 170, 255)}
    canvas = np.zeros((12, 12, 4), dtype=np.uint8)
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c in colors:
                canvas[y, x] = colors[c]
    Image.fromarray(canvas, "RGBA").save(PROPS / "gece_alev_simge.png")


def telegraph_pole():
    """Telgraf direği: yuz_direk'in tepesi ve gövdesinden tekrar eden parça; istenen boya uzatılır."""
    source = np.array(Image.open(PROPS / "yuz_direk.png").convert("RGBA"))
    Image.fromarray(source[:30], "RGBA").save(LAYER / "gece_direk_tepe.png")
    rows = source[36:52]
    columns = np.nonzero(rows[..., 3].any(axis=0))[0]
    Image.fromarray(rows[:, columns.min():columns.max() + 1], "RGBA").save(LAYER / "gece_direk_govde.png")


def tall_night_sky():
    """Gece gökyüzü gündüzünden kısa: üstüne ve altına kendi renkleri eklenip gündüzle aynı boya getirilir, gündüz görünmez."""
    folder = ROOT / "Assets" / "Sprites" / "Environment" / "Background"
    source = np.array(Image.open(folder / "bg_gece_gokyuzu.png").convert("RGBA"))
    height = np.array(Image.open(folder / "bg_gunduz_gokyuzu.png")).shape[0]
    extra = height - source.shape[0]
    above = int(extra * 0.62)
    below = extra - above
    rng = np.random.default_rng(7)
    top = np.repeat(source[:1], above, axis=0).copy()
    # Üst kısma seyrek yıldız: düz renk bant gibi durmasın.
    for _ in range(46):
        y, x = rng.integers(0, above), rng.integers(0, source.shape[1])
        top[y, x] = (214, 222, 245, 255) if rng.random() < 0.7 else (150, 160, 205, 255)
    bottom = np.repeat(source[-1:], below, axis=0)
    Image.fromarray(np.vstack([top, source, bottom]), "RGBA").save(folder / "bg_gece_gokyuzu_uzun.png")


def main():
    tall_night_sky()
    telegraph_pole()
    mud_tile()
    geyser_tile()
    geyser_crown()
    geyser_vent()
    storm_tile()
    storm_front()
    lantern_hook()
    brazier()
    thermometer_icon()
    print("gece paketi hazır")


if __name__ == "__main__":
    main()
