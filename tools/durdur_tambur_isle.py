"""
Duraklatma menüsünün (tambur) görsellerini Gemini çıktılarından oyuna hazır sprite'lara çevirir.

Girdi (Assets/Sprites/_Gelen, Gemini'den piksel ızgarasına indirilmiş, gri #727272 zeminli):
  durdur_tambur_ham.png : arkadan bakılan, kenarı altı oyuklu revolver tamburu, 6 boş hazne
  durdur_fisek_ham.png  : tek fişek dibi (pirinç halka + kapsül)
  durdur_tabela_ham.png : boş tahta tabela
  durdur_bandana_ham.webp : bordo kovboy bandanası şeridi (kullanıcı Gemini'den verdi, tam çözünürlük, ~5.69 px/piksel)
  durdur_duman_ham.png  : küçük duman bulutu (macenta #FF00FF zeminli)
Çıktı (Assets/Sprites/UI/Pause):
  durdur_tambur.png        : zemini silinmiş tambur (oyunda döner)
  durdur_fisek.png         : hazneye oturan fişek dibi
  durdur_fisek_secili.png  : seçili fişek (parlak, altın hare)
  durdur_tabela.png        : seçili seçeneğin adının yazdığı tabela
  durdur_isaret.png        : tamburun üstündeki seçim oku
  durdur_bandana.png       : seçili haznenin yanından açılan bandana (sol ucu tamburun arkasına girer)
  durdur_duman.png         : yazı bandanada belirirken çıkan duman
  durdur_yazi_<ad>_0/1.png : seçenek yazıları (0 normal, 1 seçili altın), menu_tambur_ayir.py'deki piksel font

Tambur merkezi ve hazne konumları ekrana yazılır; PauseMenuBuilder (LevelBuilder.BuildPauseMenu) sabitleri bunlarla aynı olmalı.

Kullanım (proje kökünden):
  python tools/durdur_tambur_isle.py [--preview onizleme.png]
"""
import argparse
import math
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from menu_tambur_ayir import GLYPHS, CEDILLA, DOTTED, GLYPH_H, tall  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT = ROOT / "Assets" / "Sprites" / "UI" / "Pause"

OUTLINE = (20, 8, 16)
GOLD = (232, 184, 72)
GOLD_LIGHT = (251, 230, 168)
CREAM = (242, 230, 208)
CREAM_SHADE = (176, 150, 120)
SHADOW = (48, 8, 20)  # koyu bordo: bandanada da toprakta da yumuşak durur

LABELS = {"devam": "DEVAM", "bastan": "BAŞTAN", "ayarlar": "AYARLAR", "menu": "MENÜ",
          "ses": "SES", "tamekran": "TAM EKRAN", "geri": "GERİ"}


def cut_gray(rgb):
    """Kenara bağlı gri zemini saydam yapar (Gemini zemini: düşük doygunluk, orta parlaklık)."""
    h, w = rgb.shape[:2]
    spread = rgb.max(axis=2).astype(int) - rgb.min(axis=2)
    value = rgb.max(axis=2)
    bg_like = (spread < 14) & (value > 95) & (value < 150)
    bg = np.zeros((h, w), dtype=bool)
    q = deque((y, x) for y in range(h) for x in range(w) if (y in (0, h - 1) or x in (0, w - 1)) and bg_like[y, x])
    for y, x in q:
        bg[y, x] = True
    while q:
        y, x = q.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and not bg[ny, nx] and bg_like[ny, nx]:
                bg[ny, nx] = True
                q.append((ny, nx))
    rgba = np.dstack([rgb, np.where(bg, 0, 255).astype(np.uint8)])
    return rgba


def crop(rgba, pad=0):
    ys, xs = np.nonzero(rgba[..., 3])
    return rgba[max(0, ys.min() - pad):ys.max() + 1 + pad, max(0, xs.min() - pad):xs.max() + 1 + pad]


def components(mask):
    h, w = mask.shape
    seen = np.zeros_like(mask)
    out = []
    for sy in range(h):
        for sx in range(w):
            if not mask[sy, sx] or seen[sy, sx]:
                continue
            pts, q = [], deque([(sy, sx)])
            seen[sy, sx] = True
            while q:
                y, x = q.popleft()
                pts.append((y, x))
                for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            out.append(pts)
    return out


