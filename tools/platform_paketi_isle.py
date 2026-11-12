"""
Geçirgen platform sayfalarını (Part 11 ve 11.2) üç parçalı platform görsellerine çevirir. Her tür için
sol ucun ucu + dikişsiz tekrar eden orta + sağ ucun ucu tek görselde birleşir; Unity ortayı tekrar eder
(kenar payı CAP piksel, SpriteImportRules'ta). Çıktılar Katman klasörüne gider (üst kenarından pivotlu).

  plat_tahta     ahşap iskele (11 / 1)          plat_kok     toprak ve kök (11.2 / 1)
  plat_sarmasik  sarmaşık (11 / 2)              plat_tas     taş ve moloz (11.2 / 2)
  plat_izgara    maden demir ızgara (11 / 3)    plat_bitki   yeraltı bitkileri (11.2 / 3)
                                                plat_cakil   aşınmış toprak ve çakıl (11.2 / 4)
Asma köprüler (11 / 4) kullanılmıyor.

Her türün yürünen yüzeyi (otlar ve taşlar görselin tepesinden biraz aşağıda) yazdırılır; LevelBuilder'daki
LedgeSurfaces tablosuna girer.
Kaynaklar: Assets/Sprites/_Gelen/platformlar_11.png, platformlar_112.png
Kullanım (proje kökünden):  python tools/platform_paketi_isle.py [--preview onizleme.png]
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
CAP = 30  # uç parçalarının genişliği (oyun pikseli); SpriteImportRules.PlatformCap ile aynı olmalı

# (L, M, R) kutuları: x0, y0, x1, y1 (kaynak pikseli)
TYPES = {
    "plat_tahta": ("platformlar_11", [(33, 180, 309, 258), (369, 183, 657, 246), (714, 180, 990, 258)]),
    "plat_sarmasik": ("platformlar_11", [(33, 327, 306, 417), (372, 300, 657, 444), (717, 315, 987, 420)]),
    "plat_izgara": ("platformlar_11", [(33, 483, 306, 570), (378, 489, 648, 573), (714, 483, 993, 573)]),
    "plat_kok": ("platformlar_112", [(33, 177, 312, 264), (366, 177, 657, 261), (714, 180, 993, 267)]),
    "plat_tas": ("platformlar_112", [(33, 336, 309, 414), (366, 339, 654, 417), (714, 339, 990, 420)]),
    "plat_bitki": ("platformlar_112", [(36, 492, 309, 573), (369, 480, 657, 597), (714, 489, 990, 576)]),
    "plat_cakil": ("platformlar_112", [(33, 642, 309, 720), (369, 636, 654, 726), (714, 642, 990, 717)]),
}


def piece(sheet, box):
    """Gri zemini atar (kutunun çerçevesinden ölçülür), sahte pikselleri indirir. RGB + maske döner."""
    x0, y0, x1, y1 = box
    pad = 4
    region = sheet[y0 - pad:y1 + pad, x0 - pad:x1 + pad]
    border = np.concatenate([region[:2].reshape(-1, 3), region[-2:].reshape(-1, 3),
                             region[:, :2].reshape(-1, 3), region[:, -2:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    mask = np.abs(region - bg).sum(2) > 30
    lo, hi = region.min(2), region.max(2)
    mask &= ~((lo > 140) & (hi - lo < 45))  # beyaz etiket yazıları
    h, w = region.shape[0] // PIXEL, region.shape[1] // PIXEL
    blocks = region[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL, 3).transpose(0, 2, 1, 3, 4).reshape(h, w, -1, 3)
    mblocks = mask[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL).transpose(0, 2, 1, 3).reshape(h, w, -1)
    rgb = np.zeros((h, w, 3))
    m = mblocks.mean(2) > 0.5
    for y, x in zip(*np.nonzero(m)):
        rgb[y, x] = np.median(blocks[y, x][mblocks[y, x]], axis=0)
    return rgb, drop_specks(m)


def drop_specks(m, keep=8):
    """Platformdan kopuk küçük kırıntıları (ateş böceği, yazı artığı) atar; asıl gövde ve sarkanlar kalır."""
    from collections import deque
    h, w = m.shape
    seen = np.zeros_like(m)
    out = np.zeros_like(m)
    for sy, sx in zip(*np.nonzero(m)):
        if seen[sy, sx]:
            continue
        comp, q = [], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            comp.append((y, x))
            for ny in (y - 1, y, y + 1):
                for nx in (x - 1, x, x + 1):
                    if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
        if len(comp) >= keep:
            for y, x in comp:
                out[y, x] = True
    return out


def surface_row(mask):
    """Yürünen yüzey: genişliğin çoğunun dolu olduğu ilk satır (üstteki ot ve taşlar bunun üstünde kalır)."""
    return int(np.argmax(mask.mean(1) > 0.6))


def seamless(rgb, mask, blend=12):
    w = rgb.shape[1]
    t = ((np.arange(blend) + 0.5) / blend)[None, :]
    out_rgb, out_a = rgb[:, :w - blend].copy(), mask[:, :w - blend].astype(np.float64)
    out_rgb[:, :blend] = rgb[:, w - blend:] * (1 - t[..., None]) + rgb[:, :blend] * t[..., None]
    out_a[:, :blend] = mask[:, w - blend:] * (1 - t) + mask[:, :blend] * t
    return out_rgb, out_a > 0.5


def build(sheet, boxes):
    parts = [piece(sheet, b) for b in boxes]
    surfaces = [surface_row(m) for _, m in parts]
    # Üç parçayı yüzeylerinden hizala: aynı satırda yürünsün.
    above = max(surfaces)
    below = max(m.shape[0] - s for (_, m), s in zip(parts, surfaces))
    aligned = []
    for (rgb, m), s in zip(parts, surfaces):
        top = above - s
        rgb = np.pad(rgb, ((top, above + below - top - rgb.shape[0]), (0, 0), (0, 0)))
        m = np.pad(m, ((top, above + below - top - m.shape[0]), (0, 0)))
        aligned.append((rgb, m))

    def trim_x(rgb, m):
        cols = np.where(m.any(0))[0]
        return rgb[:, cols[0]:cols[-1] + 1], m[:, cols[0]:cols[-1] + 1]

    (lr, lm), (mr, mm), (rr, rm) = [trim_x(*p) for p in aligned]
    mr, mm = seamless(mr, mm)
    rgb = np.concatenate([lr[:, :CAP], mr, rr[:, -CAP:]], axis=1)
    mask = np.concatenate([lm[:, :CAP], mm, rm[:, -CAP:]], axis=1)
    rows = np.where(mask.any(1))[0]
    rgb, mask = rgb[rows[0]:rows[-1] + 1], mask[rows[0]:rows[-1] + 1]
    surface = above - rows[0]

    # Webp gürültüsünü at: kısıtlı palet.
    opaque = rgb[mask].astype(np.uint8)
    strip = Image.fromarray(opaque.reshape(1, -1, 3), "RGB").quantize(colors=32, method=Image.Quantize.MEDIANCUT)
    palette = np.array(strip.getpalette()[:96], dtype=np.float64).reshape(-1, 3)
    flat = rgb.reshape(-1, 3)
    snapped = palette[((flat[:, None, :] - palette[None]) ** 2).sum(2).argmin(1)].reshape(rgb.shape)
    out = np.dstack([snapped.astype(np.uint8), (mask * 255).astype(np.uint8)])
    return out, surface


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    sheets = {}
    images = []
    for name, (sheet_name, boxes) in TYPES.items():
        if sheet_name not in sheets:
            sheets[sheet_name] = np.array(Image.open(SOURCE_DIR / f"{sheet_name}.png").convert("RGB"), dtype=np.float64)
        image, surface = build(sheets[sheet_name], boxes)
        path = OUTPUT_DIR / f"{name}.png"
        Image.fromarray(image, "RGBA").save(path)
        print(f"  {path.relative_to(ROOT)}  ({image.shape[1]}x{image.shape[0]})  yüzey satırı {surface}")
        images.append(image)

    if args.preview:
        scale = 3
        width = max(i.shape[1] for i in images) * scale
        height = sum(i.shape[0] * scale + 10 for i in images)
        sheet = Image.new("RGBA", (width, height), (70, 70, 90, 255))
        y = 0
        for i in images:
            img = Image.fromarray(i, "RGBA").resize((i.shape[1] * scale, i.shape[0] * scale), Image.NEAREST)
            sheet.alpha_composite(img, (0, y))
            y += img.height + 10
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
