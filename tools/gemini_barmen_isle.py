"""
Barmen/Fedai'nin Gemini sayfalarını (yürüyüş, fıçı fırlatma, tüfek, sevinç, ölüm, fıçı kırılma) oyuna hazır
karelere çevirir.

Sayfalar 4x2 düzeninde, zemin düz koyu gri. Her karede yalnız gövde (en büyük bağlı parça) alınır: yazılar,
hız çizgileri, havadaki fıçı ve boş kovan elenir. Fıçı ayrı bir mermi olduğu için kendi karesi ve kırılma
kareleri ayrıca kaydedilir. Kareler sağa bakar; sola bakış oyunda flipX ile.

Kullanım (proje kökünden):
  python tools/gemini_barmen_isle.py [--preview onizleme.png]
"""
import argparse
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_dusman_isle import body_center_x
from gemini_serif_isle import FLAME_COLORS, downscale, label
from gemini_sprite_isle import apply_palette, build_palette

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "barmen"
CANVAS_W, CANVAS_H = 128, 96  # fıçı baş üstündeyken kare 64'ü aşar
TARGET_HEIGHT = 56            # bekleme karesinde boy (oyun pikseli); Gila 34, şerif 47
ROW_BANDS = [(79, 338), (405, 727)]  # üst başlık şeridi 78. satırda biter, kafalar ona değiyor
MIN_AREA = 15000              # gövde; yazılar ve kopuk parçalar bundan küçük

# (sayfa, satır, sütun). Sıra koddaki indekslerle aynı (CombatSetupBuilder.BuildBarman).
FRAMES = (
    [("bekleme", ("yuruyus", 0, c)) for c in (0, 1)]                 # 0-1
    + [("yuruyus", ("yuruyus", r, c)) for r, c in ((0, 2), (0, 3), (1, 0), (1, 1), (1, 2), (1, 3))]  # 2-7
    + [("kaldirma", ("firlatma", 0, c)) for c in range(4)]           # 8-11: duruş, kaldırma, baş üstü, geriye esneme
    + [("savurma", ("firlatma", 1, 0))]                              # 12: fıçı elden çıkar
    + [("takip", ("firlatma", 1, 2)), ("denge", ("firlatma", 1, 3))]  # 13-14 (uçuş karesinde fıçı gövdeye yapışık, atlanır)
    + [("cekis", ("tufek", 0, c)) for c in range(4)]                 # 15-18: uzanma, çekme, öne getirme, hazırlık
    + [("ates", ("tufek", 1, c)) for c in range(4)]                  # 19-22: ateş, geri tepme, kurma, tetikte
    # 23-25: kutlama, şerefe, yudumlama (kahkaha karelerinde yazılar çizmelere yapışık, kullanılmıyor)
    + [("sevinc", ("sevinc", r, c)) for r, c in ((1, 1), (0, 2), (0, 3))]
    # 26-31: sarsılma, devrilme, yere çarpma, sarsıntı, serilme, hareketsiz (1. kare kanlı, 8. kare aynı duruş)
    + [("olum", ("olum", r, c)) for r, c in ((0, 1), (0, 2), (0, 3), (1, 0), (1, 1), (1, 2))]
    # 32-37 hasar (tüfekli): geriye sendeleme, denge kaybı, büzülme, duraksama, doğrulma, dengeyi sağlama
    # (1. karede köşedeki Gila referansı gövdeye yapışık, kullanılmıyor)
    + [("hasar", ("hasar", r, c)) for r, c in ((0, 1), (0, 2), (0, 3), (1, 0), (1, 1), (1, 2))]
)
READY_FRAME = 22              # tetikte: tüfekli yürüyüşün gövdesi
WALK_FRAMES = range(2, 8)     # fıçılı yürüyüş: bacakları alınır
LEG_ROW = 85                  # tuvalde fıçının bittiği satır; altı yalnız bacak
# Yukarı nişan kareleri bunlardan üretilir: (kare, belden yukarısının geriye yatma açısı). Geri tepme karesinde
# namlu zaten havada, az döner.
AIM_FRAMES = ((18, 45), (19, 45), (20, 18))
WAIST = (60, 70)              # dönme ekseni: tuvalde kemerin arka ucu (x, y)
# Dik yukarı nişan (hazırlık ve ateş karelerinden): gövde STEEP_TORSO kadar yatar, öndeki kol ve tüfek
# (GUN_CUT sütunundan sağı) GUN_PIVOT çevresinde STEEP_GUN kadar daha kalkar: namlu dimdik yukarı bakar.
STEEP_FRAMES = (18, 19)
STEEP_TORSO, STEEP_GUN = 40, 45
GUN_CUT, GUN_PIVOT = 73, (72, 65)

