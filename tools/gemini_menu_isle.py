"""
Ana menü görsellerini (Gemini, 2752x1536, sahte pikseller ~5.5 px) oyuna hazır sprite'lara çevirir.

  menu_arka_plan.jpg -> menu_arka_plan.png : tam ekran çöl, 640x360 (referans çözünürlük), şeffaflık yok
  menu_tabela.jpg    -> menu_tabela.png    : zincirli başlık tabelası, gri zemin silinir, ~220 px genişlik
  menu_silah_el.jpg  -> menu_silah_el.png  : Gila'nın eli ve revolver, gri zemin silinir, ~380 px genişlik

Gri zemin kenardan taşma dolgusuyla silinir (tools/gemini_dev_isle.py ile aynı yöntem), sonra her
oyun pikseli kaynaktaki bloğun medyanı olur ve renkler bir palete indirilir (JPEG gürültüsü gider).

Kullanım (proje kökünden):
  python tools/gemini_menu_isle.py [--preview onizleme.png]
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu"

# factor: kaynakta bir oyun pikselinin kaç piksel olduğu. cutout: gri zemini sil.
ITEMS = [
    {"src": "menu_arka_plan.jpg", "out": "menu_arka_plan.png", "size": (640, 360), "cutout": False, "palette": 48},
    {"src": "menu_tabela.jpg", "out": "menu_tabela.png", "width": 220, "cutout": True, "palette": 32},
    {"src": "menu_silah_el.jpg", "out": "menu_silah_el.png", "width": 380, "cutout": True, "palette": 40},
]


def gray_background(rgb):
    """Kenara bağlı düz gri alan (Gemini'nin #808080 zemini ve JPEG sapmaları)."""
    h, w = rgb.shape[:2]
    spread = rgb.max(axis=2) - rgb.min(axis=2)
    value = rgb.max(axis=2)
    bg_like = (spread < 18) & (value > 95) & (value < 170)
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
    # Kenara bağlı olmayan ama koyu hatla çevrili büyük gri alanlar da zemindir (ör. revolverin üst köprüsüyle
    # tamburu arasındaki çerçeve penceresi): oradan arkadaki gökyüzü görünmeli.
    seen = bg.copy()
    for sy, sx in zip(*np.nonzero(bg_like & ~bg)):
        if seen[sy, sx]:
            continue
        comp, q = [(sy, sx)], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and not seen[ny, nx] and bg_like[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
                    comp.append((ny, nx))
        if len(comp) >= ENCLOSED_MIN:
            ys, xs = zip(*comp)
            bg[list(ys), list(xs)] = True
    return bg


ENCLOSED_MIN = 1500  # kaynak pikseli: bundan büyük kapalı gri alan zemin sayılır (küçük gri parlamalar kalır)


def downscale(rgb, fg, out_w, out_h):
    """Her hedef piksel kaynak bloğunun medyanı; bloğun yarısından azı ön plansa şeffaf."""
    h, w = rgb.shape[:2]
    fy, fx = h / out_h, w / out_w
    out = np.zeros((out_h, out_w, 4), dtype=np.uint8)
    for y in range(out_h):
        y0, y1 = int(y * fy), max(int(y * fy) + 1, int((y + 1) * fy))
        for x in range(out_w):
            x0, x1 = int(x * fx), max(int(x * fx) + 1, int((x + 1) * fx))
            m = fg[y0:y1, x0:x1]
            if m.mean() >= 0.5:
                out[y, x, :3] = np.median(rgb[y0:y1, x0:x1][m], axis=0)
                out[y, x, 3] = 255
    return out


def quantize(sprite, colors):
    opaque = sprite[..., 3] > 0
    pixels = sprite[opaque][:, :3]
    strip = Image.fromarray(pixels.reshape(1, -1, 3).astype(np.uint8), "RGB")
    pal = np.array(strip.quantize(colors=colors, method=Image.Quantize.MEDIANCUT).getpalette()[:colors * 3],
                   dtype=np.int32).reshape(-1, 3)
    nearest = ((pixels.astype(np.int32)[:, None, :] - pal[None]) ** 2).sum(axis=2).argmin(axis=1)
    out = sprite.copy()
    out[opaque, :3] = pal[nearest]
    return out


def drop_specks(sprite, min_size=12):
    """Gövdeye bağlı olmayan küçük kırıntıları (JPEG lekesi) siler."""
    alpha = sprite[..., 3] > 0
    h, w = alpha.shape
    seen = np.zeros_like(alpha)
    for sy in range(h):
        for sx in range(w):
            if not alpha[sy, sx] or seen[sy, sx]:
                continue
            comp, q = [], deque([(sy, sx)])
            seen[sy, sx] = True
            while q:
                y, x = q.popleft()
                comp.append((y, x))
                for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= ny < h and 0 <= nx < w and alpha[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            if len(comp) < min_size:
                for y, x in comp:
                    sprite[y, x, 3] = 0
    return sprite


def process(item):
    rgb = np.asarray(Image.open(SOURCE_DIR / item["src"]).convert("RGB")).astype(np.int32)
    if item["cutout"]:
        fg = ~gray_background(rgb)
        ys, xs = np.nonzero(fg)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        rgb, fg = rgb[y0:y1, x0:x1], fg[y0:y1, x0:x1]
        out_w = item["width"]
        out_h = round(out_w * rgb.shape[0] / rgb.shape[1])
    else:
        fg = np.ones(rgb.shape[:2], dtype=bool)
        out_w, out_h = item["size"]
    sprite = quantize(downscale(rgb, fg, out_w, out_h), item["palette"])
    if item["cutout"]:
        sprite = drop_specks(sprite)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    Image.fromarray(sprite, "RGBA").save(OUT_DIR / item["out"])
    print(f"{item['out']}: {out_w}x{out_h}")
    return sprite


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()
    sprites = [process(i) for i in ITEMS]
    if args.preview:
        bg = Image.fromarray(sprites[0], "RGBA")
        sign = Image.fromarray(sprites[1], "RGBA")
        gun = Image.fromarray(sprites[2], "RGBA")
        bg.alpha_composite(sign, (14, 4))
        bg.alpha_composite(gun, (640 - gun.width + 10, 360 - gun.height + 6))
        bg.resize((1280, 720), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
