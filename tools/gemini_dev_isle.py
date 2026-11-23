"""
Mavi Dev'in (kobalt mavisi, hulk tarzı mini-boss) Gemini sayfalarını oyuna hazır karelere çevirir.
Sayfalar 4x2 hücreli, gri zeminli, altında yazı şeridi olan sunum sayfaları:
  dev_yuruyus.png : idle_01-02, walk_01-06
  dev_ziplama.png : jump_01-08 (hazırlık, çömelme, kalkış, yükselme, gülle 1-2, yere çarpma, şok dalgası)
Gri zemin kenardan taşma dolgusuyla silinir; toz, taş, hız çizgisi ve şok yayı gibi gövdeden kopuk
parçalar atılır (şok dalgası oyunda ayrı efekt olarak doğar: dev_sok_*.png, pixelfx'ten).
Kareler sağa bakar; sola bakış oyunda flipX.

Hizalama: her karenin en alt noktası tuvalin altına (pivot = yer). Yatayda sayfa başına tek kaydırma
(ilk karenin başı tuvalin ortasında): hücrelerde çizildiği gibi oynar, titremez.

Kare sırası (CombatSetupBuilder.BuildBlueGiant ile aynı olmalı):
  0-1 bekleme, 2-7 yürüyüş, 8 hazırlık, 9 çömelme, 10 kalkış, 11 yükselme, 12-13 gülle, 14 yere çarpma

Kullanım (proje kökünden):
  python tools/gemini_dev_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_sprite_isle import apply_palette, build_palette
from gemini_yarasa_isle import bbox, components, crop

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "dev"
CANVAS_W, CANVAS_H = 128, 96
TARGET_HEIGHT = 74  # bekleme karesinin boyu: Gila (34) x ~2.2
COLS = [(5, 253), (259, 509), (514, 764), (770, 1019)]
ROWS = [(40, 370), (402, 736)]

# (sayfa, hücre sırası, silinecek efekt). jump_08 (şok dalgası) kullanılmaz: yay gövdeye karışmış,
# oyunda şok dalgası ayrı efekt, poz için çarpma karesi yeter.
FRAMES = [("yuruyus", i, None) for i in range(8)] + [("ziplama", i, None) for i in range(5)] + [
    ("ziplama", 5, "hiz"), ("ziplama", 6, "tas")]
LABELS = ["bekleme 0", "bekleme 1"] + [f"yuruyus {i}" for i in range(6)] + [
    "hazirlik", "comelme", "kalkis", "yukselme", "gulle 1", "gulle 2", "carpma"]


def cell(page, index):
    x0, x1 = COLS[index % 4]
    y0, y1 = ROWS[index // 4]
    return page[y0:y1, x0:x1].astype(np.int32)  # yazı şeridi hücrelerin altında, ROWS dışında


def foreground(rgb, strip_fx):
    """Kenardan taşma dolgusuyla gri zemini siler; kalan pikseller karakter (ve efekt parçaları)."""
    h, w = rgb.shape[:2]
    spread = rgb.max(axis=2) - rgb.min(axis=2)
    value = rgb.max(axis=2)
    bg_like = (spread < 16) & (value > 38) & (value < 105)
    bg = np.zeros((h, w), dtype=bool)
    q = deque((y, x) for y in range(h) for x in (0, w - 1) if bg_like[y, x])
    q.extend((y, x) for x in range(w) for y in (0, h - 1) if bg_like[y, x])
    for y, x in q:
        bg[y, x] = True
    while q:
        y, x = q.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and not bg[ny, nx] and bg_like[ny, nx]:
                bg[ny, nx] = True
                q.append((ny, nx))
    fg = ~bg
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    if strip_fx == "hiz":  # hız çizgileri: grimsi camgöbeği, gövdenin mavisinden ve siyah hattından ayrı
        fg &= ~((value > 45) & (value < 130) & (spread < 45))
    elif strip_fx == "tas":  # yere çarpınca sıçrayan taşlar: bej/kahve, gövdede sıcak renk yok
        fg &= ~((r > b + 25) & (r > 70))
    return fg


def downscale(rgb, fg, factor):
    h, w = int(rgb.shape[0] / factor), int(rgb.shape[1] / factor)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        for x in range(w):
            y0, y1 = int(y * factor), int((y + 1) * factor)
            x0, x1 = int(x * factor), int((x + 1) * factor)
            m = fg[y0:y1, x0:x1]
            if m.mean() >= 0.5:
                out[y, x, :3] = np.median(rgb[y0:y1, x0:x1][m], axis=0)
                out[y, x, 3] = 255
    return out


def body_only(sprite):
    """Gövde en büyük bileşen; ona yakın büyüklükte olmayan kopuk parçalar (toz, taş) atılır."""
    parts = components(sprite[..., 3] > 0)
    out = np.zeros_like(sprite)
    def thin(part):  # hız çizgisi: ince uzun
        xs = [x for _, x in part]
        return max(xs) - min(xs) < 5
    keep = [parts[0]] + [p for p in parts[1:] if len(p) >= max(30, len(parts[0]) * 0.08) and not thin(p)]
    for part in keep:
        for y, x in part:
            out[y, x] = sprite[y, x]
    return out


def strip_thin(sprite, passes=3):
    """Efektten kalan 1 piksellik çizgi ve halkaları (taş hattı, hız çizgisi) aşındırır: 3x3 komşuluğunda
    4'ten az dolu piksel olan pikseller silinir. Gövdenin kenarı kalın kütleye yaslandığı için kalır."""
    out = sprite.copy()
    for _ in range(passes):
        a = (out[..., 3] > 0).astype(np.int32)
        p = np.pad(a, 1)
        n = sum(p[1 + dy:1 + dy + a.shape[0], 1 + dx:1 + dx + a.shape[1]]
                for dy in (-1, 0, 1) for dx in (-1, 0, 1)) - a
        out[(a == 1) & (n < 4)] = 0
    return out


