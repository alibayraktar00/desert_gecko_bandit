"""
Gemini'den gelen gayzer, lav ve fırtına animasyon setlerini (Assets/Sprites/_Gelen/gece/*.webp) oyuna hazırlar:
hücreleri magenta boşluklarından ayırır, magentayı şeffaf yapar, 2x büyütülmüş görseli asıl piksel boyutuna indirir,
sıkıştırma gürültüsünü paletle temizler ve parçaları oyundaki yerine göre keser.

Çıktılar:
  Katman/gece_gayzer_akis_0..3.png   sütunun dikeyde dikişsiz orta parçası (Tiled)
  Props/gece_gayzer_tepe_0..3.png    sütunun tepesindeki fışkırma (alt orta pivot)
  Props/gece_gayzer_kabarma_0..2.png fışkırma öncesi kabaran su (küçük, orta, dağılan)
  Props/gece_gayzer_kopuk_0..7.png   dipteki köpük
  Katman/gece_lav_yuzey_0..7.png     kaynar yüzey şeridi (yatay Tiled)
  Props/gece_lav_fiskirma_0..7.png   lav fışkırması
  Katman/gece_firtina_on_0..7.png    fırtına duvarının ön yüzü (dikeyde dikişsiz)
  Props/gece_hortum_0..6.png         hortum (kullanılmıyor)
  Props/gece_firtina_bulut_a_0..5.png fırtınanın kıvrılan bulut sırası (yatay setin 1. panelinden)
  Props/gece_firtina_bulut_b_0..5.png ikinci bulut sırası (yatay setin 6. panelinden)

Kullanım (proje kökünden): python tools/gece_setleri_isle.py [--preview onizleme.png]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "Assets" / "Sprites" / "_Gelen" / "gece"
PROPS = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
LAYER = ROOT / "Assets" / "Sprites" / "Environment" / "Katman"
SCALE = 2  # Gemini çıktısı asıl piksellerin 2 katı


def load(name):
    """RGBA; magenta ve ona yakın her şey şeffaf."""
    rgb = np.array(Image.open(RAW / name).convert("RGB")).astype(int)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    magenta = (r > 150) & (b > 150) & (g < 120) & (r - g > 80) & (b - g > 80)
    alpha = np.where(magenta, 0, 255).astype(np.uint8)
    return np.dstack([rgb.astype(np.uint8), alpha])


def bands(mask, axis, min_size):
    """mask'ın (True = içerik) axis boyunca art arda içerik olan aralıkları."""
    line = mask.any(axis=axis)
    out, start = [], None
    for i, v in enumerate(line):
        if v and start is None:
            start = i
        if not v and start is not None:
            out.append((start, i))
            start = None
    if start is not None:
        out.append((start, len(line)))
    return [b for b in out if b[1] - b[0] >= min_size]


def cells(image, y0, y1, min_width=20, gap_ratio=0.97):
    """Bir satır bandındaki hücreler: sütunun neredeyse tamamı şeffafsa boşluktur."""
    strip = image[y0:y1, :, 3] > 0
    transparent = 1 - strip.mean(axis=0)
    content = transparent < gap_ratio
    out, start = [], None
    for x, v in enumerate(content):
        if v and start is None:
            start = x
        if not v and start is not None:
            out.append((start, x))
            start = None
    if start is not None:
        out.append((start, len(content)))
    return [(x0, x1) for x0, x1 in out if x1 - x0 >= min_width]


