"""
Siber Hatip'in (havada süzülen, ses dalgası atan kabanlı düşman) Gemini sayfalarını oyuna hazır karelere çevirir.
Sayfalar 4x2 hücreli, gri (ya da gri damalı) zeminli, altında yazı şeridi olan sunum sayfaları:
  siber_suzulme.webp  : motion_01-08 (süzülme döngüsü)
  siber_saldiri.webp  : attack_01-08 (hazırlık, şarj, bağırış, halka patlaması ... toparlanma)
  siber_olum.webp     : death_01-08 (irtifa kaybı, düşüş, diz çökme, yıkılma, serilme, başparmak, hareketsiz)
  siber_galibiyet.webp: victory_01-08 (sakinleşme, gururlu duruş, kollar kavuşuk, yumruk, coşku, indirme, final)
Zemin kenardan taşma dolgusuyla silinir; kıvılcım, toz ve ses halkası gibi gövdeden kopuk parçalar atılır.
Ses halkası oyunda ayrı mermi olarak doğar: siber_dalga.png bu betikte çizilir (draw_wave).
Mermi yiyince açılan kalkan: siber_kalkan.png (draw_shield).
Kareler sağa bakar; sola bakış oyunda flipX.

Hizalama: her karenin en alt noktası tuvalin altına (pivot = botların altı). Yatayda sayfa başına tek kaydırma
(ilk karenin başı tuvalin ortasında). Süzülme salınımı oyunda kodla yapılır.

Kare sırası (CombatSetupBuilder.BuildCyberOrator ile aynı olmalı):
  0-7 süzülme, 8 hazırlık (attack_01, el uzanmış), 9 bağırış (attack_03), 10-17 ölüm, 18-25 galibiyet

Kullanım (proje kökünden):
  python tools/gemini_siber_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_dev_isle import body_only, downscale, head_center_x, strip_thin
from gemini_sprite_isle import apply_palette, build_palette
from gemini_yarasa_isle import bbox, components, crop

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "siber"
CANVAS_W, CANVAS_H = 96, 64
TARGET_HEIGHT = 52  # süzülme karesinin boyu: Gila (34) x ~1.5
COLS = [(4, 254), (258, 510), (514, 766), (770, 1020)]
ROWS = [(112, 382), (446, 708)]

ERODE = 4  # kaynak piksel: dış hattaki bu genişliğe kadar boşluklar dolguyu durdurur
# attack_03: ağzın önündeki ses halkaları (hücre koordinatı, x0, y0, x1, y1): oyunda halka ayrı mermi.
RING_BOX = (170, 0, 252, 110)

# (sayfa, hücre, efekt silme). attack_02 (şarj) kullanılmaz: parıltı gövdeyi örtüyor, uyarıyı kod yapar.
# attack_04-08 halkalar gövdeye karışmış; bağırış karesi atış boyunca tutulur.
FRAMES = ([("suzulme", i, None) for i in range(8)]
          + [("saldiri", 0, None), ("saldiri", 2, "halka")]
          + [("olum", i, "tas" if i == 2 else None) for i in range(8)]
          + [("galibiyet", i, None) for i in range(8)])
LABELS = ([f"suzulme {i}" for i in range(8)] + ["hazirlik", "bagiris"]
          + [f"olum {i}" for i in range(8)] + [f"galibiyet {i}" for i in range(8)])


def cell(page, index):
    x0, x1 = COLS[index % 4]
    y0, y1 = ROWS[index // 4]
    return page[y0:y1, x0:x1].astype(np.int32)


def fg_mask(rgb, strip, page_index=None):
    """Kenardan taşma dolgusuyla zemini siler. Damalı sayfalarda (ölüm, galibiyet) koyu gri pantolon zeminin
    karelerinin rengiyle aynı: dolgu dış hattaki küçük boşluklardan bacaklara sızmasın diye önce zemin
    aşındırılır (dar boşluk kapanır), dolgu yapılır, sonra kenara kadar geri büyütülür."""
    h, w = rgb.shape[:2]
    spread = rgb.max(axis=2) - rgb.min(axis=2)
    value = rgb.max(axis=2)
    bg_like = (spread < 16) & (value > 30) & (value < 105)
    core = ndimage.binary_erosion(bg_like, iterations=ERODE, border_value=1)
    labels, _ = ndimage.label(core)
    edge = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    seeds = np.isin(labels, list(edge))
    bg = ndimage.binary_dilation(seeds, iterations=ERODE + 1) & bg_like
    fg = ~bg
    if strip == "tas":  # yere çarpınca sıçrayan toz: bej/kahve, gövdede sıcak renk yalnız kazakta (o açık krem)
        r, b = rgb[..., 0], rgb[..., 2]
        fg &= ~((r > b + 25) & (r > 70) & (value < 170))
    return fg


def draw_wave():
    """Ses halkası mermisi: sağa açılan üç iç içe yay (sayfadaki halkalar 1 piksele inince kayboluyor, elle çizilir).
    Kalın siyah hat yerine koyu mavi dış kenar: oyunun hatlı stiline uyar, rengi sayfanın turkuazından."""
    w, h = 18, 26
    out = np.zeros((h, w, 4), dtype=np.uint8)
    cy = (h - 1) / 2
    rings = [(5.0, 1.6), (10.0, 1.6), (15.0, 1.6)]  # (yarıçap, kalınlık), sol kenardaki merkezden
    core, mid, edge = (214, 250, 255), (86, 214, 255), (24, 92, 168)
    for y in range(h):
        for x in range(w):
            dx, dy = x + 0.5, y - cy
            r = (dx * dx + dy * dy) ** 0.5
            if dx < abs(dy) * 0.55:  # yalnız öne bakan yay
                continue
            for i, (radius, thick) in enumerate(rings):
                d = abs(r - radius)
                if d <= thick + 0.9:
                    col = core if d <= thick * 0.45 else mid if d <= thick else edge
                    if i == 0 and col == core:
                        col = mid  # içteki halka daha sönük: dalga dışa doğru güçlenir
                    out[y, x, :3] = col
                    out[y, x, 3] = 255
    return out


def draw_shield():
    """Kalkan: Siber Hatip'i saran turkuaz enerji kabuğu. Parlak dış halka, içi soluk ve petek desenli;
    oyunda üstüne yarı saydam biner, kod nabız gibi atmasını sağlar."""
    w, h = 46, 62
    out = np.zeros((h, w, 4), dtype=np.uint8)
    cx, cy, rx, ry = (w - 1) / 2, (h - 1) / 2, w / 2 - 1, h / 2 - 1
    core, mid, edge = (214, 250, 255), (86, 214, 255), (24, 92, 168)
    for y in range(h):
        for x in range(w):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if d > 1.0:
                continue
            ring = 1.0 - d  # kenara yaklaştıkça 0
            if ring < 0.07:
                col, a = edge, 255
            elif ring < 0.14:
                col, a = core, 255
            elif ring < 0.2:
                col, a = mid, 200
            elif (x + (y // 4) * 2) % 8 == 0 and y % 4 == 0:
                col, a = mid, 150  # petek noktaları
            else:
                col, a = mid, 45
            out[y, x, :3] = col
            out[y, x, 3] = a
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {n: np.array(Image.open(SOURCE_DIR / f"siber_{n}.webp").convert("RGB"))
             for n in ("suzulme", "saldiri", "olum", "galibiyet")}

    first = cell(pages["suzulme"], 0)
    ys = np.where(body_only_mask(first).any(axis=1))[0]
    factor = (ys[-1] - ys[0] + 1) / TARGET_HEIGHT
    print(f"ölçek: {factor:.2f} kaynak piksel = 1 oyun pikseli")

    frames = []
    for page, index, strip in FRAMES:
        rgb = cell(pages[page], index)
        fg = fg_mask(rgb, strip)
        if strip == "halka":
            x0, y0, x1, y1 = RING_BOX
            fg[y0:y1, x0:x1] = False
        sprite = downscale(rgb, fg, factor)
        if strip == "tas":
            sprite = strip_thin(sprite, passes=1)
        frames.append((page, body_only(sprite)))

    wave = draw_wave()

    palette = build_palette([crop(f) for _, f in frames])
    dx_of = {}
    canvases = []
    for i, (page, f) in enumerate(frames):
        if page not in dx_of:
            dx_of[page] = int(round(CANVAS_W / 2 - head_center_x(f)))
        y0, y1, x0, x1 = bbox(f)
        sprite = apply_palette(f, palette)[y0:y1 + 1]
        canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
        top = CANVAS_H - sprite.shape[0]
        for y in range(sprite.shape[0]):
            for x in range(sprite.shape[1]):
                cx = x + dx_of[page]
                if sprite[y, x, 3] and 0 <= top + y < CANVAS_H and 0 <= cx < CANVAS_W:
                    canvas[top + y, cx] = sprite[y, x]
        if (canvas[..., 3] > 0).sum() < (f[..., 3] > 0).sum():
            print(f"  uyarı: {LABELS[i]} tuvale tam sığmadı")
        canvases.append(canvas)

    print("kare:", len(canvases), "boy:", [bbox(c)[1] - bbox(c)[0] + 1 for c in canvases], "dalga:", wave.shape[:2])

    if not args.preview:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        for i, canvas in enumerate(canvases):
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"siber_{i}.png")
        Image.fromarray(wave, "RGBA").save(OUT_DIR / "siber_dalga.png")
        Image.fromarray(draw_shield(), "RGBA").save(OUT_DIR / "siber_kalkan.png")
        return

    scale, cols = 3, 8
    n = len(canvases) + 1
    out = Image.new("RGBA", (cols * CANVAS_W * scale, ((n + cols - 1) // cols) * CANVAS_H * scale), (70, 110, 130, 255))
    draw = ImageDraw.Draw(out)
    for i, c in enumerate(canvases + [wave]):
        x, y = (i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale
        img = Image.fromarray(c, "RGBA")
        out.alpha_composite(img.resize((img.width * scale, img.height * scale), Image.NEAREST), (x, y))
        draw.line([(x + CANVAS_W * scale // 2, y + CANVAS_H * scale - 6), (x + CANVAS_W * scale // 2, y + CANVAS_H * scale)], fill=(255, 0, 0, 255))
        draw.text((x + 2, y + 2), f"{i} {(LABELS + ['dalga'])[i]}", fill=(255, 255, 0, 255))
    out.save(args.preview)


def body_only_mask(rgb):
    """Ölçek için: zemin silinmiş hücrede en büyük bileşen (numara etiketi, kıvılcım sayılmaz)."""
    fg = fg_mask(rgb, None)
    small = fg[::4, ::4]
    parts = components(small)
    mask = np.zeros_like(small)
    for y, x in parts[0]:
        mask[y, x] = True
    return mask.repeat(4, axis=0).repeat(4, axis=1)


if __name__ == "__main__":
    main()
