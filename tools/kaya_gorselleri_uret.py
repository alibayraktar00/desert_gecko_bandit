"""Katmanlı level parçalarının piksel görsellerini üretir (32 PPU).

Renkler ve doku zemin_toprak.png ile platform_00.png'den alınır, böylece yeni parçalar
oyundaki zemin ve platformlarla aynı paleti kullanır. Rastgelelik sabit tohumludur.
Çalıştırma: python tools/kaya_gorselleri_uret.py
"""
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
GROUND = ROOT / "Assets/Sprites/Environment/Ground/zemin_toprak.png"
OUT = ROOT / "Assets/Sprites/Environment/Katman"

DARK = (0x1A, 0x0C, 0x09)
CRUST = (0x2E, 0x17, 0x10)
CRUST_LIGHT = (0x6E, 0x3A, 0x22)
RIM = (0xB8, 0x6A, 0x38)


def tint(c, k, shift=(0, 0, 0)):
    return tuple(min(255, max(0, int(v * k + s))) for v, s in zip(c, shift))


def strata_tile(ground, w, h, k, shift=(0, 0, 0)):
    """Zemin dokusunun katmanlı orta bandından yatayda ve dikeyde tekrar eden parça."""
    band_top, band_h = 36, 84  # dokunun altı düz dolgu: yalnızca desenli üst kısmı oku
    src = ground.crop((0, band_top, w, band_top + band_h)).convert("RGB")
    out = Image.new("RGB", (w, h))
    for y in range(h):
        # Dikeyde git-gel okuma: alt ve üst kenar birleşir, dikiş görünmez.
        sy = y * 2 if y < h / 2 else (h - 1 - y) * 2
        sy = min(int(sy * band_h / h), band_h - 1)
        for x in range(w):
            out.putpixel((x, y), tint(src.getpixel((x, sy)), k, shift))
    return out


def jagged_profile(w, rng, low, high):
    """Yatayda tekrar eden düzensiz kenar: her sütun için piksel derinliği."""
    points = sorted(rng.sample(range(w), max(3, w // 12)))
    depths = [rng.randint(low, high) for _ in points]
    out = []
    for x in range(w):
        for i, p in enumerate(points):
            nxt = points[(i + 1) % len(points)]
            span = (nxt - p) % w or w
            if (x - p) % w < span:
                t = ((x - p) % w) / span
                out.append(round(depths[i] + (depths[(i + 1) % len(depths)] - depths[i]) * t))
                break
    return out


def back_wall(ground):
    """Mağara arka duvarı: koyu, soğuk; oyuncu ve platformlar öne çıksın."""
    return strata_tile(ground, 128, 128, 0.34, (2, 1, 8)).convert("RGBA")


def rock_mass(ground):
    """Tavan ve dış cephe kütlesi: arka duvardan açık, zeminle aynı renk ailesi."""
    return strata_tile(ground, 128, 128, 0.62).convert("RGBA")


def roof_fringe(rng):
    """Tavanın alt kenarı: kaya kütlesinden sarkan düzensiz çıkıntılar (saydam altlı)."""
    w, h = 128, 20
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    profile = jagged_profile(w, rng, 5, h - 2)
    for x in range(w):
        for y in range(profile[x]):
            edge = profile[x] - y
            c = CRUST_LIGHT if edge == 1 else CRUST if edge <= 3 else DARK
            img.putpixel((x, y), (*c, 255))
    return img


def ledge(rng):
    """Üstünde durulan ince kaya çıkıntısı: 16 px uçlar + tekrar eden orta (sprite kenarlıklı)."""
    w, h = 64, 22
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    profile = jagged_profile(w, rng, 12, h - 2)
    for x in range(w):
        # Uçlar yuvarlansın: kenara yaklaştıkça hem üst hem alt daralır.
        end = min(x, w - 1 - x)
        top = 0 if end >= 4 else (4 - end)
        bottom = profile[x] if end >= 12 else min(profile[x], 7 + end)
        for y in range(top, bottom):
            d = y - top
            if d == 0:
                c = RIM
            elif d <= 2:
                c = CRUST_LIGHT
            elif bottom - y <= 2:
                c = DARK
            else:
                c = CRUST if (x * 5 + y * y * 3) % 17 > 2 else CRUST_LIGHT
            img.putpixel((x, y), (*c, 255))
    return img


def slab_rim():
    """Kat döşemesinin üst kenarı (32x5): yürünen yüzeyi koyu arka duvardan ayırır."""
    w, h = 32, 5
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for x in range(w):
        for y in range(h):
            c = RIM if y == 0 else CRUST_LIGHT if y < 3 else CRUST
            if (x * 3) % 13 == 0 and y == 1:
                c = RIM
            img.putpixel((x, y), (*c, 255))
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rng = random.Random(1923)
    ground = Image.open(GROUND).convert("RGB")
    parts = {
        "kaya_arka.png": back_wall(ground),
        "kaya_kutle.png": rock_mass(ground),
        "kaya_sacak.png": roof_fringe(rng),
        "kaya_kenar.png": ledge(rng),
        "kaya_ust.png": slab_rim(),
    }
    for name, img in parts.items():
        img.save(OUT / name)
        print(name, img.size)


if __name__ == "__main__":
    main()
