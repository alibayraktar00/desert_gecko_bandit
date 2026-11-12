"""
Tavandan inen kapı sayfasını (gri zemin, ceiling_gate_00..05) dövüş odası kapısının parçalarına ayırır:
  tavan_kapi_ust.png    tavana gömülü taş başlık (kare 00): kapı bunun arkasından iner
  tavan_kapi_kanat.png  dikenli ahşap kanat (kare 04): ortası dikey tekrar eder, odanın boyuna uzar
  kapi_toz.png          kapı yere çarpınca kalkan toz (kare 05)

Kaynak: Assets/Sprites/_Gelen/tavan_kapi.png. Sahte pikseller (3 px) gerçek ızgaraya indirilir.
Kullanım (proje kökünden):  python tools/tavan_kapi_isle.py [--preview onizleme.png]
Gereken paketler: pillow, numpy
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets" / "Sprites" / "_Gelen" / "tavan_kapi.png"
OUTPUT_DIR = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
PIXEL = 3

# Kaynakta kabaca kutular (x0, y0, x1, y1). Alt sınırlar zemin çizgisinin (y=711) üstünde kalır.
# largest: sadece en büyük parça (kanadın yanındaki toz kırıntıları girmesin)
PARTS = [
    # Uzun taş blok ilk sayfadan (kare 00); alt kenarı zemin çizgisine (y=253) değdiği için 251'de kesilir.
    {"name": "tavan_kapi_ust", "source": "tavan_kapi_1.png", "box": (100, 75, 305, 251), "largest": True},
    {"name": "tavan_kapi_kanat", "box": (425, 528, 600, 708), "largest": True, "no_dust": True},
    {"name": "kapi_toz", "box": (690, 550, 950, 708), "largest": False},
]


def background(rgb):
    """Düz gri zemin (78,78,78) ve etiket yazıları."""
    lo, hi = rgb.min(axis=2), rgb.max(axis=2)
    gray = (hi - lo < 10) & (lo > 62) & (hi < 96)
    white = lo > 200
    return gray | white


def downscale(rgb, pixel):
    bg = background(rgb)
    h, w = rgb.shape[0] // pixel, rgb.shape[1] // pixel
    out = np.zeros((h, w, 4), dtype=np.uint8)
    lo, hi = int(pixel * 0.25), max(int(pixel * 0.75), int(pixel * 0.25) + 1)
    for y in range(h):
        for x in range(w):
            block = rgb[y * pixel:(y + 1) * pixel, x * pixel:(x + 1) * pixel]
            fg = ~bg[y * pixel:(y + 1) * pixel, x * pixel:(x + 1) * pixel]
            if fg.mean() < 0.5:
                continue
            core = block[lo:hi + 1, lo:hi + 1][fg[lo:hi + 1, lo:hi + 1]]
            out[y, x, :3] = np.median(core if len(core) else block[fg], axis=0)
            out[y, x, 3] = 255
    return out


def largest_component(alpha):
    h, w = alpha.shape
    labels = np.zeros((h, w), dtype=np.int32)
    best, best_size = 0, 0
    label = 0
    for sy, sx in zip(*np.nonzero(alpha)):
        if labels[sy, sx]:
            continue
        label += 1
        labels[sy, sx] = label
        queue, size = deque([(sy, sx)]), 0
        while queue:
            y, x = queue.popleft()
            size += 1
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and alpha[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = label
                    queue.append((ny, nx))
        if size > best_size:
            best, best_size = label, size
    return labels == best


def trim(rgba):
    alpha = rgba[..., 3] > 0
    rows, cols = np.where(alpha.any(axis=1))[0], np.where(alpha.any(axis=0))[0]
    return rgba[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path, help="parçaların büyütülmüş önizlemesi")
    args = parser.parse_args()

    sprites = []
    for part in PARTS:
        source = SOURCE.parent / part["source"] if "source" in part else SOURCE
        rgb = np.array(Image.open(source).convert("RGB"), dtype=np.int32)
        x0, y0, x1, y1 = part["box"]
        rgba = downscale(rgb[y0:y1, x0:x1], PIXEL)
        if part.get("no_dust"):
            rgba[rgba[..., :3].mean(axis=2) > 130] = 0  # açık bej toz ahşapta yok
        if part["largest"]:
            rgba[~largest_component(rgba[..., 3] > 0)] = 0
        rgba = trim(rgba)
        path = OUTPUT_DIR / f"{part['name']}.png"
        Image.fromarray(rgba, "RGBA").save(path)
        print(f"  {path.relative_to(ROOT)}  ({rgba.shape[1]}x{rgba.shape[0]})")
        sprites.append(rgba)

    if args.preview:
        scale = 6
        width = sum(s.shape[1] * scale + 20 for s in sprites)
        height = max(s.shape[0] * scale for s in sprites)
        sheet = Image.new("RGBA", (width, height), (70, 70, 90, 255))
        x = 0
        for s in sprites:
            img = Image.fromarray(s, "RGBA").resize((s.shape[1] * scale, s.shape[0] * scale), Image.NEAREST)
            sheet.alpha_composite(img, (x, 0))
            x += s.shape[1] * scale + 20
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