def downscale(cell):
    """2x'ten asıl boyuta: saydamlık ağırlıklı ortalama, kenarda yarıdan az dolu piksel atılır."""
    h, w = cell.shape[:2]
    h, w = h // SCALE * SCALE, w // SCALE * SCALE
    c = cell[:h, :w].astype(float)
    a = c[..., 3:4] / 255.0
    pre = c[..., :3] * a
    pre = pre.reshape(h // SCALE, SCALE, w // SCALE, SCALE, 3).mean(axis=(1, 3))
    a = a.reshape(h // SCALE, SCALE, w // SCALE, SCALE, 1).mean(axis=(1, 3))
    rgb = np.where(a > 0, pre / np.maximum(a, 1e-6), 0)
    alpha = np.where(a[..., 0] >= 0.5, 255, 0).astype(np.uint8)
    out = np.dstack([rgb.clip(0, 255).astype(np.uint8), alpha])
    # Kenarda magentayla karışıp pembeleşen pikseller: görselin kendisinde pembe yok, şeffaf yap.
    r, g, b = out[..., 0].astype(int), out[..., 1].astype(int), out[..., 2].astype(int)
    pink = (r > 105) & (b > 115) & (g < np.minimum(r, b) - 25)
    out[pink] = 0
    return out


def quantize(image, colors):
    """Sıkıştırma gürültüsünü siler: görünen pikselleri az sayıda renge indirir."""
    visible = image[..., 3] > 0
    if not visible.any():
        return image
    pil = Image.fromarray(image[..., :3], "RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    rgb = np.array(pil.convert("RGB"))
    out = image.copy()
    out[..., :3] = np.where(visible[..., None], rgb, 0)
    return out


def trim(image):
    ys, xs = np.nonzero(image[..., 3])
    return image[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def seamless_vertical(image, blend):
    """Üst ve alt kenarı birbirine çapraz geçirir: Tiled modda dikiş görünmez."""
    out = image.astype(float)
    h = image.shape[0]
    for i in range(blend):
        t = (i + 1) / (blend + 1)
        top, bottom = image[i].astype(float), image[h - blend + i].astype(float)
        out[i] = top * t + bottom * (1 - t)
        out[h - blend + i] = bottom * t + top * (1 - t)
    out = out.round().astype(np.uint8)
    out[..., 3] = np.where(out[..., 3] >= 128, 255, 0)
    return out


def save(image, path):
    Image.fromarray(image, "RGBA").save(path)


def process_geyser(preview):
    image = load("gayzer_seti.webp")
    rows = bands(image[..., 3] > 0, axis=1, min_size=60)
    column_band, foam_band = rows[-2], rows[-1]
    columns = [downscale(image[column_band[0]:column_band[1], x0:x1]) for x0, x1 in cells(image, *column_band)]
    foams = [downscale(image[foam_band[0]:foam_band[1], x0:x1]) for x0, x1 in cells(image, *foam_band)]
    assert len(columns) == 8 and len(foams) == 8, (len(columns), len(foams))

    # Tam sütunlar (3..6): üst 26 piksel tepe, altındaki düz akış orta parça.
    cap = 26
    for i, cell in enumerate(columns[3:7]):
        cell = quantize(cell, 10)
        body = trim(cell)
        flow = body[cap:-4]
        # Orta parça: kenar damlalarını at, genişliği sabitle; dikişsiz tekrar etsin.
        flow = seamless_vertical(flow, 24)
        save(flow, LAYER / f"gece_gayzer_akis_{i}.png")
        preview.append(flow)
    # Tepe: kabaran sütunların fokurdayan uçları. Akışın genişliğine oyunda ölçeklenir.
    for i, cell in enumerate((columns[0], columns[1], columns[2], columns[7])):
        body = trim(quantize(cell, 10))
        top = body[:30]
        save(top, PROPS / f"gece_gayzer_tepe_{i}.png")
        preview.append(top)
    # Kabarma: küçük (0), orta (1), dağılan (7)
    for i, cell in zip(range(3), (columns[0], columns[1], columns[7])):
        cell = trim(quantize(cell, 10))
        save(cell, PROPS / f"gece_gayzer_kabarma_{i}.png")
        preview.append(cell)
    for i, cell in enumerate(foams):
        cell = trim(quantize(cell, 8))
        save(cell, PROPS / f"gece_gayzer_kopuk_{i}.png")
        preview.append(cell)


def process_lava(preview):
    image = load("lav_seti.webp")
    rows = bands(image[..., 3] > 0, axis=1, min_size=60)
    surface_band, spurt_band = rows[-2], rows[-1]
    surfaces = [downscale(image[surface_band[0]:surface_band[1], x0:x1]) for x0, x1 in cells(image, *surface_band, gap_ratio=0.9)]
    spurts = [downscale(image[spurt_band[0]:spurt_band[1], x0:x1]) for x0, x1 in cells(image, *spurt_band, gap_ratio=0.9)]
    assert len(surfaces) == 8 and len(spurts) == 8, (len(surfaces), len(spurts))

    width = min(s.shape[1] for s in surfaces) - 2
    height = min(s.shape[0] for s in surfaces)
    for i, cell in enumerate(surfaces):
        cell = quantize(cell[:height, 1:1 + width], 12)
        # Hücreyi ayıran koyu çizgi kenarlarda kalmış olabilir: en dış sütunları içeriden kopyala.
        cell[:, 0] = cell[:, 1]
        cell[:, -1] = cell[:, -2]
        save(cell, LAYER / f"gece_lav_yuzey_{i}.png")
        preview.append(cell)
    for i, cell in enumerate(spurts):
        cell = quantize(cell, 12)
        # Alttaki lav şeridi fışkırmaya ait değil: yüzeyin üstünden itibaren kes.
        cell = cell[:-height + 6]
        cell = trim(cell) if cell[..., 3].any() else cell
        save(cell, PROPS / f"gece_lav_fiskirma_{i}.png")
        preview.append(cell)


def process_storm(preview):
    image = load("firtina_seti.webp")
    rows = bands(image[..., 3] > 0, axis=1, min_size=60)
    wall_band, tornado_band = rows[-2], rows[-1]
    walls = [downscale(image[wall_band[0]:wall_band[1], x0:x1]) for x0, x1 in cells(image, *wall_band)]
    tornados = [downscale(image[tornado_band[0]:tornado_band[1], x0:x1]) for x0, x1 in cells(image, *tornado_band)]
    assert len(walls) == 8 and len(tornados) == 7, (len(walls), len(tornados))

    height = min(w.shape[0] for w in walls)
    for i, cell in enumerate(walls):
        cell = quantize(cell[:height], 14)
        # Sol kenar fırtınanın içine bakar: tamamen dolu olsun, boşluk kalmasın.
        cell[:, :3, 3] = 255
        cell[:, :3, :3] = np.where(cell[:, :3, :3].sum(axis=2, keepdims=True) == 0, cell[:, 3:4, :3], cell[:, :3, :3])
        cell = seamless_vertical(cell, 10)
        save(cell, LAYER / f"gece_firtina_on_{i}.png")
        preview.append(cell)
    for i, cell in enumerate(tornados):
        cell = trim(quantize(cell, 12))
        save(cell, PROPS / f"gece_hortum_{i}.png")
        preview.append(cell)


def wavy(image, phase, amount):
    """Her satırı yatayda sinüsle kaydırır: aynı bulut kareden kareye kıvrılır."""
    out = np.zeros_like(image)
    h = image.shape[0]
    for y in range(h):
        shift = int(round(np.sin(y / h * np.pi * 2.2 + phase) * amount * (1 - y / h * 0.5)))
        out[y] = np.roll(image[y], shift, axis=0)
        if shift > 0:
            out[y, :shift] = image[y, :1]  # sol kenar fırtınanın içine bakar, açık kalmasın
        elif shift < 0:
            out[y, shift:] = 0  # sola kayınca dolu sol kenar sağ uca sarıyordu: önde dikey şerit kalmasın
    return out


def ragged_front(cell, seed):
    """Panelden kesilen sağ uç dümdüz bir dikey çizgiydi: her satırın ucunu dalgalı ve
    kırık kırık bitirir, son pikseller seyrekleşir. Fırtınanın önü kesik değil savrulan toz gibi görünür."""
    out = cell.copy()
    h, w = out.shape[:2]
    rng = np.random.default_rng(seed)
    for y in range(h):
        # İki dalganın toplamı: büyük kıvrım + küçük tırtıl; uç 8-30 piksel içeride biter.
        wave = 0.5 + 0.3 * np.sin(y / h * np.pi * 2.6 + seed) + 0.2 * np.sin(y / h * np.pi * 9 + seed * 0.7)
        limit = w - 8 - int(wave * 22) - int(rng.integers(0, 3))
        out[y, limit:] = 0
        tail = np.arange(max(0, limit - 6), limit)
        thin = rng.random(len(tail)) < (tail - (limit - 6)) / 7  # uca yaklaştıkça seyrek
        out[y, tail[thin]] = 0
    xs = np.nonzero(out[..., 3].any(axis=0))[0]
    return out[:, :xs.max() + 1]


def process_wide_storm(preview):
    """Yatay fırtına setinden iki panelin yoğun sol yarısı: sol kenarı dolu, sağa doğru incelen toz bulutu."""
    image = load("firtina_yatay_2.webp")
    panels = {"a": (112, 250, 34, 504), "b": (402, 540, 520, 990)}
    for key, (y0, y1, x0, x1) in panels.items():
        cell = quantize(downscale(image[y0:y1, x0:x1]), 16)
        width = int(cell.shape[1] * 0.62)
        cell = cell[:, 2:width]  # panelin sol çerçeve çizgisi kalmasın
        r, g, b = (cell[..., k].astype(int) for k in range(3))
        cell[(r > 80) & (b > 70) & (g < np.minimum(r, b) - 25)] = 0  # magentayla karışmış kenar pikselleri
        # Alt kenar düz kesik görünmesin: son satırlar seyrekleşerek biter.
        h = cell.shape[0]
        rng = np.random.default_rng(5)
        for k in range(6):
            row = h - 1 - k
            cell[row][rng.random(cell.shape[1]) < (6 - k) / 7] = 0
        ys = np.nonzero(cell[..., 3].any(axis=1))[0]
        cell = cell[ys.min():ys.max() + 1]
        cell = ragged_front(cell, seed={"a": 11, "b": 23}[key])
        for frame in range(6):
            out = wavy(cell, frame / 6 * np.pi * 2, 2.5)
            save(out, PROPS / f"gece_firtina_bulut_{key}_{frame}.png")
            if frame < 2:
                preview.append(out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    preview = []
    process_geyser(preview)
    process_lava(preview)
    process_storm(preview)
    process_wide_storm(preview)
    if args.preview:
        zoom = 2
        w = sum(p.shape[1] * zoom + 6 for p in preview)
        h = max(p.shape[0] for p in preview) * zoom
        sheet = Image.new("RGBA", (min(w, 2400), h * ((w // 2400) + 1)), (60, 50, 70, 255))
        x = y = 0
        for p in preview:
            tile = Image.fromarray(p, "RGBA")
            tile = tile.resize((tile.width * zoom, tile.height * zoom), Image.NEAREST)
            if x + tile.width > sheet.width:
                x, y = 0, y + h
            sheet.alpha_composite(tile, (x, y))
            x += tile.width + 6
        sheet.save(args.preview)
    print("gece setleri hazır")


if __name__ == "__main__":
    main()