# Fıçı kırılma sayfası: (x0, x1, y0, y1, fıçının merkezi x, zemin y). İlk kare (temas) uçan fıçının kendisi olur.
BARREL_CELL = (40, 250, 110, 306)
BREAK_CELLS = [
    (300, 500, 80, 306, 386, 302), (510, 760, 80, 306, 640, 302), (762, 1012, 80, 306, 886, 302),
    (15, 285, 440, 700, 150, 698), (290, 490, 440, 700, 395, 698), (525, 770, 440, 700, 650, 698),
    (790, 985, 440, 700, 890, 698),
]
BREAK_W, BREAK_H = 96, 64


def foreground(image):
    """Zemin: sayfanın kenar rengine yakın piksellerin en büyük bağlı bölgesi."""
    near = np.abs(image - image[400, 5]).max(axis=2) < 22
    lab, boxes = label(near)
    return lab != 1 + max(range(len(boxes)), key=lambda i: boxes[i][4])


def page_frames(image, fg):
    """Her satır bandında gövdeleri soldan sağa döndürür: (rgb, maske)."""
    rows = []
    for y0, y1 in ROW_BANDS:
        lab, boxes = label(fg[y0:y1])
        row = []
        for i in sorted((i for i, b in enumerate(boxes) if b[4] > MIN_AREA), key=lambda i: boxes[i][0]):
            bx0, by0, bx1, by1, _ = boxes[i]
            row.append((image[y0 + by0:y0 + by1, bx0:bx1], lab[by0:by1, bx0:bx1] == i + 1))
        rows.append(row)
    return rows


def place_on_canvas(sprite):
    """Alt orta pivot: çizmeler alt kenarda, gövde ortada."""
    canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    left = int(round(CANVAS_W / 2 - body_center_x(sprite)))
    assert left >= 0 and left + w <= CANVAS_W and h <= CANVAS_H, f"kare tuvale sığmadı: {w}x{h}"
    canvas[CANVAS_H - h:, left:left + w] = sprite
    return canvas


