"""
Ana menünün yön tabelasını Gemini çıktısından oyuna hazır sprite'lara çevirir ve tahta yazılarını üretir.

Girdi : Assets/Sprites/_Gelen/tabela_ham.png  (solda tek başına direk, sağda boş ok tahtası; düz gri zemin)
Çıktı : Assets/Sprites/UI/MainMenu/Tabela/
        tabela_direk.png              : toprak tümseğine dikili direk
        tabela_tahta.png              : sağı gösteren boş ok tahtası (oyunda her seçenek için bir kopya)
        tabela_yazi_<ad>_0.png / _1.png : tahtaya yakılmış koyu yazı (0) ve seçiliyken altın yazı (1)

Boyutlar Gila animasyonuyla (tools/gemini_gila_puro_isle.py) aynı "sanat pikseli" ölçeğinde; Unity'de ikisi de
2 kat büyütülür (MainMenuBuilder.ArtScale). Yazılar menu_tambur_ayir.py'deki piksel fontla, eğimsiz yazılır.

Kullanım (proje kökünden):
  python tools/gemini_tabela_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_gila_puro_isle import background, downscale, palette_of, snap  # noqa: E402
from menu_tambur_ayir import GLYPHS, CEDILLA, DOTTED, GLYPH_H, tall  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen" / "tabela_ham.png"
OUT = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu" / "Tabela"

POLE_HEIGHT = 104      # direk boyu (sanat pikseli)
PLANK_SIZE = (74, 17)  # tahta (genişlik, yükseklik); ok ucu dahil
TEXT_CENTER_X = 33     # yazının ortası tahtanın solundan bu kadar (çivilerin sağı, ok ucunun solu)
PALETTE = 24

INK = (58, 30, 18)          # tahtaya yakılmış yazı
INK_LIGHT = (214, 168, 112)  # yazının altındaki açık kenar (oyuk hissi)
GOLD = (244, 196, 82)
GOLD_LIGHT = (255, 236, 170)
OUTLINE = (36, 14, 10)

# oyundaki metin -> dosya adı; MainMenuBuilder.SignLabels ile aynı olmalı
LABELS = {"basla": "BAŞLA", "devam": "DEVAM ET", "level": "LEVEL SEÇ", "ayarlar": "AYARLAR", "cikis": "ÇIKIŞ",
          "level1": "LEVEL 1", "level2": "LEVEL 2", "level3": "LEVEL 3", "level4": "LEVEL 4", "geri": "GERİ",
          "ses": "SES", "tamekran": "TAM EKRAN", "sifirla": "SIFIRLA"}


def components(mask):
    h, w = mask.shape
    seen = np.zeros_like(mask)
    out = []
    for sy, sx in zip(*np.nonzero(mask)):
        if seen[sy, sx]:
            continue
        comp, q = [], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            comp.append((y, x))
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
        out.append(comp)
    return sorted(out, key=len, reverse=True)


def cut(rgb, fg, comp):
    ys, xs = zip(*comp)
    y0, y1, x0, x1 = min(ys), max(ys) + 1, min(xs), max(xs) + 1
    m = np.zeros_like(fg)
    m[list(ys), list(xs)] = True
    return rgb[y0:y1, x0:x1], m[y0:y1, x0:x1]


def bitmap(text):
    glyphs, x = [], 0
    for ch in text:
        base = DOTTED.get(ch, CEDILLA.get(ch, ch))
        g = tall(GLYPHS[base], base)
        glyphs.append((ch, g, x))
        x += len(g[0]) + 1
    bits = np.zeros((GLYPH_H + 2, x - 1), dtype=bool)
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
    return bits


def render_label(text, selected):
    bits = bitmap(text)
    h, w = bits.shape
    b = np.zeros((h + 2, w + 2), dtype=bool)
    b[1:-1, 1:-1] = bits
    out = np.zeros((h + 2, w + 2, 4), dtype=np.uint8)
    if selected:
        ring = np.zeros_like(b)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                ring |= np.roll(np.roll(b, dy, 0), dx, 1)
        out[ring & ~b] = (*OUTLINE, 255)
        out[b] = (*GOLD, 255)
        out[b & ~np.roll(b, 1, 0)] = (*GOLD_LIGHT, 255)
    else:
        out[np.roll(b, 1, 0) & ~b] = (*INK_LIGHT, 255)
        out[b] = (*INK, 255)
    return out


POLE_TOTAL = 186   # direk ekranın tepesine kadar uzanır (2x: 372 ekran pikseli); tepesine başlık tabelasının kolu çakılır
MOUND_ROWS = 18    # direğin altındaki toprak tümseği satırları
# Tümseğin oturduğu zemin: arka planda (640x360) direğin dibinin altındaki bölge (MainMenuBuilder PoleCenterXPx=62)
GROUND_BOX = (14, 326, 112, 360)   # x0, y0, x1, y1
BACKGROUND = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu" / "menu_arka_plan.png"


def pole_columns(sprite):
    """Direğin gövde sütunları: üst yarıda neredeyse hep opak olanlar."""
    upper = sprite[: sprite.shape[0] // 2, :, 3] > 0
    return upper.mean(axis=0) > 0.6


def blend_mound(sprite):
    """Toprak tümseğini arka plandaki zeminle kaynaştırır: renkler zeminden örneklenen renklere (parlaklık
    sırasıyla) eşlenir, dış kenardaki koyu hat kalkar ve kenar dama deseniyle seyrelir (yumuşak geçiş)."""
    bg = np.asarray(Image.open(BACKGROUND).convert("RGB")).astype(np.int32)
    x0, y0, x1, y1 = GROUND_BOX
    ground = bg[y0:y1, x0:x1].reshape(-1, 3)
    lum = lambda c: c[..., 0] * 0.3 + c[..., 1] * 0.59 + c[..., 2] * 0.11  # noqa: E731
    ground = ground[np.argsort(lum(ground))]
    tones = np.array([ground[int(q * (len(ground) - 1))] for q in (0.08, 0.3, 0.55, 0.78, 0.95)])

    out = sprite.copy()
    h, w = out.shape[:2]
    alpha = out[..., 3] > 0
    pole = pole_columns(out)
    mound = np.zeros_like(alpha)
    mound[h - MOUND_ROWS:] = alpha[h - MOUND_ROWS:]
    mound[:, pole] &= False
    # direğin dibindeki gölge: direk sütunlarının tümsek içindeki kısmı da toprağa döner (direk toprağa gömülü)
    mound[h - 4:] |= alpha[h - 4:]
    ys, xs = np.nonzero(mound)
    l = lum(out[ys, xs, :3].astype(np.float64))
    rank = np.argsort(np.argsort(l)) / max(1, len(l) - 1)
    out[ys, xs, :3] = tones[np.minimum((rank * len(tones)).astype(int), len(tones) - 1)]

    # dış kenar: saydama komşu tümsek pikselleri seyrelir (dama), bir içerideki halka da yarı seyrek
    def edge(m):
        a = out[..., 3] > 0
        n = np.zeros_like(a)
        n[1:] |= ~a[:-1]
        n[:, 1:] |= ~a[:, :-1]
        n[:, :-1] |= ~a[:, 1:]
        return m & n
    yy, xx = np.mgrid[0:h, 0:w]
    ring = edge(mound)
    out[ring & ((yy + xx) % 2 == 0), 3] = 0
    ring2 = edge(mound & (out[..., 3] > 0)) & ~ring
    out[ring2 & ((yy % 2 == 0) & (xx % 2 == 0)), 3] = 0
    # en alttaki iki satır da seyrek: zeminin içine gömülüyormuş gibi
    out[h - 1, ::2, 3] = 0
    return out


def extend_pole(sprite, total):
    """Direği ortasından uzatır: kapak üstte, tümsek altta kalır; aradaki gövde satırları tekrarlanır."""
    h = sprite.shape[0]
    if total <= h:
        return sprite
    mid0, mid1 = int(h * 0.35), int(h * 0.55)
    band = sprite[mid0:mid1].copy()
    band[..., 3] = np.where(pole_columns(sprite)[None, :], band[..., 3], 0)
    need = total - h
    fill = np.concatenate([band] * (need // len(band) + 1))[:need]
    return np.concatenate([sprite[:mid0], fill, sprite[mid0:]])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.int32)
    fg = ~background(rgb)
    parts = [c for c in components(fg)[:2]]
    parts.sort(key=lambda c: np.mean([x for _, x in c]))  # soldaki direk, sağdaki tahta
    pole_rgb, pole_m = cut(rgb, fg, parts[0])
    plank_rgb, plank_m = cut(rgb, fg, parts[1])

    ph, pw = pole_rgb.shape[:2]
    pole = downscale(pole_rgb, pole_m, max(1, int(round(pw * POLE_HEIGHT / ph))), POLE_HEIGHT)
    plank = downscale(plank_rgb, plank_m, *PLANK_SIZE)
    pal = palette_of([pole, plank], PALETTE)
    pole, plank = snap(pole, pal), snap(plank, pal)
    pole = blend_mound(pole)
    pole = extend_pole(pole, POLE_TOTAL)

    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray(pole, "RGBA").save(OUT / "tabela_direk.png")
    Image.fromarray(plank, "RGBA").save(OUT / "tabela_tahta.png")
    for key, text in LABELS.items():
        for sel in (0, 1):
            Image.fromarray(render_label(text, sel), "RGBA").save(OUT / f"tabela_yazi_{key}_{sel}.png")
    print(f"direk {pole.shape[1]}x{pole.shape[0]}, tahta {plank.shape[1]}x{plank.shape[0]}, {len(LABELS)} yazı -> {OUT}")

    if args.preview:
        sheet = Image.new("RGBA", (220, POLE_HEIGHT + 4), (90, 70, 60, 255))
        sheet.alpha_composite(Image.fromarray(pole, "RGBA"), (4, 2))
        for i, (key, sel) in enumerate([("devam", 1), ("level", 0), ("ayarlar", 0), ("cikis", 0)]):
            y = 8 + i * (PLANK_SIZE[1] + 3)
            sheet.alpha_composite(Image.fromarray(plank, "RGBA"), (14, y))
            lab = Image.fromarray(render_label(LABELS[key], sel), "RGBA")
            sheet.alpha_composite(lab, (14 + TEXT_CENTER_X - lab.width // 2, y + (PLANK_SIZE[1] - lab.height) // 2))
        sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
