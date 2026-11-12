"""
Mağara toprak paketini (Part 4.2, 5, 6.2, 6.3) level parçalarına çevirir. Çıktılar Katman klasörüne gider
(üst kenarından pivotlu, yan yana tekrar eder; içe aktarma ayarları SpriteImportRules'ta):

  mg_toprak.png       yüzeyin altındaki katmanlı toprak (4.2 tile_05, tile_06 ve aynası), bir kez görünür
  mg_derin.png        derin taşlı toprak (4.2 tile_09, tile_10 ve aynaları, kaydırmalı), iki yönde dikişsiz
  mg_kenar.png        kaya kütlesinin dik yüzü (4.2 tile_07), dikeyde dikişsiz; sağa bakar, sol yüz için çevrilir
  mg_sacak.png        tavandan sarkan sarkıtlar ve taş kırıntıları (Part 5), şeffaf, yatayda tekrar eder
  mg_ust.png          pürüzlü yürüme yüzeyi: 6.2 / 6.3 karolarının taşlı üst şeritleri, üstü saydam
  mg_arka.png         mağara arka duvarı: üç arka plan sahnesi, ek yerleri koyu kaya sütunlu; üstü ve altı
                      saydama söner, her odanın tabanına hizalanır (Part 5), 22 PPU, alt kenardan pivotlu
  mg_damar_*.png      toprağın içine gömülü süsler (6.2 / 6.3 karoları): kenarları pürüzlü sönen yamalar

Sahte pikseller (3 px) gerçek ızgaraya indirilir; arka plan yumuşak boyandığı için olduğu gibi kalır.
Kaynaklar: Assets/Sprites/_Gelen/magara_toprak_{42,5,62,63}.png
Kullanım (proje kökünden):  python tools/magara_toprak_paketi_isle.py [--preview onizleme.png]
Gereken paketler: pillow, numpy
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Katman"
PIXEL = 3

# Sayfalardaki karo kutuları (x0, y0, x1, y1), etiket yazıları dışarıda.
TILES_42 = {
    "top_flat": (24, 115, 282, 272), "mid_flat_01": (25, 317, 283, 511), "mid_flat_02": (306, 317, 552, 512),
    "edge_L": (575, 318, 780, 511), "bottom_flat_01": (26, 561, 282, 741), "bottom_flat_02": (306, 560, 551, 742),
}
ROW_Y = [(84, 272), (304, 495), (532, 721)]
COL_X = [(42, 259), (283, 500), (525, 742), (767, 983)]

# Gömülü süsler: (sayfa, satır, sütun). Üst şerit (gökyüzü ve yüzey) kesilir, toprağın içi kalır.
VEINS = {
    "kok": ("62", 0, 0), "kaya": ("62", 0, 1), "kemik": ("62", 0, 3),
    "maden": ("62", 1, 0), "sizinti": ("62", 1, 1),
    "kaya_iri": ("62", 2, 0), "kok_ag": ("62", 2, 1), "hazine": ("62", 2, 2), "yarik": ("62", 2, 3),
    "cevher": ("63", 0, 0), "harabe": ("63", 0, 1), "kok_sik": ("63", 0, 2), "oyuk": ("63", 0, 3),
    "kristal": ("63", 1, 0), "fosil": ("63", 1, 1), "kirik": ("63", 1, 2), "balcik": ("63", 1, 3),
    "maden_eski": ("63", 2, 0), "lav": ("63", 2, 1), "su_cebi": ("63", 2, 2), "mezar": ("63", 2, 3),
}
VEIN_TOP_CUT = 0.24  # karonun üstünden atılan pay (gökyüzü şeridi ve yüzey çizgisi)
FEATHER = 10         # süs kenarının söndüğü genişlik (oyun pikseli)
VEIN_CONTRAST, VEIN_LIFT = 1.15, 1.06  # hafif belirginleştirme; fazlası yamayı sonradan eklenmiş gösteriyor

BACKDROPS = [(525, 319, 998, 509), (28, 562, 996, 740), (26, 318, 499, 510)]  # uzak, ayrıntılı, yakın
STALACTITES = (735, 119, 1005, 232)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def load(sheet):
    return np.array(Image.open(SOURCE_DIR / f"magara_toprak_{sheet}.png").convert("RGB"), dtype=np.float64)


def downscale(rgb, pixel=PIXEL):
    """Her sahte pikselin medyanı: webp bulanıklığı ve kenar karışması atılır."""
    h, w = rgb.shape[0] // pixel, rgb.shape[1] // pixel
    blocks = rgb[:h * pixel, :w * pixel].reshape(h, pixel, w, pixel, 3).transpose(0, 2, 1, 3, 4)
    return np.median(blocks.reshape(h, w, pixel * pixel, 3), axis=2)


def seamless_x(img, blend):
    """Sağ ucu sol uca yumuşakça bindirir: sonuç yan yana dikişsiz tekrar eder (genişlik blend kadar kısalır)."""
    w = img.shape[1]
    out = img[:, :w - blend].copy()
    t = ((np.arange(blend) + 0.5) / blend)[None, :, None]
    out[:, :blend] = img[:, w - blend:] * (1 - t) + img[:, :blend] * t
    return out


def seamless_y(img, blend):
    return seamless_x(img.transpose(1, 0, 2), blend).transpose(1, 0, 2)


def palette_of(images, colors):
    pixels = np.concatenate([i.reshape(-1, 3) for i in images]).astype(np.uint8)
    strip = Image.fromarray(pixels.reshape(1, -1, 3), "RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
    return np.array(strip.getpalette()[:colors * 3], dtype=np.float64).reshape(-1, 3)


def snap(img, palette):
    flat = img.reshape(-1, 3)
    out = np.empty_like(flat)
    for i in range(0, len(flat), 20000):  # bellek: parça parça en yakın renk
        chunk = flat[i:i + 20000]
        out[i:i + 20000] = palette[((chunk[:, None, :] - palette[None]) ** 2).sum(2).argmin(1)]
    return out.reshape(img.shape)


def rgba(rgb, alpha=None):
    a = np.full(rgb.shape[:2], 255, np.uint8) if alpha is None else alpha.astype(np.uint8)
    return np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), a])


def keyed(rgb, bg):
    """Düz gri zemini at; kalanın sahte piksellerini indir."""
    diff = np.abs(rgb - bg).sum(2)
    mask = diff > 36
    small = downscale(rgb)
    h, w = small.shape[:2]
    m = mask[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL).mean((1, 3)) > 0.5
    return small, m


def ragged_alpha(h, w, feather, seed):
    """Kenara yaklaştıkça sönen, Bayer desenle pikselleşmiş saydamlık: süs toprağa gömülü görünür."""
    # Dikdörtgen değil, düzensiz oval bir leke: kenarı iri lekelerle girintili çıkıntılı, toprağa karışır.
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w]
    d = np.sqrt(((x - (w - 1) / 2) / (w / 2)) ** 2 + ((y - (h - 1) / 2) / (h / 2)) ** 2)
    coarse = rng.random((h // 6 + 2, w // 6 + 2))
    noise = np.kron(coarse, np.ones((6, 6)))[:h, :w]
    level = (1 - d) * (min(w, h) / 2) / feather + (noise - 0.5) * 0.8
    # Net kenar (karıncalı yarı saydamlık yerine): piksel sanatta temiz, girintili bir leke.
    return np.where(level > 0.45, 255, 0)


def save(name, image):
    path = OUTPUT_DIR / f"{name}.png"
    Image.fromarray(image, "RGBA").save(path)
    print(f"  {path.relative_to(ROOT)}  ({image.shape[1]}x{image.shape[0]})")
    return image


def build_ground(sheet42):
    def tile(key):
        x0, y0, x1, y1 = TILES_42[key]
        return downscale(sheet42[y0:y1, x0:x1])

    top = tile("top_flat")         # ~86x52: üst 4 satır koyu gökyüzü, sonra topraklı kenar
    mid = tile("mid_flat_01")      # katmanlı toprak
    deep = tile("bottom_flat_01")  # taşlı derin toprak
    edge = tile("edge_L")          # solda toprak, sağında boşluk

    palette = palette_of([top, mid, deep, edge], 40)
    out = {}
    blend = 12

    def join_x(*parts):
        t = ((np.arange(blend) + 0.5) / blend)[None, :, None]
        result = parts[0]
        for p in parts[1:]:
            result = np.concatenate([result[:, :-blend], result[:, -blend:] * (1 - t) + p[:, :blend] * t, p[:, blend:]], axis=1)
        return result

    def join_y(*parts):
        return join_x(*[p.transpose(1, 0, 2) for p in parts]).transpose(1, 0, 2)

    def same_height(*parts):
        # Boy ve en eşitlenir: sıralar kaydırmalı dizilince genişlikleri tutmalı.
        rows_, cols_ = min(p.shape[0] for p in parts), min(p.shape[1] for p in parts)
        return [p[:rows_, :cols_] for p in parts]

    # Katmanlı toprak: iki orta karo ve birinin aynası yan yana (tek karo 2 birimde bir aynı kıvrımı tekrar ediyordu).
    mid2 = tile("mid_flat_02")
    m1, m2 = same_height(mid[6:], mid2[6:])
    body = seamless_x(join_x(m1, m2, m1[:, ::-1]), 14)
    out["mg_toprak"] = rgba(snap(body, palette))

    # Derin toprak: iki alt karonun gövdesi ve aynaları 3x2 kaydırmalı dizilir, iki yönde dikişsiz.
    deep2 = tile("bottom_flat_02")
    cut = deep.shape[0] // 4
    a, b = same_height(deep[cut:], deep2[cut:])
    upper, lower = join_x(a, b, a[:, ::-1]), join_x(b[:, ::-1], a[:, ::-1], b)
    d = seamless_y(seamless_x(join_y(upper, lower), 14), 10)
    out["mg_derin"] = rgba(snap(d, palette))

    # Dik yüz: toprak sütununun sağ kenarı (boşluğun başladığı yer) ile içe doğru 20 piksel.
    gray = np.abs(edge - edge[:, -2:].mean((0, 1))).sum(2) < 30
    face = int(np.argmax(gray[edge.shape[0] // 2]))  # ortadaki satırda boşluğun başladığı sütun
    strip = edge[:, max(0, face - 22):face]
    strip = seamless_y(strip[4:], 10)
    out["mg_kenar"] = rgba(snap(strip, palette))
    return out, palette


def build_stalactites(sheet5):
    x0, y0, x1, y1 = STALACTITES
    region = sheet5[y0:y1, x0:x1]
    small, mask = keyed(region, region[-3:, :].reshape(-1, 3).mean(0))
    rows, cols = np.where(mask.any(1))[0], np.where(mask.any(0))[0]
    small, mask = small[:rows[-1] + 1, cols[0]:cols[-1] + 1], mask[:rows[-1] + 1, cols[0]:cols[-1] + 1]
    # Uçlarda boşluk bırak: yan yana gelince sarkıtlar yapışmasın.
    pad = 6
    small = np.pad(small, ((0, 0), (pad, pad), (0, 0)))
    mask = np.pad(mask, ((0, 0), (pad, pad)))
    return rgba(small, mask * 255)


def build_rim(sheets):
    """
    Pürüzlü yürüme yüzeyi: dört karonun (taşlı ve dalgalı) üst şeridi yan yana, gri gökyüzü saydam.
    Taşlar çarpışma çizgisinin biraz üstüne taşar. Dönen değer: tümüyle dolu ilk satır (yüzey çizgisi).
    """
    sources = [("62", 1), ("62", 0), ("63", 1), ("63", 0)]  # taşlı, dalgalı, taşlı, dalgalı
    y0, y1 = ROW_Y[0][0] + 15, ROW_Y[0][0] + 57
    strips, masks = [], []
    for sheet, col in sources:
        x0, x1 = COL_X[col]
        region = sheets[sheet][y0:y1, x0 + 3:x1 - 3]
        small = downscale(region)
        lo, hi = small.min(2), small.max(2)
        masks.append(~((hi - lo < 9) & (hi < 52)).astype(bool))  # düz koyu gri gökyüzü
        strips.append(small)
    width = min(s.shape[1] for s in strips)
    blend = 10
    t = ((np.arange(blend) + 0.5) / blend)[None, :]
    rgb, alpha = strips[0][:, :width], masks[0][:, :width].astype(np.float64)
    for s, m in zip(strips[1:], masks[1:]):
        s, m = s[:, :width], m[:, :width].astype(np.float64)
        rgb = np.concatenate([rgb[:, :-blend], rgb[:, -blend:] * (1 - t[..., None]) + s[:, :blend] * t[..., None], s[:, blend:]], axis=1)
        alpha = np.concatenate([alpha[:, :-blend], alpha[:, -blend:] * (1 - t) + m[:, :blend] * t, m[:, blend:]], axis=1)
    # Dikişsiz: sağ uç sol uca bindirilir (renk ve saydamlık birlikte).
    w = rgb.shape[1]
    tt = ((np.arange(blend) + 0.5) / blend)[None, :]
    rgb[:, :blend] = rgb[:, w - blend:] * (1 - tt[..., None]) + rgb[:, :blend] * tt[..., None]
    alpha[:, :blend] = alpha[:, w - blend:] * (1 - tt) + alpha[:, :blend] * tt
    rgb, alpha = rgb[:, :w - blend], alpha[:, :w - blend] > 0.5
    rows = np.where(alpha.any(1))[0]
    rgb, alpha = rgb[rows[0]:], alpha[rows[0]:]
    surface = int(np.argmax(alpha.all(1)))
    rgb = snap(rgb, palette_of([rgb[alpha]], 32))
    return rgba(rgb, alpha * 255), surface


def build_backdrop(sheet5):
    """
    Üç sahne yan yana, 192 piksel yüksek. Ek yerleri koyu bir kaya sütununun içinden geçer: iki ayrı
    resmin birleştiği belli olmasın. Üstü ve altı saydama söner: duvar odanın tabanına hizalanır, üstü ve
    altı düz karanlık zemine karışır.
    """
    height = 192
    parts = []
    for x0, y0, x1, y1 in BACKDROPS:
        img = Image.fromarray(sheet5[y0:y1, x0:x1].astype(np.uint8), "RGB")
        img = img.resize((round(img.width * height / img.height), height), Image.LANCZOS)
        parts.append(np.array(img, dtype=np.float64))
    blend = 90
    # Sahnelerin en koyu kayalarının rengi: ek yeri sütunu ve LevelBuilder'daki düz duvar zemini bu renk.
    allpix = np.concatenate([p.reshape(-1, 3) for p in parts])
    lum = allpix.mean(1)
    dark = allpix[lum <= np.percentile(lum, 8)].mean(0)
    print(f"  koyu kaya rengi: {tuple(int(c) for c in dark)}")

    def joint(left, right):
        t = ((np.arange(blend) + 0.5) / blend)[None, :, None]
        mix = left[:, -blend:] * (1 - t) + right[:, :blend] * t
        pillar = np.sin(np.pi * t) ** 1.5 * 0.92  # ortada neredeyse tümüyle koyu
        return mix * (1 - pillar) + dark * pillar

    pano = parts[0]
    for p in parts[1:]:
        pano = np.concatenate([pano[:, :-blend], joint(pano, p), p[:, blend:]], axis=1)
    w = pano.shape[1]
    pano = np.concatenate([joint(pano, pano)[:, :], pano[:, blend:w - blend]], axis=1)  # uçlar da sütunla bağlanır
    pano = snap(pano, palette_of([pano], 48))
    build_backdrop.dark = dark
    # Üst ve alt kenar Bayer desenle saydama söner (piksel sanatta yarı saydamlık yerine).
    h = pano.shape[0]
    y = np.arange(h)[:, None]
    fade = np.minimum(np.clip(y / 40.0, 0, 1), np.clip((h - 1 - y) / 16.0, 0, 1)) * np.ones((1, pano.shape[1]))
    yy, xx = np.mgrid[0:h, 0:pano.shape[1]]
    alpha = np.where(fade > BAYER[yy % 4, xx % 4], 255, 0)
    return rgba(pano, alpha)


def build_veins(sheets):
    out = {}
    for i, (name, (sheet, row, col)) in enumerate(VEINS.items()):
        (y0, y1), (x0, x1) = ROW_Y[row], COL_X[col]
        y0 += int((y1 - y0) * VEIN_TOP_CUT)
        small = downscale(sheets[sheet][y0:y1, x0 + 3:x1 - 3])
        # Koyu toprağın içinde kayboluyordu: ortalamadan uzaklaştırıp biraz aydınlat.
        mean = small.reshape(-1, 3).mean(0)
        small = np.clip((small - mean) * VEIN_CONTRAST + mean * VEIN_LIFT, 0, 255)
        small = snap(small, palette_of([small], 32))
        h, w = small.shape[:2]
        out[f"mg_damar_{name}"] = rgba(small, ragged_alpha(h, w, FEATHER, 100 + i))
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path, help="çıktıların büyütülmüş önizlemesi")
    args = parser.parse_args()

    sheets = {k: load(k) for k in ("42", "5", "62", "63")}
    images = {}
    ground, _ = build_ground(sheets["42"])
    images.update(ground)
    images["mg_ust"], surface = build_rim(sheets)
    print(f"  yüzey çizgisi: üstten {surface} piksel (LevelBuilder.RimRise)")
    images["mg_sacak"] = build_stalactites(sheets["5"])
    images["mg_arka"] = build_backdrop(sheets["5"])
    images.update(build_veins(sheets))

    for old in OUTPUT_DIR.glob("mg_damar_*.png"):
        if old.stem not in images:
            old.unlink()
    for name, image in images.items():
        save(name, image)

    if args.preview:
        scale = 2
        x, y, row_h = 0, 0, 0
        width = 2000
        sheet = Image.new("RGBA", (width, 1600), (70, 70, 90, 255))
        for name, image in images.items():
            img = Image.fromarray(image, "RGBA")
            img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
            if x + img.width > width:
                x, y, row_h = 0, y + row_h + 8, 0
            sheet.alpha_composite(img, (x, y))
            x += img.width + 8
            row_h = max(row_h, img.height)
        sheet.crop((0, 0, width, y + row_h)).save(args.preview)


if __name__ == "__main__":
    main()