def break_canvas(image, fg, cell, factor):
    """Kırılma karesi: parçalar fıçının merkezine ve zemine göre yerinde kalır (kare kare kaymasın)."""
    x0, x1, y0, y1, center, ground = cell
    mask = fg[y0:y1, x0:x1]
    ys, xs = np.where(mask)
    sprite = downscale(image[y0:y1, x0:x1], mask, factor)
    h, w = sprite.shape[:2]
    left = int(round(BREAK_W / 2 + (x0 + xs.min() - center) / factor))
    bottom = int(round((ground - (y0 + ys.max() + 1)) / factor))
    canvas = np.zeros((BREAK_H, BREAK_W, 4), dtype=np.uint8)
    top = BREAK_H - max(0, bottom) - h
    assert left >= 0 and left + w <= BREAK_W and top >= 0, f"kırılma karesi sığmadı: {w}x{h} sol {left} üst {top}"
    canvas[top:top + h, left:left + w] = sprite
    return canvas


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {}
    for name in ("yuruyus", "firlatma", "tufek", "sevinc", "olum", "hasar"):
        image = np.array(Image.open(SOURCE_DIR / f"barmen_{name}.webp").convert("RGB"), dtype=np.int32)
        pages[name] = page_frames(image, foreground(image))
        print(f"{name}: satırlardaki kare sayısı {[len(r) for r in pages[name]]}")

    idle_mask = pages["yuruyus"][0][0][1]
    factor = idle_mask.shape[0] / TARGET_HEIGHT
    print(f"1 oyun pikseli = {factor:.2f} kaynak piksel")

    sprites = [downscale(*pages[p][r][c], factor) for _, (p, r, c) in FRAMES]

    # Fıçı: temas karesinden, yerden sıçrayan beyaz toz çizgileri atılarak.
    image = np.array(Image.open(SOURCE_DIR / "barmen_fici_kirilma.webp").convert("RGB"), dtype=np.int32)
    fg = foreground(image)
    x0, x1, y0, y1 = BARREL_CELL
    barrel_mask = fg[y0:y1, x0:x1] & (image[y0:y1, x0:x1].min(axis=2) < 150)
    lab, boxes = label(barrel_mask)
    barrel_mask = lab == 1 + max(range(len(boxes)), key=lambda i: boxes[i][4])
    barrel = downscale(image[y0:y1, x0:x1], barrel_mask, factor)

    palette = np.vstack([build_palette(sprites + [barrel]), FLAME_COLORS])
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    canvases = []
    for i, ((name, _), sprite) in enumerate(zip(FRAMES, sprites)):
        canvas = place_on_canvas(apply_palette(sprite, palette))
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"barmen_{i}.png")
        canvases.append(canvas)
        print(f"  barmen_{i}: {name} ({sprite.shape[1]}x{sprite.shape[0]})")

    # 38-43 tüfekli yürüyüş: sayfalarda yok. Tetikteki gövdenin altına fıçılı yürüyüşün bacakları eklenir
    # (fıçı dizlere kadar iniyor, altında yalnız bacaklar kalıyor).
    for n, i in enumerate(WALK_FRAMES):
        canvas = canvases[READY_FRAME].copy()
        legs = canvases[i][LEG_ROW:].copy()
        # Yalnız bacaklar: sırttaki tüfeğin dipçiği ve fıçının kenarı gövdenin dışına taşmasın.
        xs = np.where(canvas[LEG_ROW:, :, 3] > 0)[1]
        legs[:, :max(0, xs.min() - 2)] = 0
        legs[:, xs.max() + 3:] = 0
        canvas[LEG_ROW:] = legs
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"barmen_{len(FRAMES) + n}.png")
        canvases.append(canvas)

    # 44-46 yukarı nişan: sayfalarda yok. Bacaklar yere basar, belden yukarısı (gövde, baş, tüfek) kemerin
    # çevresinde geriye döner. Bütün sprite'ı çevirmek yerine: ayaklar zeminde kalır.
    wx, wy = WAIST
    for i, angle in AIM_FRAMES:
        source = canvases[i]
        upper = source.copy()
        upper[wy + 2:] = 0
        lower = source.copy()
        lower[:wy - 2] = 0
        # Bel hizasındaki tüfek ve kollar alt parçada kalmasın: yalnız kalça ve bacaklar.
        hips = np.where(source[LEG_ROW:, :, 3] > 0)[1]
        lower[:LEG_ROW - 6, hips.max() - 2:] = 0
        turned = Image.fromarray(upper, "RGBA").rotate(angle, resample=Image.NEAREST, center=(wx, wy))
        canvas = Image.fromarray(lower, "RGBA")
        canvas.alpha_composite(turned)
        canvas = np.array(canvas)
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"barmen_{len(canvases)}.png")
        canvases.append(canvas)

    # 47-48 dik yukarı nişan ve ateş: tam tepedeki Gila için. Önce tüfek ve ön kol bilekten kalkar,
    # sonra belden yukarısı geriye yatar; bacaklar yine yerde.
    for i in STEEP_FRAMES:
        source = canvases[i]
        upper = source.copy()
        upper[wy + 2:] = 0
        gun = upper.copy()
        gun[:, :GUN_CUT] = 0
        upper[:, GUN_CUT:] = 0
        lower = source.copy()
        lower[:wy - 2] = 0
        hips = np.where(source[LEG_ROW:, :, 3] > 0)[1]
        lower[:LEG_ROW - 6, hips.max() - 2:] = 0
        raised = Image.fromarray(upper, "RGBA")
        raised.alpha_composite(Image.fromarray(gun, "RGBA").rotate(STEEP_GUN, resample=Image.NEAREST, center=GUN_PIVOT))
        canvas = Image.fromarray(lower, "RGBA")
        canvas.alpha_composite(raised.rotate(STEEP_TORSO, resample=Image.NEAREST, center=(wx, wy)))
        canvas = np.array(canvas)
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"barmen_{len(canvases)}.png")
        canvases.append(canvas)

    barrel = apply_palette(barrel, palette)
    Image.fromarray(barrel, "RGBA").save(OUT_DIR / "fici.png")
    print(f"  fici: {barrel.shape[1]}x{barrel.shape[0]}")
    extras = []
    for i, cell in enumerate(BREAK_CELLS):
        canvas = break_canvas(image, fg, cell, factor)
        canvas = apply_palette(canvas, palette)
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"fici_kirilma_{i}.png")
        extras.append(canvas)

    if args.preview:
        scale, cols = 3, 8
        tiles = canvases + extras
        rows_n = (len(tiles) + cols - 1) // cols
        out = Image.new("RGBA", (cols * CANVAS_W * scale, rows_n * CANVAS_H * scale), (90, 90, 110, 255))
        for i, c in enumerate(tiles):
            tile = Image.fromarray(c, "RGBA").resize((c.shape[1] * scale, c.shape[0] * scale), Image.NEAREST)
            out.alpha_composite(tile, ((i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale))
        out.save(args.preview)


if __name__ == "__main__":
    main()
