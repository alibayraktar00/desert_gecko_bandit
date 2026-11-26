"""
Ana menüde Gila'nın dayandığı direğin tepesindeki akbabayı Gemini çıktısından oyuna hazır karelere çevirir.

Girdi : Assets/Sprites/_Gelen/akbaba_ham.png  (2064x512, tek sıra 4 kare, direk tepesine tünemiş akbaba, gri zemin)
Çıktı : Assets/Sprites/UI/MainMenu/Gila/akbaba_00..03.png  (aynı boyutta kareler; alt kenar direğin kapağı)

Gemini akbabayı sağa bakar çizdi; menüye baksın diye yatay çevrilir. Kareler gemini_gila_puro_isle.py ile aynı
yöntemle hizalanır ve küçültülür (taban kare + sadece belirgin değişen bölgeler). Gemini'nin direğinden sadece
akbabanın ayakları altındaki birkaç satır (kapak) kalır; geri kalanını Gila karelerindeki direk tamamlar.

Kullanım (proje kökünden):
  python tools/gemini_akbaba_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gemini_gila_puro_isle as g  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen" / "akbaba_ham.png"
OUT = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu" / "Gila"

FRAMES = 4
FACTOR = 10.5    # akbabanın direği ~8 sanat pikseli olsun (Gila karelerindeki inceltilmiş direkle aynı)
PALETTE = 24
CAP_ROWS = 5     # ayakların altında kalan direk satırı (kapak)
FEET_ROWS = 4    # ayakların bu kadar satır üstüne kadar her kare tabanla aynı (pençeler ve kapak kıpırdamaz)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    img = np.asarray(Image.open(SRC).convert("RGB")).astype(np.int32)[:, ::-1]  # sola baksın
    w = img.shape[1] / FRAMES
    frames = [img[:, int(round(k * w)):int(round((k + 1) * w))] for k in range(FRAMES)]
    masks = [~g.background(f) for f in frames]
    # hizalama direğe ve ayaklara göre (alt yarı): direkle birleşim yeri kareden kareye kaymasın
    def lower(m):
        b = m.copy()
        b[: m.shape[0] // 2] = False
        return b
    aligned, amasks = [frames[0]], [masks[0]]
    for f, m in zip(frames[1:], masks[1:]):
        dy, dx = g.align(lower(masks[0]), lower(m))
        aligned.append(g.shift(f, dy, dx, np.array([122, 122, 122])))
        amasks.append(g.shift(m.astype(np.uint8), dy, dx, 0).astype(bool))

    h, fw = frames[0].shape[:2]
    out_w, out_h = int(round(fw / FACTOR)), int(round(h / FACTOR))
    small = [g.downscale(f, m, out_w, out_h) for f, m in zip(aligned, amasks)]
    pal = g.palette_of(small, PALETTE)
    small = [g.snap(s, pal) for s in small]
    base = small[0]
    # ayaklar ve direk kapağı hep tabandan: sadece ayakların birkaç satır üstünden yukarısı (gövde, baş) hareket eder
    balpha = base[..., 3] > 0
    brows = balpha.sum(axis=1)
    base_feet = max(y for y in range(len(brows)) if brows[y] > balpha[-1].sum() * 1.4)
    moving = np.zeros_like(balpha)
    moving[: base_feet - FEET_ROWS] = True
    result = [base]
    for s in small[1:]:
        diff = np.abs(s[..., :3].astype(int) - base[..., :3].astype(int)).sum(axis=2)
        changed = (diff > g.CHANGE) | ((s[..., 3] > 0) != (base[..., 3] > 0))
        region = g.dilate(g.blobs(changed, 6)) & moving
        frame = base.copy()
        frame[region] = s[region]
        result.append(frame)

    # direğin genişliği alt satırda; akbaba/kapak, satırın opak genişliği bunu belirgin aşınca başlar
    alpha = np.any([r[..., 3] > 0 for r in result], axis=0)
    pole_w = alpha[-1].sum()
    rows = alpha.sum(axis=1)
    feet = max(y for y in range(len(rows)) if rows[y] > pole_w * 1.4)
    bottom = min(len(rows), feet + 1 + CAP_ROWS)
    ys, xs = np.nonzero(alpha[:bottom])
    y0, x0, x1 = ys.min(), xs.min(), xs.max() + 1
    result = [r[y0:bottom, x0:x1] for r in result]
    pole_cols = np.nonzero(alpha[bottom - 1, x0:x1])[0]
    center = (pole_cols.min() + pole_cols.max() + 1) / 2

    for i, r in enumerate(result):
        Image.fromarray(r, "RGBA").save(OUT / f"akbaba_{i:02d}.png")
    print(f"{len(result)} kare {result[0].shape[1]}x{result[0].shape[0]}, direk genişliği {len(pole_cols)}, "
          f"direğin ortası x={center:.1f} (MainMenuBuilder.VulturePostCenterArt)")

    if args.preview:
        ph, pw = result[0].shape[:2]
        sheet = Image.new("RGBA", (pw * FRAMES, ph), (90, 120, 160, 255))
        for i, r in enumerate(result):
            sheet.alpha_composite(Image.fromarray(r, "RGBA"), (i * pw, 0))
        sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
