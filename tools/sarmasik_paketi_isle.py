"""
Dikenli sarmaşık sayfalarını (Part 8.3 ve 8.4) oyun görsellerine çevirir: Derin Galeri girişindeki dikenli kuyu
ve sarı lav üstündeki sarmaşık halkaları için.

  Props (tavan_ ile başlayanlar üst kenarından asılır):
    tavan_sarmasik_halka   dilin tutunduğu halkalı sarmaşık (8.4 / prop_01)
    tavan_diken_koza       tavanda sallanan diken kozası (8.4 / prop_03)
    tavan_diken_top        düşerken açılmış dikenli top (8.4 / prop_03_b)
    sari_lav_sicrama       lava düşenin sıçraması (8.4 / fluid_01_b)
    sari_lav_fiskirma_0..7 gece_lav_fiskirma karelerinin sarıya boyanmışı
  Katman (üst kenarından pivotlu, yan yana tekrar eder):
    sarmasik_duvar_sol / sarmasik_duvar_sag   duvar yüzündeki dikenler (8.3 / tile_03, tile_04)
    sarmasik_tavan                            rafın altından sarkan dikenler (8.3 / tile_02)
    sari_lav_yuzey_0..7                       gece_lav_yuzey karelerinin sarıya boyanmışı (8.4 / fluid_01_c rengi)

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
    "sarmasik_duvar_sol": ("sarmasik_part8_3", (770, 327, 860, 492), LAYERS, "seffaf"),
    "sarmasik_duvar_sag": ("sarmasik_part8_3", (889, 327, 980, 492), LAYERS, "seffaf"),
    "sarmasik_tavan": ("sarmasik_part8_3", (528, 325, 740, 470), LAYERS, "seffaf"),
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
    made += [LAYERS / "sari_lav_yuzey_0.png", PROPS / "sari_lav_fiskirma_3.png"]

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