def head_center_x(sprite):
    alpha = sprite[..., 3] > 0
    rows = np.where(alpha.any(axis=1))[0]
    top = alpha[rows[0]:rows[0] + max(1, (rows[-1] - rows[0]) // 5)]
    return np.where(top)[1].mean()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {n: np.array(Image.open(SOURCE_DIR / f"dev_{n}.png").convert("RGB")) for n in ("yuruyus", "ziplama")}

    # Ölçek: ilk bekleme karesinin kaynak boyu -> TARGET_HEIGHT.
    first = cell(pages["yuruyus"], 0)
    ys = np.where(foreground(first, None).any(axis=1))[0]
    factor = (ys[-1] - ys[0] + 1) / TARGET_HEIGHT
    print(f"ölçek: {factor:.2f} kaynak piksel = 1 oyun pikseli")

    frames = []
    for page, index, strip in FRAMES:
        rgb = cell(pages[page], index)
        sprite = downscale(rgb, foreground(rgb, strip), factor)
        if strip:
            sprite = strip_thin(sprite)
        frames.append((page, body_only(sprite)))

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

    print("kare:", len(canvases), "boy:", [bbox(c)[1] - bbox(c)[0] + 1 for c in canvases])

    if not args.preview:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        for i, canvas in enumerate(canvases):
            Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"dev_{i}.png")
        return

    scale, cols = 3, 8
    n = len(canvases)
    out = Image.new("RGBA", (cols * CANVAS_W * scale, ((n + cols - 1) // cols) * CANVAS_H * scale), (70, 110, 130, 255))
    draw = ImageDraw.Draw(out)
    for i, c in enumerate(canvases):
        x, y = (i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale
        out.alpha_composite(Image.fromarray(c, "RGBA").resize((CANVAS_W * scale, CANVAS_H * scale), Image.NEAREST), (x, y))
        draw.line([(x + CANVAS_W * scale // 2, y + CANVAS_H * scale - 6), (x + CANVAS_W * scale // 2, y + CANVAS_H * scale)], fill=(255, 0, 0, 255))
        draw.text((x + 2, y + 2), f"{i} {LABELS[i]}", fill=(255, 255, 0, 255))
    out.save(args.preview)


if __name__ == "__main__":
    main()
