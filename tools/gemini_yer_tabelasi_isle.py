"""
Yer adı tabelasını (kasaba, vadi, bölüm adı) Gemini çıktısından oyuna hazır sprite'a çevirir.

Girdi : Assets/Sprites/_Gelen/yer_tabelasi_ham.png (1376x768, düz gri zemin, boş iki direkli ahşap levha)
Çıktı : Assets/Sprites/Environment/Props/tabela_yer.png (alt orta pivot: direkler yere oturur)

Levhanın sol ve sağ 25 pikseli (uçlar, direkler ve kum yığınları) sabit kalır, ortası yan yana tekrar ederek yazının boyuna
uzar (SpriteImportRules: tabela_yer.png kenarlığı, LevelPainter.PlaceSign). Gemini ızgarası ~11.4 kaynak pikseli.

Kullanım (proje kökünden): python tools/gemini_yer_tabelasi_isle.py [--preview onizleme.png]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen" / "yer_tabelasi_ham.png"
OUT = ROOT / "Assets" / "Sprites" / "Environment" / "Props" / "tabela_yer.png"
FACTOR = 11.4
BG_DIST = 20
PALETTE = 16


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()
    img = np.asarray(Image.open(SRC).convert("RGB")).astype(int)
    bg = np.median(img[:6].reshape(-1, 3), axis=0)
    fg = np.abs(img - bg).max(axis=2) > BG_DIST
    lab, _ = ndimage.label(ndimage.binary_closing(fg, iterations=3))
    sl = max(ndimage.find_objects(lab), key=lambda s: (s[0].stop - s[0].start) * (s[1].stop - s[1].start))
    c, m = img[sl], ndimage.binary_fill_holes(fg)[sl]
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
    out = out[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    op = out[..., 3] > 0
    px = out[op][:, :3]
    strip = Image.fromarray(px.reshape(1, -1, 3).astype(np.uint8), "RGB")
    pal = np.array(strip.quantize(colors=PALETTE, method=Image.Quantize.MEDIANCUT).getpalette()[:PALETTE * 3]).reshape(-1, 3)
    out[op, :3] = pal[((px[:, None, :].astype(int) - pal[None]) ** 2).sum(axis=2).argmin(axis=1)]
    Image.fromarray(out, "RGBA").save(OUT)
    rows = np.nonzero(op.mean(axis=1) > 0.8)[0]
    print(f"tabela_yer: {out.shape[1]}x{out.shape[0]}, levha satırları {rows.min()}-{rows.max()} (üstten)")
    if args.preview:
        Image.fromarray(out, "RGBA").resize((out.shape[1] * 8, out.shape[0] * 8), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
