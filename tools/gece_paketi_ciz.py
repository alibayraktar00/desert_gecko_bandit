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


def geyser_tile():
    """Gayzer sütunu: dikey akan buhar ve su çizgileri, kenarları yırtık saydam. 64x64 (2 birim), dikeyde dikişsiz."""
    width, height = 64, 64
    noise = layered_noise(width, height, 41, (6, 6))
    streaks = wrap_noise(width, height, 14, 2, 53)
    distance = np.abs(np.arange(width) - (width - 1) / 2) / (width / 2)
    edge = np.tile(distance, (height, 1)) + (noise - 0.5) * 0.55
    rgb = palette(np.clip(noise * 0.5 + streaks * 0.6 - 0.08, 0, 0.999), [(150, 205, 225), (190, 228, 240), (226, 244, 250), (255, 255, 255)])
    alpha = np.where(edge < 0.55, 235, np.where(edge < 0.78, 150, np.where(edge < 0.95, 70, 0))).astype(np.uint8)
    Image.fromarray(np.dstack([rgb, alpha]), "RGBA").save(LAYER / "gece_gayzer.png")


def storm_tile():
    """Fırtınanın gövdesi: koyu kum ve buz bulutu. 256x256, her yönde dikişsiz."""
    size = 256
    noise = layered_noise(size, size, 71, (6, 6))
    swirl = layered_noise(size, size, 83, (9, 5))
    value = np.clip(noise * 0.7 + swirl * 0.5 - 0.1, 0, 0.999)
    # Gövde koyu ve az karşıtlıklı: tekrar eden desen göze batmasın. Parlak bulut sadece ön yüzde.
    rgb = palette(value, [(22, 24, 48), (28, 31, 58), (34, 38, 68), (40, 46, 78), (48, 54, 88), (56, 64, 98)])
    alpha = np.full((size, size), 255, dtype=np.uint8)
    Image.fromarray(np.dstack([rgb, alpha]), "RGBA").save(LAYER / "gece_firtina.png")


def storm_front():
    """Fırtınanın ön yüzü: sağ kenarı yırtık, sağa doğru dağılan kum ve buz bulutu. 96x128, dikeyde dikişsiz."""
    width, height = 96, 128
    noise = layered_noise(width, height, 97, (3, 4))
    swirl = layered_noise(width, height, 113, (6, 8))
    value = np.clip(noise * 0.75 + swirl * 0.45 - 0.1, 0, 0.999)
    rgb = palette(value, [(30, 34, 66), (46, 52, 90), (70, 80, 120), (104, 118, 156), (150, 168, 198), (206, 220, 236)])
    # 0 solda (katı), 1 sağda (bitti). Gürültü kenarı yırtar.
    along = np.tile(np.arange(width) / (width - 1), (height, 1))
    edge = along + (noise - 0.5) * 0.9 + (swirl - 0.5) * 0.35
    alpha = np.where(edge < 0.5, 255, np.where(edge < 0.62, 190, np.where(edge < 0.72, 110, np.where(edge < 0.8, 50, 0)))).astype(np.uint8)
    Image.fromarray(np.dstack([rgb, alpha]), "RGBA").save(LAYER / "gece_firtina_on.png")


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
    storm_tile()
    storm_front()
    lantern_hook()
    brazier()
    thermometer_icon()
    print("gece paketi hazır")


if __name__ == "__main__":
    main()
