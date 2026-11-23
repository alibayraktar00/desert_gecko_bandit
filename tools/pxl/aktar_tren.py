"""Tren bölümünün görsellerini Unity'ye aktarır.

- pxl çıktıları (tools/pxl/out): tren sprite'ları tek tek, tekerlek animasyonları kare kare.
- pixelfx sprite sheet'leri (~/.claude/pixelfx/records): kare kare, efektin orijini (pivot) alt ortaya gelecek şekilde.
  Props klasöründeki her şey alt orta pivotla içe aktarılır (SpriteImportRules), böylece Unity'de efekt orijini
  doğrudan nesnenin konumu olur. Tuval, tüm karelerin dolu kısmına göre kırpılır.

Çalıştır: python tools/pxl/aktar_tren.py   (önce: pxl build tools/pxl/tren.px.js ve pixelfx --sheet)
"""
import json
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PXL_OUT = os.path.join(ROOT, "tools", "pxl", "out")
FX = os.path.join(os.path.expanduser("~"), ".claude", "pixelfx", "records")
DEST = os.path.join(ROOT, "Assets", "Sprites", "Environment", "Props", "Tren")

# (pixelfx id, oyundaki ad, kaç karede bir alınır)
EFFECTS = [
    ("fire/train-smoke", "tren_duman", 2),
    ("explosions/big-explosion", "tren_patlama", 2),
    ("explosions/dirt-explosion", "tren_toz", 2),
    ("status/stunned", "sersem", 2),
]


def save(img, name):
    img.save(os.path.join(DEST, name + ".png"))


def export_pxl():
    for f in os.listdir(os.path.join(PXL_OUT, "sprites")):
        if f.startswith("tren_") and f.endswith(".png"):
            save(Image.open(os.path.join(PXL_OUT, "sprites", f)).convert("RGBA"), f[:-4])
    for f in os.listdir(os.path.join(PXL_OUT, "anims")):
        if f.startswith("tren_") and f.endswith(".png"):
            strip = Image.open(os.path.join(PXL_OUT, "anims", f)).convert("RGBA")
            frames = json.load(open(os.path.join(PXL_OUT, "anims", f[:-4] + ".json")))["frames"]
            for i, fr in enumerate(frames.values()):
                r = fr["frame"]
                save(strip.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"])), f"{f[:-4]}_{i}")


# Lokomotifin parçaları (kaza: dinamit altında patlar). Kesik kenarlar dişli ve isli; alt orta pivot.
# (ad, x0, y0, x1, y1) lokomotif sprite'ında piksel. TrainCrash bunları lokomotifin yerinde açıp savurur.
LOCO_PIECES = [
    ("kabin", 0, 6, 110, 134),
    ("kazan_arka", 104, 56, 204, 122),
    ("kazan_orta", 204, 30, 300, 122),
    ("kazan_on", 300, 0, 392, 122),
    ("mahmuz", 380, 116, 416, 160),
    ("sasi_arka", 0, 118, 200, 136),
    ("sasi_on", 200, 118, 392, 136),
]


def export_loco_pieces():
    import random
    rnd = random.Random(7)
    loco = Image.open(os.path.join(PXL_OUT, "sprites", "tren_lokomotif.png")).convert("RGBA")
    soot = (20, 12, 16, 255)
    for name, x0, y0, x1, y1 in LOCO_PIECES:
        piece = loco.crop((x0, y0, x1, y1))
        px = piece.load()
        w, h = piece.size
        cut = {"l": x0 > 0, "r": x1 < loco.width, "t": y0 > 0, "b": y1 < loco.height}
        # Dişli kırık: kesik kenarlardan rastgele derinlikte kemir, hemen içini isle
        for side, on in cut.items():
            if not on:
                continue
            n = h if side in "lr" else w
            depth = 0
            for i in range(n):
                depth = max(0, min(5, depth + rnd.choice((-2, -1, 0, 1, 2))))
                for d in range(depth + 2):
                    x, y = {"l": (d, i), "r": (w - 1 - d, i), "t": (i, d), "b": (i, h - 1 - d)}[side]
                    if 0 <= x < w and 0 <= y < h and px[x, y][3]:
                        px[x, y] = (0, 0, 0, 0) if d < depth else soot
        save(piece, f"tren_lokomotif_{name}")
    return LOCO_PIECES


def export_effect(fx_id, name, step):
    folder = os.path.join(FX, *fx_id.split("/"))
    base = fx_id.split("/")[1]
    meta = json.load(open(os.path.join(folder, base + ".json")))["meta"]["pixelfx"]
    sheet = Image.open(os.path.join(folder, base + ".png")).convert("RGBA")
    fw, fh = meta["frameSize"]
    px, py = meta["pivot"]
    cols = meta["columns"]
    frames = []
    for i in range(0, meta["frames"], step):
        x, y = (i % cols) * fw, (i // cols) * fh
        frames.append(sheet.crop((x, y, x + fw, y + py)))  # orijinin altı (zemin) atılır
    # Dolu kısmın birleşimi: orijine göre simetrik genişlik, üstten kırp.
    boxes = [f.getbbox() for f in frames if f.getbbox()]
    left = min(b[0] for b in boxes)
    right = max(b[2] for b in boxes)
    top = min(b[1] for b in boxes)
    half = max(px - left, right - px)
    for i, f in enumerate(frames):
        out = Image.new("RGBA", (half * 2, py - top), (0, 0, 0, 0))
        out.alpha_composite(f.crop((0, top, fw, py)), (half - px, 0))
        save(out, f"{name}_{i}")
    return len(frames), meta["fps"] / step


if __name__ == "__main__":
    os.makedirs(DEST, exist_ok=True)
    export_pxl()
    export_loco_pieces()
    for fx_id, name, step in EFFECTS:
        n, fps = export_effect(fx_id, name, step)
        print(f"{name}: {n} kare, {fps:g} fps")
