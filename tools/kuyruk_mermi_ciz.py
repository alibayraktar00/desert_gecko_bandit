"""
Fırlatılan kuyruk (TailProjectile): gergin, hafif S dalgalı Gila kuyruğu. Kopan uç (kalın, kızıl kesik) arkada,
sivri uç önde; uçuş yönüne çevrilir. Renkler Gila'nın kendi kuyruğundan (fall/gila_fall_0.png).

Uçarken dalgalanır: S dalgası kuyruk boyunca kopan uçtan sivri uca doğru akar (FRAMES kare bir dalga).

Çıktı: Assets/Sprites/Player/kuyruk_mermi_<0..FRAMES-1>.png (sağa bakar, merkezden pivotlu, 32 px = 1 birim)
Çalıştırma: python tools/kuyruk_mermi_ciz.py
"""
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "Assets/Sprites/Player"
FRAMES = 6

W, H = 30, 17
OUTLINE = (4, 0, 1, 255)
LIGHT = (237, 172, 89, 255)   # üst parlak
MID = (222, 138, 42, 255)     # gövde
DARK = (184, 116, 46, 255)    # alt gölge
BAND = (155, 53, 27, 255)     # Gila desenindeki kızıl bant
FLESH = (196, 74, 52, 255)    # kopan yerin kesiti
FLESH_D = (132, 40, 30, 255)

LENGTH = 22.0   # kuyruğun boyu (px)
X0 = 4.3        # kopan ucun x'i


def center(t, phase=0.0):
    """Gergin S dalgası: kopan uç ortada, önce hafif yükselir, ortada çöker, uca doğru yukarı kıvrılır.
    phase dalgayı kuyruk boyunca ileri kaydırır; kopan uç daha az, uç daha çok sallanır."""
    return X0 + t * LENGTH, H / 2 + 0.5 + wave(t, phase) - mean_wave(phase)


def wave(t, phase):
    sway = 0.75 + 0.25 * t
    y = -2.0 * sway * math.sin(math.pi * (t * 1.7 - 0.05) - phase)  # tümsek, sonra çukur
    if t > 0.8:
        y -= 2.2 * ((t - 0.8) / 0.2) ** 1.5  # sivri uç yukarı kalkar
    return y


def mean_wave(phase):
    """Dalganın ortalama yüksekliği: kuyruk her karede aynı yerde dursun, yalnız kıvrımı aksın."""
    return sum(wave(i / 50, phase) for i in range(51)) / 51


def radius(t):
    """Kalın kopan uçtan sivri uca incelir."""
    return 2.6 * (1 - t) ** 0.75 + 0.35


def draw(phase):
    # Süper örnekleme: her pikselin içinde 4x4 nokta, kuyruğa düşenlerin oranı ve en yakın t
    pix = {}
    N = 400
    samples = [center(i / N, phase) + (radius(i / N), i / N) for i in range(N + 1)]
    for y in range(H):
        for x in range(W):
            hits, best_t, side = 0, None, 0.0
            for sy in range(4):
                for sx in range(4):
                    px, py = x + (sx + 0.5) / 4, y + (sy + 0.5) / 4
                    for cx, cy, r, t in samples:
                        if (px - cx) ** 2 + (py - cy) ** 2 <= r * r:
                            hits += 1
                            if best_t is None or abs(px - cx) < 0.5:
                                best_t, side = t, (py - cy) / r
                            break
            if hits >= 7:
                pix[(x, y)] = (best_t, side)

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for (x, y), (t, side) in pix.items():
        c = LIGHT if side < -0.35 else DARK if side > 0.45 else MID
        # Gila bantları: kuyruk boyunca üç kızıl halka (uca doğru sıklaşır)
        if any(abs(t - b) < 0.035 for b in (0.3, 0.52, 0.7)) and side > -0.6:
            c = BAND
        # kopan uç: kızıl kesit
        if t < 0.03:
            c = FLESH if side < 0.2 else FLESH_D
        img.putpixel((x, y), c)
    # dış çizgi
    for (x, y) in list(pix):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in pix and 0 <= q[0] < W and 0 <= q[1] < H:
                img.putpixel(q, OUTLINE)
    return img


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for i in range(FRAMES):
        draw(2 * math.pi * i / FRAMES).save(OUT_DIR / f"kuyruk_mermi_{i}.png")
    print(f"kuyruk_mermi: {FRAMES} kare, {W}x{H}")


if __name__ == "__main__":
    main()
