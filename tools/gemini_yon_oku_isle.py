"""
Yön oku tabelalarını Gemini çıktılarından oyuna hazır sprite'lara çevirir.

Girdi (Assets/Sprites/_Gelen, 1376x768, düz gri zemin):
  yon_oku_ham.png        : aşağı, sağa
  yon_oku_capraz_ham.png : yukarı, sağ-aşağı, sağ-yukarı
Çıktı (Assets/Sprites/Environment/Props, alt orta pivot: kazık yere oturur):
  yon_oku_asagi / yon_oku_sag / yon_oku_yukari / yon_oku_sag_asagi / yon_oku_sag_yukari .png
Sola bakanlar oyunda yatay aynalanır (SpriteRenderer.flipX).

Gemini ızgarası ~11.5 kaynak pikseli (FACTOR); her sanat pikseli kendi bloğundaki ön plan piksellerinin medyanı.
Beş tabela ortak bir palete indirilir (iki ayrı görselden geldikleri için tonları biraz farklı).

Kullanım (proje kökünden): python tools/gemini_yon_oku_isle.py [--preview onizleme.png]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
FACTOR = 11.5
BG_DIST = 20
PALETTE = 24
# (görsel, sıra soldan, çıktı adı)
SIGNS = [
    ("yon_oku_ham.png", 0, "yon_oku_asagi"),
    ("yon_oku_ham.png", 1, "yon_oku_sag"),
    ("yon_oku_capraz_ham.png", 0, "yon_oku_yukari"),
    ("yon_oku_capraz_ham.png", 1, "yon_oku_sag_asagi"),
    ("yon_oku_capraz_ham.png", 2, "yon_oku_sag_yukari"),
]


def objects(img):
    """Gri zemin üstündeki tabelalar: soldan sağa (x0, x1, y0, y1) ve ön plan maskesi."""
    bg = np.median(img[:6].reshape(-1, 3), axis=0)
    fg = np.abs(img - bg).max(axis=2) > BG_DIST
    lab, _ = ndimage.label(ndimage.binary_closing(fg, iterations=3))
    boxes = [(s[1].start, s[1].stop, s[0].start, s[0].stop) for s in ndimage.find_objects(lab)
             if (s[0].stop - s[0].start) * (s[1].stop - s[1].start) > 2000]
    return sorted(boxes), ndimage.binary_fill_holes(fg)


def downscale(img, fg, box):
    x0, x1, y0, y1 = box
    c, m = img[y0:y1, x0:x1], fg[y0:y1, x0:x1]
    h, w = c.shape[:2]
    oh, ow = int(round(h / FACTOR)), int(round(w / FACTOR))
    out = np.zeros((oh, ow, 4), np.uint8)
    for y in range(oh):
        ya, yb = int(y * h / oh), int((y + 1) * h / oh)
        for x in range(ow):
            xa, xb = int(x * w / ow), int((x + 1) * w / ow)
            mm = m[ya:yb, xa:xb]
            if mm.mean() >= 0.5:
                out[y, x, :3] = np.median(c[ya:yb, xa:xb][mm], axis=0)
                out[y, x, 3] = 255
    ys, xs = np.nonzero(out[..., 3])
    return out[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()
    cache, sprites = {}, []
    for file, index, name in SIGNS:
        if file not in cache:
            img = np.asarray(Image.open(RAW / file).convert("RGB")).astype(int)
            cache[file] = (img, *objects(img))
        img, boxes, fg = cache[file]
        sprites.append((name, downscale(img, fg, boxes[index])))

    # ortak palet
    pixels = np.concatenate([s[s[..., 3] > 0][:, :3] for _, s in sprites])
    strip = Image.fromarray(pixels.reshape(1, -1, 3).astype(np.uint8), "RGB")
    pal = np.array(strip.quantize(colors=PALETTE, method=Image.Quantize.MEDIANCUT).getpalette()[:PALETTE * 3]).reshape(-1, 3)
    OUT.mkdir(parents=True, exist_ok=True)
    for name, s in sprites:
        op = s[..., 3] > 0
        px = s[op][:, :3].astype(int)
        s[op, :3] = pal[((px[:, None, :] - pal[None]) ** 2).sum(axis=2).argmin(axis=1)]
        Image.fromarray(s, "RGBA").save(OUT / f"{name}.png")
        print(f"{name}: {s.shape[1]}x{s.shape[0]}")

    if args.preview:
        h = max(s.shape[0] for _, s in sprites)
        sheet = Image.new("RGBA", (sum(s.shape[1] + 6 for _, s in sprites), h), (120, 120, 120, 255))
        x = 0
        for _, s in sprites:
            sheet.alpha_composite(Image.fromarray(s, "RGBA"), (x, h - s.shape[0]))
            x += s.shape[1] + 6
        sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