def grid_sample(rgb, period, phase_x, phase_y):
    """Gemini'nin büyütülmüş piksellerini (period kaynak pikseli = 1 oyun pikseli) tekrar oyun pikseline indirir:
    her hücrenin ortasındaki 3x3 örneğin medyanı (kenar yumuşaması ve sıkıştırma gürültüsü gider)."""
    h, w = rgb.shape[:2]
    xs = np.arange(phase_x + 0.5 - period * np.ceil((phase_x + 0.5) / period), w, period)
    ys = np.arange(phase_y + 0.5 - period * np.ceil((phase_y + 0.5) / period), h, period)
    out = np.zeros((len(ys), len(xs), 3), dtype=np.uint8)
    for j, y0 in enumerate(ys):
        for i, x0 in enumerate(xs):
            pts = [rgb[min(h - 1, max(0, int(round(y0 + dy * period)))), min(w - 1, max(0, int(round(x0 + dx * period))))]
                   for dy in (0.3, 0.5, 0.7) for dx in (0.3, 0.5, 0.7)]
            out[j, i] = np.median(np.array(pts), axis=0)
    return out


def cut_magenta(rgb):
    """Macenta zemini ve ona karışmış pembe kenar piksellerini saydam yapar (duman gri-bej, etkilenmez)."""
    c = rgb.astype(int)
    pink = (c[..., 0] - c[..., 1] > 50) & (c[..., 2] - c[..., 1] > 50)
    return np.dstack([rgb, np.where(pink, 0, 255).astype(np.uint8)])


def chambers(tambur):
    """Haznelerin (en koyu, büyük, içi dolu bölgeler) merkezleri ve yarıçapı, sprite pikseli (sol üstten)."""
    rgb = tambur[..., :3].astype(int)
    dark = (rgb.sum(axis=2) < 60) & (tambur[..., 3] > 0)
    holes = [c for c in components(dark) if len(c) > 80]
    res = []
    for pts in holes:
        ys = np.array([p[0] for p in pts]); xs = np.array([p[1] for p in pts])
        res.append(((xs.mean() + 0.5), (ys.mean() + 0.5), math.sqrt(len(pts) / math.pi)))
    return res


def shrink(rgba, size):
    """Büyük fişek görselini hazne boyuna indirir: renk ortalaması + keskin alfa + koyu dış hat."""
    im = Image.fromarray(rgba, "RGBA")
    pre = Image.new("RGBA", im.size)
    pre.paste(im, (0, 0))
    small = np.array(pre.resize((size, size), Image.BOX))
    a = small[..., 3] > 110
    out = np.zeros_like(small)
    out[a, :3] = small[a, :3]
    out[a, 3] = 255
    # kenar pikselleri koyu hat
    edge = a & ~(np.roll(a, 1, 0) & np.roll(a, -1, 0) & np.roll(a, 1, 1) & np.roll(a, -1, 1))
    out[edge, :3] = OUTLINE
    return out


def selected_variant(fisek):
    """Seçili fişek: biraz daha parlak, çevresinde 1 piksel altın hare."""
    h, w = fisek.shape[:2]
    out = np.zeros((h + 2, w + 2, 4), dtype=np.uint8)
    body = fisek.copy()
    lit = body[..., 3] > 0
    body[lit, :3] = np.clip(body[lit, :3].astype(int) * 1.18 + 12, 0, 255).astype(np.uint8)
    a = np.zeros((h + 2, w + 2), dtype=bool)
    a[1:-1, 1:-1] = lit
    ring = np.zeros_like(a)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy or dx:
                ring |= np.roll(np.roll(a, dy, 0), dx, 1)
    out[ring & ~a] = (*GOLD_LIGHT, 255)
    out[1:-1, 1:-1][lit] = body[lit]
    return out


def arrow():
    """Tamburun üstünde aşağı bakan küçük altın ok (seçili hazneyi gösterir)."""
    rows = ["#########",
            "#BBBBBBB#",
            ".#BLLLB#.",
            "..#BLB#..",
            "...#B#...",
            "....#...."]
    out = np.zeros((len(rows), len(rows[0]), 4), dtype=np.uint8)
    col = {"#": OUTLINE, "B": GOLD, "L": GOLD_LIGHT}
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            if c in col:
                out[y, x] = (*col[c], 255)
    return out


