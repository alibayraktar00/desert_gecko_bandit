"""
Ana menünün "ateş" anındaki gerçekçi revolverini hazırlar (gemini_menu_isle.py ve menu_tambur_ayir.py'den sonra).

Menüde büyük, yazılı tamburlu silah durur; seçince üstüne pus çöker, pus dağılırken tamburu çerçeveye tam
oturmuş gerçekçi silah görünür ve ateş eder. O silahı da Gila'nın eli tutar: eski görseldeki el (parmaklar,
elin sırtı, bileklik, kol) ayrı katman olarak çıkarılır, yeni silah elin arkasına kabzası parmakların altına
gelecek şekilde konur.

Girdi : Assets/Sprites/_Gelen/menu_silah_gercekci.jpg (Gemini, elsiz silah, gri zemin)
        Assets/Sprites/UI/MainMenu/menu_silah_el.png
Çıktı : menu_gercekci_silah.png : elsiz gerçekçi silah, eski silah görseliyle aynı tuval ve hizada
        menu_el.png             : yalnızca Gila'nın eli ve kolu (aynı tuval)
        menu_pus.png            : geçişte silahın üstüne çöken pikselli toz bulutu
        menu_toz_bulut.png      : sahne geçişinde soldan sağa savrulan yarı saydam toz topağı

Kullanım (proje kökünden):
  python tools/menu_gercekci_silah.py [--preview onizleme.png]
"""
import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gemini_menu_isle import downscale, gray_background, quantize  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen" / "menu_silah_gercekci.jpg"
ART = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu"

GUN_WIDTH = 400            # yeni silahın genişliği (oyun pikseli)
GRIP_TARGET = (242, 150)   # yeni kabzanın merkezi buraya: parmak uçları kabzanın önünü sarar, fazla taşmaz
TRIGGER_FINGER_AT = (197, 111)  # eski işaret parmağının yeni silahta tetiğin önüne geldiği merkez
HAND_MIN_X = 172           # bunun solundaki turuncu, el değil (fişek pirinci)
TRIGGER_FINGER_MAX_X = 200       # ağırlık merkezi bunun solundaki el parçası eski tetik parmağıdır
CUFF_MIN_X = 296           # bilekliğin kahvesi bu x'ten sonra (soldaki kahve kabzanın tahtası)
OUTLINE = (36, 1, 29)
BACKSTRAP_ZONE = [(226, 86), (258, 86), (284, 198), (264, 198), (246, 112), (226, 112)]  # kahve leke bölgesi
STRAY_BOX = (286, 172, 312, 217)  # x0, y0, x1, y1: avucun altındaki eski kabza çizgisi burada silinir


