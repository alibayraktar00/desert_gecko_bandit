"""
Gila'nın her karesinden kuyruğu kopmuş ve yeniden büyüyen halleri üretir.

Kuyruk sınırları her kare için elle ölçüldü (TAILS): bacak ve gövde kuyruktan sayılmaz. Kopunca kalçada
gerçek kenarına oturan bir kontur kalır. Yeni kuyruk kertenkelede olduğu gibi kalçadan ince çıkar: önce
boyu uzar, sonra kalınlaşır ve en son eski haline döner. Büyüme kuyruğun orta hattını izler, kıvrımı korur.

Çıktı: Sprites/Player/Gila/kuyruk/<kare>_k0.png ... _k9.png

Kullanım (proje kökünden):
  python tools/gila_kuyruk_isle.py [--preview onizleme.png]
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
GILA = ROOT / "Assets" / "Sprites" / "Player" / "Gila"
OUT = GILA / "kuyruk"
OUTLINE = (31, 15, 11, 255)
STAGE_COUNT = 10

# Kuyruğun kalçaya bağlandığı nokta (x, y): büyüme buradan başlar.
HIPS = {
    "idle/gila_idle_0": (26, 42), "idle/gila_idle_1": (26, 42), "idle/gila_idle_2": (26, 42), "idle/gila_idle_3": (25, 42),
    "run/gila_run_0": (23, 41), "run/gila_run_1": (23, 40), "run/gila_run_2": (20, 40), "run/gila_run_3": (23, 40),
    "run/gila_run_4": (20, 40), "run/gila_run_5": (22, 40), "run/gila_run_6": (21, 41), "run/gila_run_7": (23, 40),
    "wall/gila_wall_0": (26, 42), "wall/gila_wall_1": (29, 43), "wall/gila_wall_2": (28, 44), "wall/gila_wall_3": (26, 43),
    "wall/gila_wall_4": (26, 43), "wall/gila_wall_5": (26, 43), "wall/gila_wall_6": (26, 43), "wall/gila_wall_7": (26, 43),
    "rise/gila_rise_0": (23, 48), "apex/gila_apex_0": (22, 48), "fall/gila_fall_0": (25, 36), "crouch/gila_crouch_0": (26, 43),
}

# kare -> [(ilk satır, son satır, bu sütunun solu kuyruk)]. Satırlar dışında kalan her şey gövde ve bacaktır.
TAILS = {
    "idle/gila_idle_0": [(33, 40, 25), (41, 46, 27)],
    "idle/gila_idle_1": [(33, 40, 25), (41, 46, 27)],
    "idle/gila_idle_2": [(33, 40, 25), (41, 46, 27)],
    "idle/gila_idle_3": [(33, 40, 25), (41, 46, 26)],
    "run/gila_run_0": [(30, 38, 21), (39, 42, 24), (43, 44, 22)],
    "run/gila_run_1": [(32, 38, 22), (39, 41, 24), (42, 42, 20)],
    "run/gila_run_2": [(32, 37, 18), (38, 41, 21), (42, 42, 18)],
    "run/gila_run_3": [(30, 37, 21), (38, 41, 24), (42, 43, 20)],
    "run/gila_run_4": [(30, 38, 19), (39, 41, 21), (42, 42, 18)],
    "run/gila_run_5": [(28, 37, 20), (38, 42, 23), (43, 43, 17)],
    "run/gila_run_6": [(30, 38, 20), (39, 43, 22)],
    "run/gila_run_7": [(30, 37, 22), (38, 42, 24), (43, 43, 19)],
    "wall/gila_wall_0": [(32, 40, 25), (41, 45, 27)],
    "wall/gila_wall_1": [(33, 40, 28), (41, 46, 30)],
    "wall/gila_wall_2": [(35, 41, 27), (42, 47, 29)],
    "wall/gila_wall_3": [(35, 41, 26), (42, 46, 27)],
    "wall/gila_wall_4": [(32, 41, 26), (42, 45, 27)],
    "wall/gila_wall_5": [(33, 41, 25), (42, 46, 27)],
    "wall/gila_wall_6": [(32, 41, 25), (42, 45, 27), (46, 47, 24)],
    "wall/gila_wall_7": [(32, 41, 25), (42, 46, 27)],
    "rise/gila_rise_0": [(44, 47, 23), (48, 63, 24)],
    "apex/gila_apex_0": [(46, 49, 24), (50, 63, 23)],
    "fall/gila_fall_0": [(17, 24, 28), (25, 33, 25), (34, 38, 26)],
    "crouch/gila_crouch_0": [(32, 40, 26), (41, 46, 27)],
}


def tail_mask(alpha, rows):
    mask = np.zeros_like(alpha)
    for y0, y1, x_max in rows:
        mask[y0:y1 + 1, :x_max] = alpha[y0:y1 + 1, :x_max]
    return mask


def neighbours(y, x, shape, diagonal=True):
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if (dy or dx) and (diagonal or dy == 0 or dx == 0) and 0 <= y + dy < shape[0] and 0 <= x + dx < shape[1]:
                yield y + dy, x + dx


def along_tail(interior, mask, hip):
    """
    Kalçadan başlayıp kuyruğun içinden geçerek sayar: konturu aşmadığı için kıvrımlar birbirine karışmaz.
    Koyu desenle kopan parçalara ikinci geçişte kontur üzerinden ulaşır.
    """
    dist = np.full(mask.shape, -1, dtype=np.int32)
    hx, hy = hip
    queue = deque()
    for y, x in zip(*np.nonzero(interior)):
        if (x - hx) ** 2 + (y - hy) ** 2 <= 9:
            dist[y, x] = 0
            queue.append((y, x))
    assert queue, f"kalça noktası kuyruğun dışında: {hip}"
    for allowed in (interior, mask):
        queue = deque(zip(*np.nonzero(dist >= 0)))
        while queue:
            y, x = queue.popleft()
            for ny, nx in neighbours(y, x, mask.shape):
                if allowed[ny, nx] and dist[ny, nx] < 0:
                    dist[ny, nx] = dist[y, x] + 1
                    queue.append((ny, nx))
    return dist


def depth_inside(interior):
    """Kenara uzaklık: orta hat en derindir."""
    depth = np.zeros(interior.shape, dtype=np.int32)
    depth[interior] = 1
    changed, level = True, 1
    while changed:
        changed = False
        for y, x in zip(*np.nonzero(depth == level)):
            if all(depth[ny, nx] >= level for ny, nx in neighbours(y, x, interior.shape, diagonal=False)) \
                    and 0 < y < interior.shape[0] - 1 and 0 < x < interior.shape[1] - 1:
                depth[y, x] = level + 1
                changed = True
        level += 1
    return depth


def stage_shape(t):
    """t: 0-1 büyüme. Önce boy, sonra kalınlık."""
    length = 0.06 + 0.94 * min(1.0, t / 0.75) ** 0.9
    width = 1 if t < 0.4 else 2 if t < 0.72 else 3 if t < 0.9 else 9
    return length, width


def keep_main_body(image):
    """Gövdeden kopuk kalan küçük parçaları (kuyruğun kontur artıkları) siler."""
    alpha = image[..., 3] > 0
    seen = np.zeros(alpha.shape, dtype=bool)
    groups = []
    for sy, sx in zip(*np.nonzero(alpha)):
        if seen[sy, sx]:
            continue
        seen[sy, sx] = True
        queue, cells = deque([(sy, sx)]), []
        while queue:
            y, x = queue.popleft()
            cells.append((y, x))
            for ny, nx in neighbours(y, x, alpha.shape):
                if alpha[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    queue.append((ny, nx))
        groups.append(cells)
    biggest = max(groups, key=len)
    for cells in groups:
        if cells is not biggest:
            for y, x in cells:
                image[y, x] = 0
    return image


def build_stage(full, mask, interior, along, depth, t):
    image = full.copy()
    image[mask] = 0
    longest = max(1, along.max())
    reach = longest * stage_shape(t)[0]
    width = stage_shape(t)[1]

    ridge = np.zeros(longest + 2, dtype=np.int32)
    for y, x in zip(*np.nonzero(interior)):
        g = along[y, x]
        ridge[max(0, g - 1):g + 2] = np.maximum(ridge[max(0, g - 1):g + 2], depth[y, x])

    visible = np.zeros_like(mask)
    for y, x in zip(*np.nonzero(interior)):
        g = along[y, x]
        if g > reach:
            continue
        tip = 1 + int((reach - g) / 2)  # uca doğru incelir
        if depth[y, x] >= ridge[g] - min(width, tip) + 1:
            visible[y, x] = True
    image[visible] = full[visible]

    # Yeni kuyruğun ve kopma yerinin konturu
    alpha = full[..., 3] > 0
    for y, x in zip(*np.nonzero(visible)):
        for ny, nx in neighbours(y, x, mask.shape):
            if not visible[ny, nx] and (mask[ny, nx] or not alpha[ny, nx]):
                image[ny, nx] = OUTLINE
    for y, x in zip(*np.nonzero(mask)):
        if visible[y, x]:
            continue
        for ny, nx in neighbours(y, x, mask.shape, diagonal=False):
            if alpha[ny, nx] and not mask[ny, nx]:
                image[ny, nx] = OUTLINE
    return keep_main_body(image)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    OUT.mkdir(parents=True, exist_ok=True)
    rows_out = []
    for name, rows in TAILS.items():
        full = np.array(Image.open(GILA / f"{name}.png").convert("RGBA"))
        alpha = full[..., 3] > 0
        mask = tail_mask(alpha, rows)
        rgb = full[..., :3].astype(np.int32)
        interior = mask & (rgb.sum(axis=2) > 170)  # koyu kontur iç sayılmaz
        along = along_tail(interior, mask, HIPS[name])
        depth = depth_inside(interior)

        stages = [build_stage(full, mask, interior, along, depth, i / (STAGE_COUNT - 1) * 0.97) for i in range(STAGE_COUNT)]
        base = name.split("/")[1]
        if not args.preview:
            for i, image in enumerate(stages):
                Image.fromarray(image, "RGBA").save(OUT / f"{base}_k{i}.png")
        rows_out.append([stages[i] for i in (0, 2, 4, 6, 8)] + [full])

    if args.preview:
        zoom, crop = 3, (6, 14, 44, 60)
        w, h = (crop[2] - crop[0]) * zoom, (crop[3] - crop[1]) * zoom
        per_column = 12
        sheet = Image.new("RGBA", (w * 12, h * 12), (70, 110, 130, 255))
        for i, row in enumerate(rows_out):
            for j, image in enumerate(row):
                tile = Image.fromarray(image, "RGBA").crop(crop).resize((w, h), Image.NEAREST)
                sheet.alpha_composite(tile, ((i // per_column) * 6 * w + j * w, (i % per_column) * h))
        sheet.save(args.preview)
    print("kare:", len(rows_out))


if __name__ == "__main__":
    main()
