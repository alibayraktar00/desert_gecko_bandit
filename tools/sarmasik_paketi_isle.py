"""
Dikenli sarmaşık sayfalarını (Part 8.3 ve 8.4) oyun görsellerine çevirir: Derin Galeri girişindeki dikenli kuyu
ve sarı lav üstündeki sarmaşık halkaları için.

  Props (tavan_ ile başlayanlar üst kenarından asılır):
    tavan_sarmasik_halka   dilin tutunduğu halkalı sarmaşık (8.4 / prop_01)
    tavan_diken_koza       tavanda sallanan diken kozası (8.4 / prop_03)
    tavan_diken_top        düşerken açılmış dikenli top (8.4 / prop_03_b)
    dikenli_zemin_a / _b   rafın üstünü kaplayan kabarık dikenli sarmaşık (8.3 / tile_01, tile_01_b)
    tavan_dikenli_sarkit   rafın altından sarkan dikenler (8.3 / tile_02)
    tavan_dikenli_sarkan   uzun sarkan dikenli sarmaşıklar (8.3 / prop_01)
    dikenli_cali           dikenli çalı yumağı: rafın ucu, duvar (8.3 / prop_03)
    dikenli_kose_dis/_ic   dış ve iç köşe sarmaşıkları (8.3 / tile_07, prop_04)
    sari_lav_sicrama       lava düşenin sıçraması (8.4 / fluid_01_b)
    sari_lav_fiskirma_0..7 gece_lav_fiskirma karelerinin sarıya boyanmışı
  Katman (üst kenarından pivotlu, yan yana tekrar eder):
    sarmasik_duvar_sol / sarmasik_duvar_sag   duvar yüzündeki dikenler (8.3 / tile_03, tile_04)
    sari_lav_yuzey_0..7                       gece_lav_yuzey karelerinin sarıya boyanmışı (8.4 / fluid_01_c rengi)
    sari_lav_govde_0..7, gece_lav_govde_0..7  yüzeyin altındaki lav gövdesi: alt alta dikişsiz tekrar eder

Kaynaklar: Assets/Sprites/_Gelen/sarmasik_part8_3.webp, sarmasik_part8_4.webp
Kullanım (proje kökünden):  python tools/sarmasik_paketi_isle.py [--preview onizleme.png]
Gereken paketler: pillow, numpy
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
PROPS = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
LAYERS = ROOT / "Assets" / "Sprites" / "Environment" / "Katman"
PIXEL = 3

# ad: (kaynak, kutu x0 y0 x1 y1, klasör, kip). kip: "tek" = yalnızca en büyük parça (ve ona yapışık olanlar),
# "hepsi" = kırıntılar dışında her şey, "seffaf" = kutunun kenarına kadar boşluk bırakmadan (tekrar eden doku).
PIECES = {
    "tavan_sarmasik_halka": ("sarmasik_part8_4", (118, 121, 184, 264), PROPS, "tek"),
    "tavan_diken_koza": ("sarmasik_part8_4", (585, 365, 680, 480), PROPS, "tek"),
    "tavan_diken_top": ("sarmasik_part8_4", (805, 325, 945, 490), PROPS, "tek"),
    "sari_lav_sicrama": ("sarmasik_part8_4", (295, 548, 490, 715), PROPS, "hepsi"),
    "dikenli_zemin_a": ("sarmasik_part8_3", (45, 330, 256, 495), PROPS, "hepsi"),
    "dikenli_zemin_b": ("sarmasik_part8_3", (286, 330, 498, 495), PROPS, "hepsi"),
    "tavan_dikenli_sarkit": ("sarmasik_part8_3", (528, 325, 740, 480), PROPS, "hepsi"),
    "tavan_dikenli_sarkan": ("sarmasik_part8_3", (50, 92, 322, 250), PROPS, "hepsi"),
    "dikenli_cali": ("sarmasik_part8_3", (528, 545, 740, 715), PROPS, "hepsi"),
    "dikenli_kose_dis": ("sarmasik_part8_3", (286, 545, 498, 715), PROPS, "hepsi"),
    "dikenli_kose_ic": ("sarmasik_part8_3", (769, 545, 980, 715), PROPS, "hepsi"),
    "sarmasik_duvar_sol": ("sarmasik_part8_3", (770, 327, 860, 492), LAYERS, "seffaf"),
    "sarmasik_duvar_sag": ("sarmasik_part8_3", (889, 327, 980, 492), LAYERS, "seffaf"),
}

# Sarı lav rampası (koyudan açığa), fluid_01_c'den ölçüldü.
LAVA_RAMP = np.array([
    (92, 34, 6), (150, 62, 0), (214, 98, 4), (240, 140, 0), (246, 170, 6), (250, 200, 10), (255, 222, 60), (255, 244, 170),
], dtype=float)


def mask_of(region):
    """Gri hücre zemini dışında kalan pikseller: renkli (doymuş) ya da zeminden belirgin açık/koyu olanlar."""
    border = np.concatenate([region[:3].reshape(-1, 3), region[-3:].reshape(-1, 3),
                             region[:, :3].reshape(-1, 3), region[:, -3:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    sat = region.max(2) - region.min(2)
    lum = region.mean(2)
    m = (sat > 18) | (np.abs(lum - bg.mean()) > 26)
    # Kontur: renkli parçaya komşu, zeminden koyu pikseller
    dark = lum < bg.mean() - 7
    grown = m.copy()
    grown[1:] |= m[:-1]
    grown[:-1] |= m[1:]
    grown[:, 1:] |= m[:, :-1]
    grown[:, :-1] |= m[:, 1:]
    return m | (grown & dark)


def components(m):
    h, w = m.shape
    seen = np.zeros_like(m)
    out = []
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
        out.append(comp)
    return out


def keep(m, mode):
    comps = components(m)
    out = np.zeros_like(m)
    if not comps:
        return out
    if mode == "tek":
        biggest = max(comps, key=len)
        for y, x in biggest:
            out[y, x] = True
        return out
    limit = 10 if mode == "hepsi" else 4
    for comp in comps:
        if len(comp) >= limit:
            for y, x in comp:
                out[y, x] = True
    return out


def downscale(region, m):
    h, w = region.shape[0] // PIXEL, region.shape[1] // PIXEL
    blocks = region[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL, 3).transpose(0, 2, 1, 3, 4).reshape(h, w, -1, 3)
    mblocks = m[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL).transpose(0, 2, 1, 3).reshape(h, w, -1)
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    small = mblocks.mean(2) >= 0.5
    for y, x in zip(*np.nonzero(small)):
        rgba[y, x, :3] = np.median(blocks[y, x][mblocks[y, x]], axis=0)
        rgba[y, x, 3] = 255
    return rgba


def trim(rgba, mode):
    if mode == "seffaf":
        return rgba
    ys, xs = np.nonzero(rgba[..., 3])
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def cut(sheets, name):
    src, (x0, y0, x1, y1), _, mode = PIECES[name]
    region = sheets[src][y0:y1, x0:x1]
    m = mask_of(region)
    small = downscale(region, m)
    # Kırıntılar küçük ölçekte ayıklanır: ateş böcekleri ve düşen çizgiler gider.
    alpha = keep(small[..., 3] > 0, mode)
    small[~alpha] = 0
    return trim(small, mode)


def recolor_lava(path_in, path_out):
    im = np.array(Image.open(path_in).convert("RGBA")).astype(float)
    lum = im[..., :3] @ np.array([0.3, 0.55, 0.15])
    visible = im[..., 3] > 0
    if not visible.any():
        Image.fromarray(im.astype(np.uint8)).save(path_out)
        return
    lo, hi = np.percentile(lum[visible], 2), np.percentile(lum[visible], 99.5)
    t = np.clip((lum - lo) / max(1.0, hi - lo), 0, 1) * (len(LAVA_RAMP) - 1)
    i = np.floor(t).astype(int).clip(0, len(LAVA_RAMP) - 2)
    f = (t - i)[..., None]
    rgb = LAVA_RAMP[i] * (1 - f) + LAVA_RAMP[i + 1] * f
    # rampa basamaklı kalsın (piksel sanat): en yakın renge yuvarla
    nearest = np.argmin(((rgb[..., None, :] - LAVA_RAMP[None, None]) ** 2).sum(-1), axis=-1)
    im[..., :3] = np.where(visible[..., None], LAVA_RAMP[nearest], im[..., :3])
    Image.fromarray(im.astype(np.uint8)).save(path_out)


BODY_W, BODY_H = 64, 64


def lava_palette(surface_path):
    """Yüzey karesinin kullandığı renkler, koyudan açığa (az kullanılan ara tonlar atılır)."""
    im = np.array(Image.open(surface_path).convert("RGBA"))
    px = im[im[..., 3] > 0][:, :3]
    colors, counts = np.unique(px, axis=0, return_counts=True)
    colors = colors[counts >= 20]
    lum = colors @ np.array([0.3, 0.55, 0.15])
    return colors[np.argsort(lum)].astype(float)


def make_body(surface_path, out_paths, seed):
    """
    Lav gövdesi: yüzeyin renkleriyle akışkan damarlı doku ve kabarcıklar. Dalgaların periyodu karenin tam katı olduğu
    için her yönde dikişsiz tekrar eder; 8 kare boyunca damarlar yana akar, kabarcıklar şişip söner (döngü kapanır).
    Yüzeyin alt satırları koyu olduğundan gövde de koyu tonlardan başlar, açık tonlar sadece damar ve kabarcıkta.
    """
    palette = lava_palette(surface_path)
    n = len(palette)
    dark = palette[: max(3, int(n * 0.65))]  # gövde: en açık tonlar yüzeye kalsın
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:BODY_H, 0:BODY_W].astype(float)
    waves = [(0, 3, 1.0), (1, 2, 0.7), (1, 5, 0.45), (2, 4, 0.35), (3, 7, 0.2)]  # çoğu yatay: lav yana akar
    phases = rng.uniform(0, 2 * np.pi, len(waves))
    bubbles = [(rng.uniform(0, BODY_W), rng.uniform(0, BODY_H), rng.uniform(2.2, 3.8), rng.integers(0, 8)) for _ in range(6)]
    for frame, out in enumerate(out_paths):
        t = 2 * np.pi * frame / len(out_paths)
        v = np.zeros_like(x)
        for (fx, fy, a), ph in zip(waves, phases):
            v += a * np.sin(2 * np.pi * (fx * x / BODY_W + fy * y / BODY_H) + ph + t * max(1, fx))
        v = (v - v.min()) / (v.max() - v.min())
        v = v ** 2.0  # çoğu koyu, damarlar ince ve parlak
        idx = np.clip((v * len(dark)).astype(int), 0, len(dark) - 1)
        rgb = dark[idx]
        for bx, by, r, born in bubbles:
            life = (frame - born) % 8
            radius = r * [0.5, 0.8, 1.0, 1.0, 0.8, 0.5, 0, 0][life]
            if radius <= 0:
                continue
            dx = (x - bx + BODY_W / 2) % BODY_W - BODY_W / 2
            dy = (y - by + BODY_H / 2) % BODY_H - BODY_H / 2
            d = np.hypot(dx, dy)
            rgb[d <= radius] = palette[int(n * 0.6)]
            rgb[(d <= radius) & (dx + dy < -radius * 0.4)] = palette[int(n * 0.85)]  # sol üstte parıltı
            rgb[(d > radius) & (d <= radius + 0.9)] = palette[1]  # koyu kontur
        tile = np.dstack([rgb, np.full(x.shape, 255.0)])
        Image.fromarray(tile.astype(np.uint8)).save(out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview")
    args = parser.parse_args()
    sheets = {n: np.array(Image.open(SOURCE_DIR / f"{n}.webp").convert("RGB")).astype(float)
              for n in ("sarmasik_part8_3", "sarmasik_part8_4")}
    made = []
    for name, (_, _, folder, _) in PIECES.items():
        rgba = cut(sheets, name)
        out = folder / f"{name}.png"
        Image.fromarray(rgba).save(out)
        made.append(out)
        print(f"{name}: {rgba.shape[1]}x{rgba.shape[0]}")
    for i in range(8):
        recolor_lava(LAYERS / f"gece_lav_yuzey_{i}.png", LAYERS / f"sari_lav_yuzey_{i}.png")
        recolor_lava(PROPS / f"gece_lav_fiskirma_{i}.png", PROPS / f"sari_lav_fiskirma_{i}.png")
    for seed, lava in enumerate(("gece", "sari")):
        make_body(LAYERS / f"{lava}_lav_yuzey_0.png", [LAYERS / f"{lava}_lav_govde_{i}.png" for i in range(8)], seed + 3)
    made += [LAYERS / "sari_lav_yuzey_0.png", LAYERS / "sari_lav_govde_0.png", LAYERS / "gece_lav_govde_0.png",
             PROPS / "sari_lav_fiskirma_3.png"]

    if args.preview:
        tiles = [Image.open(p).convert("RGBA") for p in made]
        scale = 3
        width = sum(t.width * scale + 10 for t in tiles) + 10
        height = max(t.height for t in tiles) * scale + 20
        sheet = Image.new("RGBA", (width, height * 2), (0, 0, 0, 255))
        sheet.paste((150, 120, 90, 255), (0, height, width, height * 2))
        x = 10
        for t in tiles:
            big = t.resize((t.width * scale, t.height * scale), Image.NEAREST)
            sheet.alpha_composite(big, (x, 10))
            sheet.alpha_composite(big, (x, height + 10))
            x += big.width + 10
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
