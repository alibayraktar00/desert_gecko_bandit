"""Hazır görsellerden level sonu dağını ve checkpoint kaktüsünün karelerini üretir (32 PPU).

Girdi:  Assets/Sprites/_Gelen/dag_kaya.jpg, checkpoint_kaktus.jpg (kapalı), checkpoint_aktif.jpg (8 kare)
Çıktı:  Katman/dag.png
        Props/Checkpoint/kapali.png, yanis_0-3.png (fener tutuşur), aktif_0-29.png (3 saniyelik yanma döngüsü)

Sayfanın ilk karesindeki sönük fener kullanılmaz; kalan yedisi döngüdür, tutuşma ilkine doğru geçer. Tüm kareler aynı tuvalde, gövde ortası ve
alt kenarlarından hizalıdır. Sayfanın kareleri birbirinden hafifçe farklı çizildiği için döngüde tek bir
kare sabit gövde olur. Alev sayfanın karelerinden gelir. Bayrak ve duman, kareler arasında zıplamasın
diye yavaş ve kesintisiz hareketle burada canlandırılır. Rastgelelik sabit tohumludur.
Çalıştırma: python tools/checkpoint_dag_isle.py
"""
import math
import random
from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "Assets/Sprites/_Gelen"
LAYER_DIR = ROOT / "Assets/Sprites/Environment/Katman"
CHECKPOINT_DIR = ROOT / "Assets/Sprites/Environment/Props/Checkpoint"

# --- Dağ ---
MOUNTAIN_SCALE = 2.3    # 6.6 birim yükseklik: tepesi kameraya sığar, en yüksek zıplamanın üstünde kalır
MOUNTAIN_W, MOUNTAIN_H = 860, 320
GAP_ROCK = (0x2C, 0x18, 0x16)


def is_purple(c):
    r, g, b = c
    return b > r + 6 and b > g + 14


def cut_background(path):
    src = Image.open(path).convert("RGB")
    out = src.convert("RGBA")
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            if is_purple(px[x, y][:3]):
                px[x, y] = (0, 0, 0, 0)
    return out


def shrink(img, scale):
    """Kenar sızması olmadan küçültür; alfa keskin kalır, JPEG gürültüsü paletle temizlenir."""
    size = (round(img.width / scale), round(img.height / scale))
    alpha = img.getchannel("A").resize(size, Image.BOX).point(lambda v: 255 if v > 150 else 0)
    # Saydam piksellerin rengi karışmasın: zemin rengini en yakın kaya rengine çek.
    rgb = Image.new("RGB", img.size, (0x4A, 0x24, 0x18))
    rgb.paste(img, (0, 0), img)
    rgb = rgb.resize(size, Image.BOX).quantize(colors=48, method=Image.Quantize.MEDIANCUT).convert("RGB")
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def strip_dark_side(img):
    """Görselin solundaki siyah gölge sütununu atar: arkadaki kaya şerit gibi kesilmesin."""
    px = img.load()
    q = deque((0, y) for y in range(img.height))
    seen = set(q)
    while q:
        x, y = q.popleft()
        r, g, b, a = px[x, y]
        if a and r + g + b > 100:
            continue
        px[x, y] = (0, 0, 0, 0)
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= n[0] < img.width and 0 <= n[1] < img.height and n not in seen:
                seen.add(n)
                q.append(n)


