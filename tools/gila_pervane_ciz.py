"""
Gila'nın süzülme kareleri: düşüş karesinin (fall/gila_fall_0) kuyruk kıvrımı silinir, kuyruk dik yükselir ve ucu
tepede helikopter pervanesi gibi yatay döner (yandan bakınca kısalıp uzar, çevresinde silik pervane diski).

Çıktı: Assets/Sprites/Player/Gila/glide/gila_glide_<0..7>.png  (8 kare bir tur; PlayerVisual.glideFrames)
Çalıştırma: python tools/gila_pervane_ciz.py   (yalnız ilk kez .meta Unity'ye bırakılır ya da GilaSpriteBinder bağlar)
"""
import math
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets/Sprites/Player/Gila/fall/gila_fall_0.png"
OUT_DIR = ROOT / "Assets/Sprites/Player/Gila/glide"

OUTLINE = (4, 0, 1, 255)
MID = (222, 138, 42, 255)
LIGHT = (237, 172, 89, 255)
DARK = (184, 116, 46, 255)
BLUR = (255, 214, 150)

base = Image.open(SRC).convert("RGBA")
# Kuyruk gövdesinin kısaltılması (px): sırttan yükselen kuyruk bu kadar alçalır, pervane de onunla iner.
SHORTEN = 4
TOP = 24 + SHORTEN  # kuyruk gövdesinin kalan ilk satırı

# Kıvrımı sil: tepe kısmı ve iç kıvrım (y 24-27, x 21-28); kuyruk gövdesinin üst SHORTEN satırı da gider
body = base.copy()
for y in range(14, max(28, TOP)):
    for x in range(16, 29):
        if y < TOP or x >= 21:
            body.putpixel((x, y), (0, 0, 0, 0))
# Kesik kuyruğun üstü kapatılmaz: pervane sapı kuyruğun içinden başlar, ikisi tek parça görünür.

PIVOT = (18.5, TOP - 2.0)  # kuyruğun tepesi: pervane sapı buradan yükselir


def seg_dist(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy + 1e-9)))
    cx, cy = ax + vx * t, ay + vy * t
    return math.hypot(px - cx, py - cy), t


def blade_pixels(points, radius_from, radius_to):
    """Çoklu çizgi boyunca kalınlığı azalan kuyruk: piksel -> (renk ağırlığı t)."""
    pix = {}
    total = len(points) - 1
    for y in range(0, 64):
        for x in range(0, 64):
            best = None
            for i in range(total):
                d, t = seg_dist(x + 0.5, y + 0.5, *points[i], *points[i + 1])
                tt = (i + t) / total
                r = radius_from + (radius_to - radius_from) * tt
                if d <= r and (best is None or tt < best):
                    best = tt
            if best is not None:
                pix[(x, y)] = best
    return pix


def tail_points(angle_deg, length, curl, squash=1.0):
    """Göbekten çıkan, ucu kıvrık kuyruk ucu. squash<1: yatay düzlemde dönme (dikey basık)."""
    pts = [PIVOT]
    a = math.radians(angle_deg)
    steps = 6
    for i in range(1, steps + 1):
        f = i / steps
        bend = a + curl * f * f  # uca doğru kıvrılır
        px, py = pts[-1]
        step = length / steps
        dx, dy = math.cos(bend) * step, -math.sin(bend) * step * squash
        pts.append((px + dx, py + dy))
    return pts