def label(text, selected):
    """Düz satır piksel yazı, altında 1 piksel koyu bordo gölge. Normal: krem; seçili: altın, üst kenarı açık."""
    glyphs, x = [], 0
    for ch in text:
        base = DOTTED.get(ch, CEDILLA.get(ch, ch))
        g = tall(GLYPHS[base], base)
        glyphs.append((ch, g, x))
        x += len(g[0]) + 1
    w, h = x - 1, GLYPH_H + 2
    bits = np.zeros((h, w), dtype=bool)
    for ch, g, gx in glyphs:
        for y, row in enumerate(g):
            for xx, c in enumerate(row):
                if c == "#":
                    bits[1 + y, gx + xx] = True
        if ch in CEDILLA:
            bits[1 + GLYPH_H, gx + len(g[0]) // 2] = True
        if ch == "İ":
            bits[0, gx + len(g[0]) // 2] = True
        elif ch in DOTTED:
            bits[0, gx] = bits[0, gx + len(g[0]) - 1] = True
    # Dış hat yok (harf araları ve iç boşluklar dolup siyah kutu oluyordu): yalnızca 1 piksel aşağı gölge,
    # böylece yazının arkasında bandananın bordosu görünür.
    b = np.zeros((h + 1, w + 1), dtype=bool)
    b[:-1, :-1] = bits
    shadow = np.roll(b, 1, 0) & ~b
    out = np.zeros((h + 1, w + 1, 4), dtype=np.uint8)
    out[shadow] = (*SHADOW, 255)
    top = b & ~np.roll(b, 1, 0)
    if selected:
        out[b] = (*GOLD, 255)
        out[top] = (*GOLD_LIGHT, 255)
    else:
        out[b] = (*CREAM_SHADE, 255)
        out[top] = (*CREAM, 255)
    return out


def save(arr, name):
    Image.fromarray(arr, "RGBA").save(OUT / name)
    return arr


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)

    tambur = crop(cut_gray(np.array(Image.open(SRC / "durdur_tambur_ham.png").convert("RGB"))))
    # tam ortalı olsun diye kareye tamamla (dönerken merkez kaymasın)
    th, tw = tambur.shape[:2]
    side = max(th, tw) | 1
    sq = np.zeros((side, side, 4), dtype=np.uint8)
    oy, ox = (side - th) // 2, (side - tw) // 2
    sq[oy:oy + th, ox:ox + tw] = tambur
    save(sq, "durdur_tambur.png")
    holes = chambers(sq)
    cx = cy = side / 2
    radii = [math.hypot(x - cx, y - cy) for x, y, _ in holes]
    hole_r = sum(r for *_, r in holes) / len(holes)
    print(f"tambur {side}x{side}, merkez ({cx:.1f},{cy:.1f}), {len(holes)} hazne, "
          f"halka yarıçapı {sum(radii) / len(radii):.2f} px, hazne yarıçapı {hole_r:.2f} px")
    for x, y, r in holes:
        print(f"  hazne ({x:.1f},{y:.1f}) açı {math.degrees(math.atan2(cx - x, cy - y)):.1f}")

    fisek_big = crop(cut_gray(np.array(Image.open(SRC / "durdur_fisek_ham.png").convert("RGB"))))
    size = int(round(hole_r * 2)) + 1
    fisek = save(shrink(fisek_big, size), "durdur_fisek.png")
    secili = save(selected_variant(fisek), "durdur_fisek_secili.png")
    print(f"fişek {size}x{size}")

    tabela = save(crop(cut_gray(np.array(Image.open(SRC / "durdur_tabela_ham.png").convert("RGB")))), "durdur_tabela.png")
    print(f"tabela {tabela.shape[1]}x{tabela.shape[0]}")
    ok = save(arrow(), "durdur_isaret.png")

    bandana_rgb = grid_sample(np.array(Image.open(SRC / "durdur_bandana_ham.webp").convert("RGB")), 5.69, 4.0, 0.75)
    bandana = save(crop(cut_gray(bandana_rgb)), "durdur_bandana.png")
    print(f"bandana {bandana.shape[1]}x{bandana.shape[0]}")
    duman = save(crop(cut_magenta(np.array(Image.open(SRC / "durdur_duman_ham.png").convert("RGB")))), "durdur_duman.png")
    print(f"duman {duman.shape[1]}x{duman.shape[0]}")

    labels = []
    for key, text in LABELS.items():
        for s in (0, 1):
            labels.append(save(label(text, bool(s)), f"durdur_yazi_{key}_{s}.png"))

    if args.preview:
        tiles = [sq, fisek, secili, tabela, ok, bandana, duman] + labels
        w = sum(t.shape[1] for t in tiles) + 4 * len(tiles)
        h = max(t.shape[0] for t in tiles)
        sheet = Image.new("RGBA", (w, h), (60, 40, 30, 255))
        x = 0
        for t in tiles:
            im = Image.fromarray(t, "RGBA")
            sheet.alpha_composite(im, (x, 0))
            x += t.shape[1] + 4
        sheet.resize((w * 3, h * 3), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