def shade(img, k, cool):
    """Geridekiler soluk ve hafif mavi: hava perspektifi."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (int(r * k + cool[0]), int(g * k + cool[1]), int(b * k + cool[2]), a)
    return out


def fill_gaps(img):
    """Kayaların arasından gökyüzü görünmesin: her sütunda kaya başladıktan sonraki boşluklar koyu kaya olur."""
    px = img.load()
    min_run = 6  # tepedeki çalılar sayılmaz, araları dolmasın
    for x in range(img.width):
        run, start = 0, None
        for y in range(img.height):
            run = run + 1 if px[x, y][3] else 0
            if run >= min_run:
                start = y
                break
        if start is None:
            continue
        for y in range(start, img.height):
            if not px[x, y][3]:
                px[x, y] = (*GAP_ROCK, 255)


def mountain():
    """Önde görseldeki kayalık, arkasında aynı kayanın büyüyen ve solan kopyaları: level dışı sıradağ."""
    front = shrink(cut_background(RAW / "dag_kaya.jpg"), MOUNTAIN_SCALE)
    strip_dark_side(front)
    box = front.getbbox()
    front = front.crop((box[0], 0, box[2], front.height))
    canvas = Image.new("RGBA", (MOUNTAIN_W, MOUNTAIN_H), (0, 0, 0, 0))

    def layer(scale, k, cool, right, flip=False):
        size = (round(front.width * scale), round(front.height * scale))
        part = shade(front.resize(size, Image.NEAREST), k, cool)
        if flip:
            part = part.transpose(Image.FLIP_LEFT_RIGHT)
        canvas.alpha_composite(part, (right - size[0], MOUNTAIN_H - size[1]))

    # Sonuncu aynalı: dağın dış ucu da düz kesik değil, yamaç olarak biter.
    # Katmanlar geniş bindirilir: her kayanın dış kenarı öndekinin arkasında kalır, dağ tek parça görünür.
    layer(1.34, 0.46, (16, 20, 34), MOUNTAIN_W - 330, flip=True)
    layer(1.18, 0.66, (8, 10, 18), MOUNTAIN_W - 110)
    layer(1.0, 1.0, (0, 0, 0), MOUNTAIN_W)
    fill_gaps(canvas)
    canvas.save(LAYER_DIR / "dag.png")
    print("dag.png", canvas.size, "ön kaya", front.size)


# --- Checkpoint ---
CP_HEIGHT = 72                          # kaktüs ve taşların yüksekliği (oyun pikseli) = 2,25 birim
CP_LEFT, CP_RIGHT, CP_TOP = 30, 42, 30  # gövde ortasından sol/sağ, kaktüs tepesinden yukarı (oyun pikseli)
SHEET_COLS = [40, 290, 540, 790, 1024]
SHEET_ROWS = [10, 285, 572]
OFF_CELL = (120, 60, 470, 510)          # kapalı kaktüs, checkpoint_kaktus.jpg içinde
SMOKE_BOOST = 1.7                       # sayfadaki duman koyu zemine göre: gökyüzünde de görünsün


def is_sheet_bg(c):
    r, g, b = c
    return b >= r - 4 and b > g + 6 and abs(r - 42) < 30 and abs(g - 29) < 24 and abs(b - 52) < 28


def cut_cell(src, box):
    cell = src.crop(box).convert("RGBA")
    px = cell.load()
    for y in range(cell.height):
        for x in range(cell.width):
            if is_sheet_bg(px[x, y][:3]):
                px[x, y] = (0, 0, 0, 0)
    return cell


def anchors(cell):
    """Hizalama noktaları: gövde tepesinin ortası, tepesi ve taşların altı."""
    px = cell.load()
    center = top = None
    for y in range(cell.height):
        greens = [x for x in range(cell.width) if px[x, y][3] and px[x, y][1] > px[x, y][0] + 2 and px[x, y][1] > px[x, y][2] + 2]
        if len(greens) > 6:
            center, top = sum(greens) / len(greens), y
            break
    body = cell.copy()
    keep_largest(body)
    return center, top, body.getbbox()[3]


def clean_lantern_air(cell, center, bottom):
    """Fenerin arkasındaki kızıl parıltıyı siler (gökyüzünde leke gibi durur), dumanı açar."""
    px = cell.load()
    x0, x1 = int(center + 18), min(cell.width, int(center + 80))
    for y in range(max(0, bottom - 215), bottom - 140):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a and 44 <= r <= 92 and 26 <= g <= 62 and 26 <= b <= 66 and abs(g - b) < 16 and r < g + 34:
                px[x, y] = (0, 0, 0, 0)
    for y in range(0, max(0, bottom - 195)):
        for x in range(x0 - 10, x1):
            r, g, b, a = px[x, y]
            if a and abs(r - b) < 24 and abs(r - g) < 26:
                px[x, y] = (min(255, int(r * SMOKE_BOOST)), min(255, int(g * SMOKE_BOOST)), min(255, int(b * SMOKE_BOOST)), 255)


def keep_largest(img):
    """Ana gövdeye bağlı olmayan parçaları atar."""
    px = img.load()
    seen, best = set(), []
    for y in range(img.height):
        for x in range(img.width):
            if px[x, y][3] and (x, y) not in seen:
                group, q = [], deque([(x, y)])
                seen.add((x, y))
                while q:
                    a, b = q.popleft()
                    group.append((a, b))
                    for n in ((a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1)):
                        if 0 <= n[0] < img.width and 0 <= n[1] < img.height and px[n][3] and n not in seen:
                            seen.add(n)
                            q.append(n)
                if len(group) > len(best):
                    best = group
    keep = set(best)
    for y in range(img.height):
        for x in range(img.width):
            if (x, y) not in keep:
                px[x, y] = (0, 0, 0, 0)


def to_game_size(cell, lit=False):
    """Hizalar ve oyun boyuna indirir; alt satır taşların altıdır, böylece kare yere tam oturur."""
    center, top, bottom = anchors(cell)
    scale = (bottom - top) / CP_HEIGHT  # sayfadaki kareler biraz farklı boyda: her biri aynı boya iner
    print(f"  ölçek {scale:.2f}")
    if lit:
        clean_lantern_air(cell, center, bottom)
    box = (round(center - CP_LEFT * scale), round(bottom - (CP_TOP + CP_HEIGHT) * scale), round(center + CP_RIGHT * scale), bottom)
    canvas = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), (0, 0, 0, 0))
    canvas.paste(cell, (-box[0], -box[1]))
    return shrink_to(canvas, (CP_LEFT + CP_RIGHT, CP_TOP + CP_HEIGHT))


def shrink_to(img, size):
    """Saydam kenarlar renge karışmasın diye önceden çarpılmış alfayla küçültür."""
    small = img.convert("RGBa").resize(size, Image.BOX).convert("RGBA")
    alpha = small.getchannel("A").point(lambda v: 255 if v > 110 else 0)
    small.putalpha(alpha)  # palete indirilmez: az sayıdaki duman pikseli kahverengiye dönerdi
    return small


# Döngüde hareket eden bölgeler (oyun pikseli, kare tuvalinde)
SMOKE_ZONE = (34, 0, 72, 38)
FLAME_BOX = (37, 41, 44, 48)
FLAG_BOX = (44, 49, 70, 66)


def in_box(x, y, box):
    return box[0] <= x < box[2] and box[1] <= y < box[3]


def is_smoke(c):
    """Duman ve kıvılcım: açık renkli; fener sapı koyu ve kahverengi olduğu için ayrılır."""
    r, g, b, a = c
    return a > 0 and r > 85 and abs(r - b) < 34


def flag_mask(frame):
    """Bayrak pikselleri: kırmızı gövdesi ve ona bitişik koyu kenarı."""
    px = frame.load()

    def red(x, y):
        r, g, b, a = px[x, y]
        return a > 0 and r > 60 and r > g * 1.5 and r > b * 1.4

    mask = set()
    for y in range(FLAG_BOX[1], FLAG_BOX[3]):
        for x in range(FLAG_BOX[0], FLAG_BOX[2]):
            if red(x, y):
                mask.add((x, y))
    for x, y in list(mask):
        for nx in range(x - 1, x + 2):
            for ny in range(y - 1, y + 2):
                if in_box(nx, ny, FLAG_BOX) and nx < frame.width and px[nx, ny][3] and sum(px[nx, ny][:3]) < 110:
                    mask.add((nx, ny))
    return mask


def still_body(frames):
    """Tek kareyi sabit gövde yapar: duman ve bayrak silinir, bayrağın ardı öteki karelerden tamamlanır."""
    base = frames[0].copy()
    px = base.load()
    for y in range(SMOKE_ZONE[1], SMOKE_ZONE[3]):
        for x in range(SMOKE_ZONE[0], SMOKE_ZONE[2]):
            if is_smoke(px[x, y]):
                px[x, y] = (0, 0, 0, 0)

    masks = [flag_mask(f) for f in frames]
    for y in range(FLAG_BOX[1], FLAG_BOX[3]):
        for x in range(FLAG_BOX[0], min(FLAG_BOX[2], base.width)):
            if (x, y) not in masks[0]:
                continue
            seen = [f.getpixel((x, y)) for f, m in zip(frames, masks) if (x, y) not in m]
            solid = [c for c in seen if c[3]]
            px[x, y] = solid[0] if len(solid) * 2 > len(seen) else (0, 0, 0, 0)
    return base


LOOP_FRAMES = 30                                # 10 kare/sn ile 3 saniye
FLAME_ORDER = [0, 1, 2, 3, 4, 5, 6, 3, 1, 4]    # her biri 3 kare kalır: titreme yumuşak olur
FLAG_WAVES = 2                                  # döngüde bayraktan geçen dalga sayısı
FLAG_LENGTH = 0.2                               # dalganın bayrak boyunca sıklığı
PUFFS, SMOKE_RISE = 6, 28                       # aynı anda duman sayısı, yükselme (piksel)
SMOKE_ORIGIN = (41, 31)                         # fener sapının tepesi


def soft_flag(px, pixels, index):
    """Bayrak sapa bağlı yerde durur; uca doğru artan, yavaş ilerleyen tek bir dalgayla sallanır."""
    t = index / LOOP_FRAMES * 2 * math.pi * FLAG_WAVES
    for x, y, c in pixels:
        reach = (x - FLAG_BOX[0]) / (FLAG_BOX[2] - FLAG_BOX[0])
        px[x, y + round(math.sin(x * FLAG_LENGTH - t) * 2.6 * reach)] = c


def soft_smoke(frame, index):
    """İnce bir duman sütunu: yavaş yükselir, hafifçe salınır, büyüyüp dama deseniyle seyrelerek kaybolur."""
    px = frame.load()
    for p in range(PUFFS):
        life = (index / LOOP_FRAMES + p / PUFFS) % 1.0
        cx = SMOKE_ORIGIN[0] + life * 4 + math.sin(life * 2 * math.pi + p) * (0.4 + 1.6 * life)
        cy = SMOKE_ORIGIN[1] - life * SMOKE_RISE
        radius = 0.9 + 1.6 * life
        tone = int(205 - 45 * life)
        for y in range(int(cy - radius), int(cy + radius) + 2):
            for x in range(int(cx - radius), int(cx + radius) + 2):
                if not (0 <= x < frame.width and 0 <= y < frame.height):
                    continue
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 > radius * radius:
                    continue
                if (life > 0.6 and (x + y) % 2) or (life > 0.85 and x % 2):
                    continue
                px[x, y] = (tone, tone - 8, tone - 12, 255)


def compose_loop(frames):
    """Sabit gövdenin üstüne alev, bayrak ve duman biner; gövde hiç oynamaz."""
    body = still_body(frames)
    flag = sorted(flag_mask(frames[0]))
    src0 = frames[0].load()
    flag_pixels = [(x, y, src0[x, y]) for x, y in flag if x < body.width]
    out = []
    for i in range(LOOP_FRAMES):
        result = body.copy()
        px = result.load()
        flame = frames[FLAME_ORDER[i // 3]].load()
        for y in range(FLAME_BOX[1], FLAME_BOX[3]):
            for x in range(FLAME_BOX[0], FLAME_BOX[2]):
                px[x, y] = flame[x, y]
        soft_flag(px, flag_pixels, i)
        soft_smoke(result, i)
        out.append(result)
    return out


def checkpoint():
    CHECKPOINT_DIR.mkdir(parents=True, exist_ok=True)
    for old in CHECKPOINT_DIR.glob("*.png"):
        old.unlink()  # eski kare sayısından artan dosya kalmasın (meta'yı Unity temizler)

    off = to_game_size(cut_cell(Image.open(RAW / "checkpoint_kaktus.jpg").convert("RGB"), OFF_CELL))
    off.save(CHECKPOINT_DIR / "kapali.png")

    sheet = Image.open(RAW / "checkpoint_aktif.jpg").convert("RGB")
    frames = []
    for r in range(2):
        for c in range(4):
            box = (SHEET_COLS[c], SHEET_ROWS[r], SHEET_COLS[c + 1], SHEET_ROWS[r + 1])
            frames.append(to_game_size(cut_cell(sheet, box), lit=True))

    loop = compose_loop(frames[1:])
    for i, frame in enumerate(loop):
        frame.save(CHECKPOINT_DIR / f"aktif_{i}.png")

    # Tutuşma: kapalıdan döngünün ilk karesine dama geçişi.
    w, h = off.size
    order = [(x, y) for y in range(h) for x in range(w)]
    random.Random(1923).shuffle(order)
    for i in range(4):
        frame = off.copy()
        px, target = frame.load(), loop[0].load()
        for x, y in order[:int(len(order) * (i + 1) / 4)]:
            px[x, y] = target[x, y]
        frame.save(CHECKPOINT_DIR / f"yanis_{i}.png")
    print("checkpoint kareleri tamam", off.size)


def main():
    mountain()
    checkpoint()


if __name__ == "__main__":
    main()