def gun_sprite():
    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.int32)
    fg = ~gray_background(rgb)
    ys, xs = np.nonzero(fg)
    rgb, fg = rgb[ys.min():ys.max() + 1, xs.min():xs.max() + 1], fg[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h = round(GUN_WIDTH * rgb.shape[0] / rgb.shape[1])
    return quantize(downscale(rgb, fg, GUN_WIDTH, h), 40)


def grip_center(sprite):
    """Kabzanın tahtası: sıcak kahverengi pikseller; ağırlık merkezi."""
    r, g, b = (sprite[..., i].astype(int) for i in range(3))
    wood = (sprite[..., 3] > 0) & (r > b + 40) & (r > 80) & (g < r)
    ys, xs = np.nonzero(wood)
    return xs.mean(), ys.mean()


def hand_layer(old):
    """Eski görselden yalnızca el: turuncu deri, bileklik ve bunlara değen koyu dış hat."""
    h, w = old.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    r, g, b = (old[..., i].astype(int) for i in range(3))
    opaque = old[..., 3] > 0
    # Arka kayış bölgesinde eski ceviz kabzanın açık kahve tonları (165,93,50 gibi) ele karışmasın: orada yalnızca
    # gerçek ten (r >= 180) sayılır; parmaklar dışarıda kaldığı için gölge tonlarını korur.
    from PIL import ImageDraw
    zone_img = Image.new("L", (w, h), 0)
    ImageDraw.Draw(zone_img).polygon(BACKSTRAP_ZONE, fill=255)
    zone = np.asarray(zone_img) > 0
    skin = opaque & (r > 150) & (g > 60) & (r - b > 80) & (xs >= HAND_MIN_X) & (~zone | (r >= 180))
    cuff = opaque & (r > b + 25) & (r < 150) & (xs >= CUFF_MIN_X)
    body = skin | cuff
    # sadece büyük parçalar (eldeki çizgiler, bileklik); kopuk kırıntılar atılır
    keep = np.zeros_like(body)
    finger = np.zeros_like(body)
    seen = np.zeros_like(body)
    for sy, sx in zip(*np.nonzero(body)):
        if seen[sy, sx]:
            continue
        comp, q = [(sy, sx)], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and body[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
                    comp.append((ny, nx))
        # eski işaret parmağı (silahın önünde, tek başına duran parça) ayrı tutulur: yeni tetiğe taşınır
        if len(comp) >= 40:
            target = keep if np.mean([x for _, x in comp]) >= TRIGGER_FINGER_MAX_X else finger
            for y, x in comp:
                target[y, x] = True
    # elin içindeki koyu çizgiler (parmak araları, çizgiler) ve çevresindeki dış hat
    dark = opaque & (old[..., :3].max(axis=2) < 110)
    # yalnızca ele doğrudan değen koyu pikseller (eski kabzanın uzun dış hat çizgileri kalmasın)
    def with_outline(m):
        ring = np.zeros_like(m)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            ring |= np.roll(np.roll(m, dy, 0), dx, 1)
        return fill_holes(m | (ring & dark)) & opaque

    grown = drop_small(with_outline(keep), 60)
    # avuç içinin altından sarkan eski kabza dış hattı (koyu ince çizgi)
    x0, y0, x1, y1 = STRAY_BOX
    grown[y0:y1, x0:x1] &= ~dark[y0:y1, x0:x1]
    out = old.copy()
    out[~grown, 3] = 0
    fm = with_outline(finger)
    ys_f, xs_f = np.nonzero(fm)
    tip = old[ys_f.min():ys_f.max() + 1, xs_f.min():xs_f.max() + 1].copy()
    tip[~fm[ys_f.min():ys_f.max() + 1, xs_f.min():xs_f.max() + 1], 3] = 0
    return out, tip


def fill_holes(mask):
    """Maskenin içinde kalan kapalı boşlukları (parmak yastıklarının açık renkli tepeleri gibi) doldurur."""
    h, w = mask.shape
    outside = np.zeros_like(mask)
    q = deque((y, x) for y in range(h) for x in (0, w - 1) if not mask[y, x])
    q.extend((y, x) for x in range(w) for y in (0, h - 1) if not mask[y, x])
    for y, x in q:
        outside[y, x] = True
    while q:
        y, x = q.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and not mask[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                q.append((ny, nx))
    return ~outside


def drop_small(mask, min_size):
    """Küçük kopuk parçaları (eski kabzanın dış hat artıkları) atar."""
    h, w = mask.shape
    out = np.zeros_like(mask)
    seen = np.zeros_like(mask)
    for sy, sx in zip(*np.nonzero(mask)):
        if seen[sy, sx]:
            continue
        comp, q = [(sy, sx)], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
                    comp.append((ny, nx))
        if len(comp) >= min_size:
            for y, x in comp:
                out[y, x] = True
    return out


# ---- eli yeni kabzaya oturtma (kullanıcının çizdiği gibi) ----
# Parmaklar: kabzanın önünde yalnızca yuvarlak uçları (yastıklar) görünür, gövdeleri kabzanın arkasına sarılır.
FINGER_BOX = (190, 110, 250, 190)                          # x0, y0, x1, y1: üç parmağın bulunduğu bölge
# yastık + parmağın uca yakın bir parçası görünür (elips sola uzatıldı); daha solu kabzanın arkasına sarılır
FINGER_PADS = [((226, 128), (18, 12)), ((230, 152), (17, 12)), ((223, 176), (16, 10))]  # merkez, yarıçap
# Avuç içi: kabzanın arka kayışı ile elin sırtı/bileklik arasındaki boşluk (çokgen, tuval pikseli)
# Avuç içi kabzanın arka kayışının ÜSTÜNE biner: kullanıcının işaretlediği iki çizgi arası (üst (246,96)-(261,91),
# alt (278,169)-(296,160)); solu kayışın ön kenarı boyunca, sağı elin sırtına kavuşur.
PALM = [(246, 96), (261, 90), (298, 160), (278, 170)]
SKIN = [(184, 116, 46), (217, 136, 44), (234, 168, 84)]    # koyu, ana, açık deri


def in_ellipse_px(shape, center, radius):
    ys, xs = np.mgrid[0:shape[0], 0:shape[1]]
    return ((xs + 0.5 - center[0]) / radius[0]) ** 2 + ((ys + 0.5 - center[1]) / radius[1]) ** 2 <= 1.0


def outline_of(mask):
    ring = np.zeros_like(mask)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        ring |= np.roll(np.roll(mask, dy, 0), dx, 1)
    return ring & ~mask


def trim_fingers(hand):
    """Parmak bölgesinde yalnızca yastıklar (ve onların dış hattı) kalır."""
    out = hand.copy()
    h, w = hand.shape[:2]
    pads = np.zeros((h, w), dtype=bool)
    for c, r in FINGER_PADS:
        pads |= in_ellipse_px(hand.shape, c, r)
    x0, y0, x1, y1 = FINGER_BOX
    box = np.zeros((h, w), dtype=bool)
    box[y0:y1, x0:x1] = True
    cut = box & ~pads
    out[cut, 3] = 0
    # kesilen kenara koyu hat: yastığın kendi sınırı
    edge = outline_of(pads & (out[..., 3] > 0)) & box
    out[edge] = (*OUTLINE, 255)
    return out


def add_palm(hand, gun):
    """Avuç içini silahın önüne çizer (elin zaten olduğu yerlere dokunmaz): sol kenarı gölgede, kayışa
    sarılan kenarı koyu hatlı; ortası ana ten, sağa doğru elin sırtıyla birleşir."""
    out = hand.copy()
    from PIL import ImageDraw
    img = Image.new("L", (hand.shape[1], hand.shape[0]), 0)
    ImageDraw.Draw(img).polygon(PALM, fill=255)
    poly = np.asarray(img) > 0
    palm = poly & (hand[..., 3] == 0)
    if not palm.any():
        return out
    # sol kenardan uzaklık: kayışa sarılan kenar koyu, içe doğru açılır
    left = np.zeros_like(palm)
    for d in range(1, 4):
        left |= ~np.roll(poly, d, 1)
    out[palm] = (*SKIN[1], 255)
    out[palm & left] = (*SKIN[0], 255)
    out[palm & ~left & ~np.roll(poly, -5, 1)] = (*SKIN[2], 255)
    edge = outline_of(poly) & (hand[..., 3] == 0)
    out[edge] = (*OUTLINE, 255)
    return out


# Avucun alt sınırı (kullanıcının yeşil çizgisi): bunun altında kalan eski el parçası silinir, çizgi boyunca hat çekilir.
PALM_BOTTOM = ((262, 177), (302, 160))   # sol ve sağ uç (tuval pikseli)
PALM_BOTTOM_X = (258, 304)               # silmenin uygulandığı x aralığı (sağında bileklik başlar)


def cut_below_palm(hand):
    out = hand.copy()
    h, w = hand.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    (x0, y0), (x1, y1) = PALM_BOTTOM
    line_y = y0 + (xs - x0) * (y1 - y0) / (x1 - x0)
    band = (xs >= PALM_BOTTOM_X[0]) & (xs <= PALM_BOTTOM_X[1])
    below = band & (ys > line_y + 0.5)
    out[below, 3] = 0
    edge = band & (np.abs(ys - line_y) <= 0.5) & (out[..., 3] > 0)
    out[edge] = (*OUTLINE, 255)
    return out


PUFF_SIZE = 64
PUFF_COLORS = [(176, 140, 104), (212, 180, 136), (236, 214, 176)]  # koyu, orta, açık toz (çöl paleti)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def puff_sprite():
    """Pikselli toz bulutu: yuvarlak, kenarı 4x4 ızgarayla seyrelir, üstü açık altı koyu."""
    n = PUFF_SIZE
    ys, xs = np.mgrid[0:n, 0:n] + 0.5
    d = np.hypot(xs - n / 2, ys - n / 2) / (n / 2)
    alpha = np.clip((1.0 - d) / 0.45, 0, 1)
    solid = alpha > BAYER[(ys.astype(int)) % 4, (xs.astype(int)) % 4]
    light = (ys - n / 2) / (n / 2) - (xs - n / 2) / (n / 2) * 0.3
    tone = np.where(light < -0.25, 2, np.where(light < 0.3, 1, 0))
    out = np.zeros((n, n, 4), dtype=np.uint8)
    for i, c in enumerate(PUFF_COLORS):
        out[solid & (tone == i)] = (*c, 255)
    return out


CLOUD_SIZE = 96
CLOUD_COLORS = [(196, 168, 128), (220, 198, 160), (240, 226, 200)]  # gölge, orta, ışık (açık kum)


def cloud_sprite():
    """Toz geçişinin topağı: birkaç daireden oluşan kabarık bulut; kenarlara doğru gerçek saydamlıkla (alfa
    basamaklı) söner, üstü ışık alır. Oyunda onlarcası soldan sağa savrulur (DustTransition)."""
    n = CLOUD_SIZE
    ys, xs = np.mgrid[0:n, 0:n] + 0.5
    rng = np.random.default_rng(11)
    lobes = [(n / 2, n / 2 + 4, n * 0.30)] + [
        (n / 2 + rng.uniform(-n * 0.2, n * 0.2), n / 2 + rng.uniform(-n * 0.12, n * 0.08), n * rng.uniform(0.16, 0.24))
        for _ in range(5)]
    dens = np.zeros((n, n))
    for cx, cy, r in lobes:
        dens = np.maximum(dens, 1.0 - np.hypot(xs - cx, ys - cy) / r)
    alpha = np.clip(dens / 0.6, 0, 1) ** 1.4
    alpha = np.round(alpha * 6) / 6 * 0.9        # 6 basamak, en yoğun yerinde bile %90
    up = np.zeros((n, n))
    for cx, cy, r in lobes:
        up = np.maximum(up, 1.0 - np.hypot(xs - cx, ys + 4 - cy) / r)
    light = dens - up                               # lobların üst kenarı aydınlık
    tone = np.where(light < -0.04, 2, np.where(light < 0.03, 1, 0))
    out = np.zeros((n, n, 4), dtype=np.uint8)
    for i, c in enumerate(CLOUD_COLORS):
        m = tone == i
        out[m, :3] = c
    out[..., 3] = (alpha * 255).astype(np.uint8)
    return out


def muzzle_point(canvas):
    """Gerçekçi silahın namlu ucu: en soldaki dolu sütunun ortası (tuval pikseli)."""
    a = np.asarray(canvas)[..., 3] > 0
    cols = np.nonzero(a.any(axis=0))[0]
    x = cols.min()
    ys = np.nonzero(a[:, x:x + 3].any(axis=1))[0]
    return x, (ys.min() + ys.max() + 1) / 2


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    old = np.asarray(Image.open(ART / "menu_silah_el.png").convert("RGBA"))
    gun = gun_sprite()
    gx, gy = grip_center(gun)
    canvas = Image.new("RGBA", (old.shape[1], old.shape[0]), (0, 0, 0, 0))
    ox, oy = round(GRIP_TARGET[0] - gx), round(GRIP_TARGET[1] - gy)
    canvas.alpha_composite(Image.fromarray(gun, "RGBA"), (max(0, ox), max(0, oy)), (max(0, -ox), max(0, -oy)))
    canvas.save(ART / "menu_gercekci_silah.png")

    hand, tip = hand_layer(old)
    hand = trim_fingers(hand)
    hand = add_palm(hand, np.asarray(canvas))
    hand = cut_below_palm(hand)
    hand_img = Image.fromarray(hand, "RGBA")
    tx, ty = TRIGGER_FINGER_AT  # eski çizimin işaret parmağı, tetiğin önünde
    hand_img.alpha_composite(Image.fromarray(tip, "RGBA"), (round(tx - tip.shape[1] / 2), round(ty - tip.shape[0] / 2)))
    hand_img.save(ART / "menu_el.png")
    hand = np.asarray(hand_img)
    Image.fromarray(puff_sprite(), "RGBA").save(ART / "menu_pus.png")
    Image.fromarray(cloud_sprite(), "RGBA").save(ART / "menu_toz_bulut.png")
    mx, my = muzzle_point(canvas)
    print(f"gercekci silah {gun.shape[1]}x{gun.shape[0]}, kabza merkezi ({gx:.0f},{gy:.0f}) -> kaydirma ({ox},{oy}), "
          f"namlu ucu ({mx},{my:.1f})")

    if args.preview:
        bg = Image.open(ART / "menu_arka_plan.png").convert("RGBA").crop((270, 149, 270 + old.shape[1], 149 + old.shape[0]))
        f = bg.copy()
        f.alpha_composite(canvas)
        f.alpha_composite(Image.fromarray(hand, "RGBA"))
        f.resize((f.width * 3, f.height * 3), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