def draw_frame(blades, ghosts, dots=None):
    img = body.copy()
    layer = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    # tek piksellik halka ve iz: piksel -> saydamlık (gövdenin arkasında kalır)
    for (x, y), alpha in (dots or {}).items():
        if 0 <= x < 64 and 0 <= y < 64 and img.getpixel((x, y))[3] == 0:
            layer.putpixel((x, y), BLUR + (alpha,))
    # hayalet (hareket bulanıklığı): açık renk, saydam, çizgisiz
    for pts, alpha in ghosts:
        for (x, y), t in blade_pixels(pts, 1.2, 0.6).items():
            if img.getpixel((x, y))[3] == 0:
                layer.putpixel((x, y), BLUR + (alpha,))
    img.alpha_composite(layer)
    for blade in blades:
        # (noktalar, kalın uç, ince uç) ya da yalnız noktalar (kanat kalınlığı)
        pts, r0, r1 = blade if isinstance(blade, tuple) else (blade, 1.9, 1.0)
        pix = blade_pixels(pts, r0, r1)
        # dış çizgi
        for (x, y) in list(pix):
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if q not in pix and 0 <= q[0] < 64 and 0 <= q[1] < 64 and img.getpixel(q)[3] == 0:
                    img.putpixel(q, OUTLINE)
        for (x, y), t in pix.items():
            if body.getpixel((x, y))[3]:
                continue  # kuyruk başın ve şapkanın arkasında kalır
            c = LIGHT if t < 0.25 else MID if t < 0.75 else DARK
            img.putpixel((x, y), c)
    return img


# Pervane diskinin eğimi (derece): disk sağa doğru yükselir, kuyruk ucu çapraz bir eksende döner.
TILT = 25.0
DISC_R, DISC_r = 10.5, 3.0  # diskin büyük ve küçük yarıçapı (yandan basık daire)


def disc_point(hub, angle_deg, scale=1.0, lift=0.0):
    """Eğik diskte bir nokta: önce yatay basık daire, sonra TILT kadar (saat yönünün tersine) döndürülür."""
    a = math.radians(angle_deg)
    x, y = math.cos(a) * DISC_R * scale, -math.sin(a) * DISC_r * scale - lift
    t = math.radians(TILT)
    return (hub[0] + x * math.cos(t) + y * math.sin(t), hub[1] - x * math.sin(t) + y * math.cos(t))


RING_ALPHA = 70     # pervane halkasının silikliği
TRAIL_DEG = 130     # kanat ucunun arkasında kalan iz (derece): hızlı dönüşte uzun bulanıklık
TRAIL_ALPHA = 190   # izin en parlak (uca yakın) yeri


def ring_dots(hub, theta):
    """Pervane halkası: tek piksel kalınlığında, eşit silik eğik elips; kanat ucunun ardında giderek sönen iz."""
    dots = {}
    steps = 360
    for k in range(steps):
        ang = k * 360 / steps
        x, y = disc_point(hub, ang, 1.0, 1.2)
        q = (int(round(x - 0.5)), int(round(y - 0.5)))
        # ucun gerisinde kalan açı (0 = uç, TRAIL_DEG = izin sonu)
        behind = (theta - ang) % 360
        a = RING_ALPHA
        if behind <= TRAIL_DEG:
            a = int(TRAIL_ALPHA - (TRAIL_ALPHA - RING_ALPHA) * behind / TRAIL_DEG)
        dots[q] = max(dots.get(q, 0), a)
    return dots


def version_b(i, n=8):
    """Helikopter pervanesi: kuyruk yükselir, ucu tepede eğik bir halka üzerinde döner (yandan kısalıp uzar)."""
    hub = (PIVOT[0] + 0.5, PIVOT[1] - 4)  # sap göbeğe kadar 4 px uzar
    # Sap kuyruğun içinden (kesik yerin altından) başlar, kuyruğun kendi kalınlığında (3 px dolgu + dış çizgi)
    # yükselir: birleşim yeri dikişsiz, kuyruk kalınlaşmaz.
    stalk = ([(PIVOT[0], TOP + 1.5), PIVOT, hub], 1.4, 1.2)
    theta = i * 360 / n
    blade = [hub, disc_point(hub, theta, 0.5, 0.5), disc_point(hub, theta, 1.0, 1.2)]
    return draw_frame([stalk, (blade, 1.4, 0.9)], [], ring_dots(hub, theta))


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for i in range(8):
        version_b(i, 8).save(OUT_DIR / f"gila_glide_{i}.png")
    print("gila_glide: 8 kare")


if __name__ == "__main__":
    main()
