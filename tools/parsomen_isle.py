"""
Eğitim tabelalarının parşömenini Gemini çıktısından oyuna hazır parçalara çevirir.

Girdi: Assets/Sprites/_Gelen/parsomen_ham.png (1200x896, gri zemin; üstte kapalı/açılan rulolar scroll_01-04,
altta açık parşömenler scroll_05-06).
Çıktı (Assets/Sprites/UI/Parsomen):
  parsomen_rulo_0.png  : kurdeleyle bağlı kapalı rulo (scroll_01)
  parsomen_rulo_1.png  : kurdele çözülmüş, iki tutamak yan yana (scroll_02)
  parsomen_sol.png / parsomen_sag.png : açık parşömenin tutamakları (scroll_06)
  parsomen_kagit.png   : tutamaklar arasındaki kâğıt; sol/sağ PAPER_BORDER piksel sabit, ortası yan yana tekrar
                         eder (SpriteImportRules kenarlığı ayarlar), böylece kâğıt her yazının boyuna uzar.

Gemini ızgarası ~4 kaynak pikseli; oyunun piksel boyuna (32 PPU, tutamak ~1.8 birim) yakın dursun diye FACTOR=6 ile
küçültülür. Her sanat pikseli kendi bloğundaki ön plan piksellerinin medyanıdır (yumuşatma yok).

Kullanım (proje kökünden): python tools/parsomen_isle.py [--preview onizleme.png]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen" / "parsomen_ham.png"
OUT = ROOT / "Assets" / "Sprites" / "UI" / "Parsomen"
FACTOR = 6
BG_DIST = 14          # zeminden bu kadar farklı piksel ön plan
# Kaynak kutular (x0, x1, y0, y1): kapalı rulolar ve açık parşömen aynı dikey aralıkta (tutamak boyu aynı)
ROLLED = [(101, 208, 70, 403), (312, 438, 70, 403)]
OPEN = (603, 1159, 515, 857)
ROLLER_W = 8          # küçültülmüş açık parşömende tutamak genişliği (sanat pikseli)
PAPER_BORDER = 12     # kâğıdın tekrar etmeyen sol/sağ kenarı (SpriteImportRules.ParchmentPaperBorder)


def cut(img, box):
    x0, x1, y0, y1 = box
    c = img[y0:y1, x0:x1]
    bg = np.median(img[:5].reshape(-1, 3), axis=0)
    fg = ndimage.binary_fill_holes(np.abs(c - bg).max(axis=2) > BG_DIST)
    h, w = c.shape[:2]
    oh, ow = int(round(h / FACTOR)), int(round(w / FACTOR))
    out = np.zeros((oh, ow, 4), np.uint8)
    for y in range(oh):
        ya, yb = int(y * h / oh), int((y + 1) * h / oh)
        for x in range(ow):
            xa, xb = int(x * w / ow), int((x + 1) * w / ow)
            m = fg[ya:yb, xa:xb]
            if m.mean() >= 0.5:
                out[y, x, :3] = np.median(c[ya:yb, xa:xb][m], axis=0)
                out[y, x, 3] = 255
    return out


def trim_x(a):
    xs = np.nonzero((a[..., 3] > 0).any(axis=0))[0]
    return a[:, xs.min():xs.max() + 1]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()
    img = np.asarray(Image.open(SRC).convert("RGB")).astype(int)

    rolled = [trim_x(cut(img, b)) for b in ROLLED]
    full = trim_x(cut(img, OPEN))
    h = full.shape[0]
    rolled = [r[:h] if r.shape[0] >= h else np.pad(r, ((0, h - r.shape[0]), (0, 0), (0, 0))) for r in rolled]
    parts = {
        "parsomen_rulo_0": rolled[0],
        "parsomen_rulo_1": rolled[1],
        "parsomen_sol": full[:, :ROLLER_W],
        "parsomen_sag": full[:, -ROLLER_W:],
        "parsomen_kagit": full[:, ROLLER_W:-ROLLER_W],
    }
    OUT.mkdir(parents=True, exist_ok=True)
    for name, a in parts.items():
        Image.fromarray(a, "RGBA").save(OUT / f"{name}.png")
        print(f"{name}: {a.shape[1]}x{a.shape[0]}")
    paper = parts["parsomen_kagit"]
    rows = np.nonzero((paper[..., 3] > 0).mean(axis=1) > 0.9)[0]
    print(f"kâğıt: tekrar eden orta {paper.shape[1] - 2 * PAPER_BORDER} px, dolu satırlar {rows.min()}-{rows.max()} "
          f"(ParchmentSign yazı alanı)")

    if args.preview:
        sheet = Image.new("RGBA", (sum(a.shape[1] for a in parts.values()) + 40, h), (83, 83, 83, 255))
        x = 0
        for a in parts.values():
            sheet.alpha_composite(Image.fromarray(a, "RGBA"), (x, 0))
            x += a.shape[1] + 8
        sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
