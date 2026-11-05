"""Hazır görsellerden level sonu dağını ve checkpoint kaktüsünün karelerini üretir (32 PPU).

Girdi:  Assets/Sprites/_Gelen/dag_kaya.jpg, checkpoint_kaktus.jpg
Çıktı:  Katman/dag.png
        Props/Checkpoint/kapali.png, yanis_0-3.png (fener tutuşur), aktif_0-11.png (döngü: alev, bayrak, duman)

Görseldeki duman tek karelik olduğu için kaldırılır; alev, bayrak ve duman burada canlandırılır.
Tüm checkpoint kareleri aynı tuvalde ve alttan hizalıdır. Rastgelelik sabit tohumludur.
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
    layer(1.34, 0.46, (16, 20, 34), MOUNTAIN_W - 450, flip=True)
    layer(1.18, 0.66, (8, 10, 18), MOUNTAIN_W - 210)
    layer(1.0, 1.0, (0, 0, 0), MOUNTAIN_W)
    canvas.save(LAYER_DIR / "dag.png")
    print("dag.png", canvas.size, "ön kaya", front.size)


# --- Checkpoint ---
CP_SCALE = 4
CP_W, CP_ROWS = 80, 108     # kaktüs alanı (oyun pikseli)
SMOKE_ROOM = 44             # dumanın yükselebilmesi için üstte boş alan
OFF_BOX = (140, 60)
ON_BOX = (620, 60)
OFF_SHIFT = 2               # kapalı kaktüs görselde 2 piksel sağda: üst üste gelsin
LANTERN = (33, 6, 51, 38)   # fener bölgesi; açık görselde parıltı ve duman var, kapalıdan alınır
GLASS = (36, 24, 46, 34)
FLAG = (47, 38, CP_W, 57)
SMOKE_START = (43, 9)
FRAMES = 12

FLAMES = [
    ["...o...", "..oo...", "..oyo..", ".oyyo..", ".oywyo.", ".oywyo.", "..oyo.."],
    ["....o..", "...oo..", "..oyo..", "..oyyo.", ".oywyo.", ".oywyo.", "..oyo.."],
    [".......", "...o...", "..oyo..", "..oyo..", ".oywyo.", ".oywyo.", "..oyo.."],
    ["..o....", "..oo...", ".ooyo..", ".oyyo..", ".oywyo.", ".oywyo.", "..oyo.."],
]
SMALL_FLAME = [".......", ".......", ".......", "...o...", "..oyo..", "..oyo..", "...o..."]
FLAME_COLORS = {"o": (236, 118, 40), "y": (255, 200, 72), "w": (255, 244, 200)}


def bg_near(c):
    r, g, b = c
    return b > r + 2 and b > g + 10 and r > g and abs(r - 45) < 34 and abs(g - 30) < 30 and abs(b - 58) < 34


def sample(src, box):
    """4 pikselde bir oyun pikseli: bloğun ortasından okur, çoğunluğu zeminse saydam bırakır."""
    px = src.load()
    x0, y0 = box
    out = Image.new("RGBA", (CP_W, CP_ROWS), (0, 0, 0, 0))
    for j in range(CP_ROWS):
        for i in range(CP_W):
            cx, cy = x0 + i * CP_SCALE + 2, y0 + j * CP_SCALE + 2
            count = sum(bg_near(px[cx + a, cy + b]) for a in (-1, 0, 1) for b in (-1, 0, 1))
            if count <= 4:
                out.putpixel((i, j), (*px[cx, cy], 255))
    return out


def keep_largest(img):
    """Görseldeki kıvılcım ve duman kırıntıları ana gövdeye bağlı değildir, atılır."""
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


def warm_glass(img):
    """Fener camı içindeki ışık."""
    px = img.load()
    x0, y0, x1, y1 = GLASS
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a and r + g + b > 150:
                px[x, y] = (min(255, int(r * 1.15 + 34)), min(255, int(g * 1.0 + 12)), int(b * 0.62), a)


def draw_flame(img, rows):
    px = img.load()
    for dy, row in enumerate(rows):
        for dx, ch in enumerate(row):
            if ch != ".":
                px[38 + dx, 25 + dy] = (*FLAME_COLORS[ch], 255)


def wave_flag(base, flag_pixels, t):
    """Bayrak sapa bağlı yerde durur, uca doğru artan dalgayla sallanır."""
    out = base.copy()
    px = out.load()
    x0, y0, x1, y1 = FLAG
    for x, y, c in flag_pixels:
        if x > 57:
            px[x, y] = (0, 0, 0, 0)
    for x, y, c in flag_pixels:
        reach = max(0, x - x0) / (x1 - x0)
        dy = round(math.sin(x * 0.32 - t) * (0.6 + 3.2 * reach))
        if 0 <= y + dy < out.height:
            px[x, y + dy] = c
    return out


def flag_pixels_of(img):
    px = img.load()
    x0, y0, x1, y1 = FLAG
    out = []
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a and (r > g * 1.5 or (x > 57 and r < 70)):
                out.append((x, y, px[x, y]))
    return out


def pad_top(img):
    out = Image.new("RGBA", (CP_W, CP_ROWS + SMOKE_ROOM), (0, 0, 0, 0))
    out.alpha_composite(img, (0, SMOKE_ROOM))
    return out


def smoke(frame, index, puffs=7, rise=46):
    """Fenerin tepesinden yükselen duman; döngü başa dikişsiz döner. Yukarıda seyrelerek kaybolur."""
    px = frame.load()
    sx, sy = SMOKE_START[0], SMOKE_START[1] + SMOKE_ROOM
    for p in range(puffs):
        life = ((index / FRAMES) + p / puffs) % 1.0
        cx = sx + math.sin(life * 5.2 + p * 2.1) * (0.8 + 3.2 * life) + life * 6
        cy = sy - life * rise
        radius = 1.6 + 4.2 * life
        tone = int(198 - 66 * life)
        for y in range(int(cy - radius), int(cy + radius) + 1):
            for x in range(int(cx - radius), int(cx + radius) + 1):
                if not (0 <= x < CP_W and 0 <= y < frame.height):
                    continue
                d = (x - cx) ** 2 + (y - cy) ** 2
                if d > radius * radius:
                    continue
                # Yaşlandıkça dama deseniyle seyrelir; gerçek saydamlık piksel sanata uymuyor.
                if life > 0.72 and (x + y + p) % 2:
                    continue
                if life > 0.88 and (x // 2 + y) % 2:
                    continue
                k = 0.86 if y > cy + radius * 0.25 else 1.0
                px[x, y] = (int(tone * k), int((tone - 12) * k), int((tone - 18) * k), 255)


def embers(frame, index, seed):
    rng = random.Random(seed)
    px = frame.load()
    for _ in range(4):
        x0, phase, drift = rng.randint(36, 50), rng.random(), rng.uniform(-5, 6)
        life = ((index / FRAMES) + phase) % 1.0
        x, y = int(x0 + drift * life), int(SMOKE_ROOM + 12 - life * 26)
        if life < 0.75 and 0 <= y < frame.height:
            px[x, y] = (255, 170, 60, 255) if life < 0.4 else (214, 96, 40, 255)


def checkpoint():
    CHECKPOINT_DIR.mkdir(parents=True, exist_ok=True)
    src = Image.open(RAW / "checkpoint_kaktus.jpg").convert("RGB")
    off_raw, on_raw = sample(src, OFF_BOX), sample(src, ON_BOX)

    off = Image.new("RGBA", (CP_W, CP_ROWS), (0, 0, 0, 0))
    off.paste(off_raw, (-OFF_SHIFT, 0))
    keep_largest(off)

    on = on_raw.copy()
    lantern = off.crop(LANTERN)
    # Parıltı ve duman fener çevresinde; temiz kapalı fenerle değiştirilir, kaktüs yeşili korunur.
    pxo, pxl = on.load(), lantern.load()
    for y in range(LANTERN[1], LANTERN[3]):
        for x in range(LANTERN[0], LANTERN[2]):
            c = pxl[x - LANTERN[0], y - LANTERN[1]]
            r, g, b, a = c
            is_green = a and g > r and g > b
            if not is_green:
                pxo[x, y] = c
    keep_largest(on)
    warm_glass(on)

    pad_top(off).save(CHECKPOINT_DIR / "kapali.png")

    flags = flag_pixels_of(on)
    for i in range(FRAMES):
        frame = wave_flag(on, flags, i / FRAMES * 2 * math.pi)
        draw_flame(frame, FLAMES[i % len(FLAMES)])
        frame = pad_top(frame)
        smoke(frame, i)
        embers(frame, i, 7)
        frame.save(CHECKPOINT_DIR / f"aktif_{i}.png")

    # Tutuşma: kapalıdan açığa dama geçişi, alev büyür, ilk duman çıkar.
    order = [(x, y) for y in range(CP_ROWS) for x in range(CP_W)]
    random.Random(1923).shuffle(order)
    for i in range(4):
        amount = (i + 1) / 5
        frame = off.copy()
        px, pon = frame.load(), on.load()
        for x, y in order[:int(len(order) * amount)]:
            px[x, y] = pon[x, y]
        draw_flame(frame, SMALL_FLAME if i < 2 else FLAMES[2])
        frame = pad_top(frame)
        if i >= 2:
            smoke(frame, 0, puffs=i, rise=20)
        frame.save(CHECKPOINT_DIR / f"yanis_{i}.png")
    print("checkpoint kareleri tamam")


def main():
    mountain()
    checkpoint()


if __name__ == "__main__":
    main()
