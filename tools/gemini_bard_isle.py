"""
Bard'ın (yarasa çağıran gitarist) sayfalarını oyuna hazır karelere çevirir: insan formu, dönüşüm, canavar formu.

Sayfalar 4x2 düzeninde, zemin düz koyu gri. Her karede yalnız gövde (en büyük bağlı parça) alınır: yazılar,
kıvılcımlar ve köşedeki Gila referansı elenir. Referansın gövdeye yapıştığı ilk kareler kullanılmaz.
Sayfalar (hatta aynı sayfanın satırları) farklı ölçekte çizilmiş: her satır kendi boy ölçüsüyle küçültülür,
insan INSAN_BOY, canavar CANAVAR_BOY piksel olur. Dönüşüm kareleri ikisinin arasında kademeli büyür.

Gitarla vurma (yakın dövüş): insan formunun kareleri ekran görüntülerinden kesilir (bard_insan_vurus_*.png;
görüntülerin kenarındaki çerçeve ve yarım kareler KIRP ile dışarıda bırakılır). Canavar formunun vurma
kareleri çizilmedi: insan formunun aynı karelerinden üretilir. Ten ve giysi canavarın mor paletine boyanır,
gövde canavar boyuna büyütülür, başın yerine canavarın çalma karesindeki başı (dağınık saç, boynuzlar) oturur.

Kareler sağa bakar; sola bakış oyunda flipX ile.

Kullanım (proje kökünden):
  python tools/gemini_bard_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_dusman_isle import body_center_x
from gemini_serif_isle import downscale, label
from gemini_sprite_isle import apply_palette, build_palette

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "bard"
CANVAS_W, CANVAS_H = 128, 96
INSAN_BOY, CANAVAR_BOY = 50, 56   # ayakta boy (oyun pikseli); Gila 34, şerif 47, barmen 56
MIN_AREA = 9000                   # gövde; yazılar ve kopuk parçalar bundan küçük
TOLERANCE = 10                    # zemin rengine yakınlık: canavarın koyu mor dış çizgisi zemine karışmasın

# sayfa -> (dosya, [(satır üstü, satır altı, o satırda ayakta duran gövdenin kaynak boyu, hedef boy)])
PAGES = {
    "i_yuruyus": ("bard_insan_yuruyus.webp", [(79, 337, 252, INSAN_BOY), (420, 729, 255, INSAN_BOY)]),
    "i_calma": ("bard_insan_calma.webp", [(67, 275, 200, INSAN_BOY), (300, 547, 192, INSAN_BOY)]),
    "i_hasar": ("bard_insan_hasar.webp", [(79, 337, 249, INSAN_BOY), (420, 729, 256, INSAN_BOY)]),
    "i_sevinc": ("bard_insan_sevinc.webp", [(79, 337, 249, INSAN_BOY), (420, 729, 256, INSAN_BOY)]),
    # Ölüm sayfaları öteki sayfalardan küçük çizilmiş (yerdeki gitar 170 piksel, yürüyüşte 253): ayakta boy
    # ölçüsü gitarın boyuna göre küçültüldü, yoksa Bard ölürken ufalıyordu.
    "i_olum": ("bard_insan_olum.webp", [(79, 276, 172, INSAN_BOY), (560, 733, 172, INSAN_BOY)]),
    # Dönüşüm: boy kare kare ayarlanır (GROW), buradaki ölçü yalnız başlangıç.
    "donusum": ("bard_donusum.webp", [(79, 337, 249, INSAN_BOY), (410, 701, 244, CANAVAR_BOY)]),
    "c_yuruyus": ("bard_canavar_yuruyus.webp", [(76, 284, 201, CANAVAR_BOY), (300, 541, 203, CANAVAR_BOY)]),
    "c_calma": ("bard_canavar_calma.webp", [(76, 284, 201, CANAVAR_BOY), (300, 541, 203, CANAVAR_BOY)]),
    "c_hasar": ("bard_canavar_hasar.webp", [(75, 379, 289, CANAVAR_BOY), (440, 733, 262, CANAVAR_BOY)]),
    "c_sevinc": ("bard_canavar_sevinc.webp", [(75, 379, 289, CANAVAR_BOY), (440, 733, 262, CANAVAR_BOY)]),
    "c_olum": ("bard_canavar_olum.png", [(18, 189, 185, CANAVAR_BOY)]),
    # Vurma: iki ayrı ekran görüntüsü. Duruş karesinin boyu 177 piksel.
    "i_vurus_a": ("bard_insan_vurus_1.png", [(13, 197, 177, INSAN_BOY)]),   # duruş, geriye çekme
    "i_vurus_b": ("bard_insan_vurus_2.png", [(25, 213, 177, INSAN_BOY)]),   # yukarı kaldırma, iniş/savurma
}
# sayfa -> (sol, sağ): yalnız bu sütunlar okunur (yarım kalmış komşu kare, çerçeve, düğmeler dışarıda).
KIRP = {"i_vurus_b": (60, 545)}

# Canavarın hasar ve sevinç sayfaları öteki sayfalardan dar ve uzun çizilmiş: (sayfa, satır) -> yatay esnetme.
# Boy yine satır ölçüsüyle oturur, en çalma karelerindeki gövdeyle aynı olur.
STRETCH = {("c_hasar", 0): 1.25, ("c_hasar", 1): 1.18, ("c_sevinc", 0): 1.25, ("c_sevinc", 1): 1.18}

# Dönüşümde hedef boy: insan boyundan canavar boyuna kademeli (kare 2..8).
GROW = [50, 51, 52, 53, 54, 55, 56]


def cells(page, pairs):
    return [(page, r, c) for r, c in pairs]


def row(page, r, columns):
    return [(page, r, c) for c in columns]


# Sıra koddaki indekslerle aynı (CombatSetupBuilder.BuildBard).
GROUPS = [
    # --- insan ---
    ("i_bekleme", cells("i_calma", [(0, 0), (1, 3)])),                      # 0-1
    ("i_yuruyus", row("i_yuruyus", 0, (1, 2, 3)) + row("i_yuruyus", 1, range(4))),  # 2-8 (1. karede Gila referansı yapışık)
    ("i_calma", row("i_calma", 0, range(4)) + row("i_calma", 1, range(4))),  # 9-16
    ("i_hasar", row("i_hasar", 0, (1, 2, 3)) + row("i_hasar", 1, range(4))),  # 17-23
    ("i_sevinc", row("i_sevinc", 0, range(4)) + row("i_sevinc", 1, range(4))),  # 24-31
    ("i_olum", row("i_olum", 0, range(4)) + row("i_olum", 1, range(3))),     # 32-38 (son kare yalnız kalıntı, kullanılmıyor)
    # --- dönüşüm --- (1. kare insan duruşu: yerine bekleme karesi oynar)
    ("donusum", row("donusum", 0, (1, 2, 3)) + row("donusum", 1, range(4))),  # 39-45
    # --- canavar ---
    ("c_bekleme", row("c_yuruyus", 0, (0, 1))),                              # 46-47
    ("c_yuruyus", row("c_yuruyus", 0, (2, 3)) + row("c_yuruyus", 1, range(4))),  # 48-53
    ("c_calma", row("c_calma", 0, range(4)) + row("c_calma", 1, range(4))),  # 54-61
    ("c_hasar", row("c_hasar", 0, range(4)) + row("c_hasar", 1, range(4))),  # 62-69
    ("c_sevinc", row("c_sevinc", 0, range(4)) + row("c_sevinc", 1, range(4))),  # 70-77
    ("c_olum", row("c_olum", 0, (0, 1))),                                    # 78-79: sarsılma, dizlerin üstüne çöküş
    # 80-83 insan gitarla vurma: geriye çekme, yukarı kaldırma, iniş/savurma, duruş (toparlanma)
    ("i_vurus", [("i_vurus_a", 0, 1), ("i_vurus_b", 0, 0), ("i_vurus_b", 0, 1), ("i_vurus_a", 0, 0)]),
]

# Canavarın gitarla vurması: (insan karesi, o karede yüzün ortası (x, y) tuvalde ya da None).
# None: baş kolların arasında, görünmüyor (yukarı kaldırma): yalnız boyanır.
MONSTER_BASH = [(80, (66, 52)), (81, None), (82, (69, 59)), (83, (64, 52))]   # 84-87
MONSTER_HEAD = (54, (56, 37, 79, 58), (68, 51))  # canavar karesi, baş kutusu (sol, üst, sağ, alt), yüzün ortası
MONSTER_SKIN = [(47, 26, 45), (75, 46, 69), (112, 86, 88)]  # koyu, orta, açık


def foreground(image, screenshot=False):
    """
    Zemin: sayfanın kenar rengine yakın piksellerin en büyük bağlı bölgesi. Ekran görüntülerinde kenarda yazı ve
    çerçeve olabilir: zemin rengi orada bütün görüntünün ortanca rengidir.
    """
    reference = np.median(image.reshape(-1, 3), axis=0) if screenshot else image[min(400, image.shape[0] - 10), 5]
    near = np.abs(image - reference).max(axis=2) < TOLERANCE
    lab, boxes = label(near)
    return lab != 1 + max(range(len(boxes)), key=lambda i: boxes[i][4])


def page_frames(image, fg, bands):
    """Her satır bandında gövdeleri soldan sağa döndürür: (rgb, maske)."""
    rows = []
    for y0, y1, _, _ in bands:
        lab, boxes = label(fg[y0:y1])
        found = []
        for i in sorted((i for i, b in enumerate(boxes) if b[4] > MIN_AREA), key=lambda i: boxes[i][0]):
            bx0, by0, bx1, by1, _ = boxes[i]
            found.append((image[y0 + by0:y0 + by1, bx0:bx1], lab[by0:by1, bx0:bx1] == i + 1))
        rows.append(found)
    return rows


def stretch(cell, mask, factor):
    """Kareyi yatayda esnetir (en yakın komşu): küçültmeden önce, kaynak çözünürlükte."""
    columns = np.minimum((np.arange(int(round(cell.shape[1] * factor))) / factor).astype(int), cell.shape[1] - 1)
    return cell[:, columns], mask[:, columns]


def place_on_canvas(sprite):
    """Alt orta pivot: ayaklar alt kenarda, gövde ortada."""
    canvas = np.zeros((CANVAS_H, CANVAS_W, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    left = int(round(CANVAS_W / 2 - body_center_x(sprite)))
    assert left >= 0 and left + w <= CANVAS_W and h <= CANVAS_H, f"kare tuvale sığmadı: {w}x{h} sol {left}"
    canvas[CANVAS_H - h:, left:left + w] = sprite
    return canvas


def is_cream(rgb):
    """Gitarın kremi: boyanmaz, başın altında kalmaz."""
    return (rgb[..., 0] > 165) & (rgb[..., 1] > 135)


def monster_bash(source, face, head_canvas):
    """İnsan vurma karesinden canavar karesi: mor palet, canavar boyu, canavarın başı."""
    rgb = source[..., :3].astype(np.int32)
    opaque = source[..., 3] > 0
    cream = is_cream(rgb)
    skin = opaque & ~cream & (rgb[..., 0] > 95) & (rgb[..., 0] - rgb[..., 2] > 35)
    cloth = opaque & ~cream & ~skin
    out = source.copy()
    light = rgb.sum(axis=2) / 3
    ramp = np.array(MONSTER_SKIN, dtype=np.uint8)
    out[skin, :3] = ramp[np.digitize(light[skin], (95, 125))]
    v = rgb.max(axis=2)[cloth]
    out[cloth, :3] = np.stack([10 + v, 6 + v * 0.75, 16 + v * 1.05], axis=1).clip(0, 255).astype(np.uint8)

    # Canavar boyu: ayak hizası (alt orta) sabit, en yakın komşu.
    scale = CANAVAR_BOY / INSAN_BOY
    ys, xs = np.mgrid[0:CANVAS_H, 0:CANVAS_W]
    sx = np.round(CANVAS_W / 2 + (xs - CANVAS_W / 2) / scale).astype(int)
    sy = np.round(CANVAS_H - 1 + (ys - (CANVAS_H - 1)) / scale).astype(int)
    inside = (sx >= 0) & (sx < CANVAS_W) & (sy >= 0)
    grown = np.zeros_like(out)
    grown[inside] = out[sy[inside], sx[inside]]
    if face is None:
        return grown

    index, (x0, y0, x1, y1), (cx, cy) = MONSTER_HEAD
    fx = int(round(CANVAS_W / 2 + (face[0] - CANVAS_W / 2) * scale))
    fy = int(round(CANVAS_H - 1 + (face[1] - (CANVAS_H - 1)) * scale))
    for y in range(y0, y1):
        for x in range(x0, x1):
            pixel = head_canvas[y, x]
            tx, ty = x - cx + fx, y - cy + fy
            if not pixel[3] or is_cream(pixel[None, :3].astype(np.int32))[0] or not (0 <= tx < CANVAS_W and 0 <= ty < CANVAS_H):
                continue
            # Gitar başın önünde kalır: hedefteki krem pikselin üstüne yazılmaz.
            if grown[ty, tx, 3] and is_cream(grown[ty, tx][None, :3].astype(np.int32))[0]:
                continue
            grown[ty, tx] = pixel
    return grown


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    pages = {}
    for name, (file, bands) in PAGES.items():
        image = np.array(Image.open(SOURCE_DIR / file).convert("RGB"), dtype=np.int32)
        screenshot = name.startswith("i_vurus")
        if name in KIRP:
            image = image[:, KIRP[name][0]:KIRP[name][1]]
        if screenshot:
            # Savurmanın beyaz-mavi yayı üstteki düğmelere ve çerçeveye değiyor: yay silinir, gövde ve gitar kalır
            # (krem gitarda mavi kanal kırmızıdan belirgin düşük, yayda değil).
            arc = (image.min(axis=2) > 140) & (image[..., 2] >= image[..., 0] - 12)
            image[arc] = np.median(image.reshape(-1, 3), axis=0)
        pages[name] = page_frames(image, foreground(image, screenshot), bands)
        print(f"{name}: satırlardaki kare sayısı {[len(r) for r in pages[name]]}")

    sprites, names = [], []
    for group, frames in GROUPS:
        for n, (page, r, c) in enumerate(frames):
            _, _, source_height, target = PAGES[page][1][r]
            if group == "donusum":
                # Kaynakta insan 249, canavar 244 piksel: boy insan ölçüsünden canavar ölçüsüne kayar.
                t = n / (len(frames) - 1)
                source_height = 249 + (244 - 249) * t
                target = GROW[n]
            cell, mask = pages[page][r][c]
            if (page, r) in STRETCH:
                cell, mask = stretch(cell, mask, STRETCH[(page, r)])
            sprites.append(downscale(cell, mask, source_height / target))
            names.append(group)

    human = [s for s, n in zip(sprites, names) if n.startswith("i_")]
    monster = [s for s, n in zip(sprites, names) if not n.startswith("i_")]
    palettes = {True: build_palette(human), False: build_palette(monster)}

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    canvases = []
    for i, (name, sprite) in enumerate(zip(names, sprites)):
        canvas = place_on_canvas(apply_palette(sprite, palettes[name.startswith("i_")]))
        canvases.append(canvas)
        print(f"  bard_{i}: {name} ({sprite.shape[1]}x{sprite.shape[0]})")

    head_canvas = canvases[MONSTER_HEAD[0]]
    for source, face in MONSTER_BASH:
        print(f"  bard_{len(canvases)}: c_vurus (kare {source})")
        canvases.append(monster_bash(canvases[source], face, head_canvas))

    for i, canvas in enumerate(canvases):
        Image.fromarray(canvas, "RGBA").save(OUT_DIR / f"bard_{i}.png")

    if args.preview:
        scale, cols = 3, 8
        rows_n = (len(canvases) + cols - 1) // cols
        out = Image.new("RGBA", (cols * CANVAS_W * scale, rows_n * CANVAS_H * scale), (90, 90, 110, 255))
        for i, c in enumerate(canvases):
            tile = Image.fromarray(c, "RGBA").resize((CANVAS_W * scale, CANVAS_H * scale), Image.NEAREST)
            out.alpha_composite(tile, ((i % cols) * CANVAS_W * scale, (i // cols) * CANVAS_H * scale))
        out.save(args.preview)


if __name__ == "__main__":
    main()
