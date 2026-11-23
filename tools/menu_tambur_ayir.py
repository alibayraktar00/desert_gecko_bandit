"""
Ana menü revolverinin tamburunu döndürülebilir parçalara ayırır (gemini_menu_isle.py'den sonra çalışır).

Girdi : Assets/Sprites/UI/MainMenu/menu_silah_el.png
Çıktı : menu_silah_bos.png  : fişekleri silinmiş, tamburun arka yüzü ve oluksuz yan yüzü yeniden boyanmış silah
        menu_fisek.png      : tek fişek dibi (pirinç halka + kapsül); oyunda altı kopyası halkada döner
        menu_silah_on.png   : dönen fişeklerin ÜSTÜNDE kalması gereken parçalar (horoz, tetik parmağı)
        menu_tambur_maske.png : tamburun yan yüzü (yazılar ve oluklar yalnızca burada görünür)
        menu_oluk.png       : yan yüzde yuvarlanan oluk çizgisi
        menu_yazi_<ad>_0/1.png : tambura kazınmış menü yazıları (0 çelik, 1 seçili altın), 4x6 piksel font

Halka ve yan yüz ölçüleri (sprite pikseli, sol üstten) aşağıdaki sabitlerde; Unity tarafı
(Assets/Scripts/Editor/MainMenuBuilder.cs, RingCenterPx/RingRadiusPx/Drum*Px) bunlarla aynı olmalı.
FACE_CENTER, FACE_RADIUS veya TEXT_SLOPE_DEG değişirse MainMenuBuilder.cs (FaceCenterPx, FaceRadiusPx, LineAngle) de güncellenir.

Kullanım (proje kökünden):
  python tools/menu_tambur_ayir.py [--preview onizleme.png]
"""
import argparse
import math
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu"

RING_CENTER = (146.4, 85.75)   # fişek halkasının merkezi (göbek)
RING_RADIUS = (19.0, 28.8)     # halkanın yatay / dikey yarıçapı (perspektif)
RIM_RADIUS = (9.5, 11.0)       # bir fişek dibinin dış hattıyla kapladığı alan
TEMPLATE_RIM = 4               # şablon olarak kesilecek fişek (0 = üst, saat yönünde); 4 = sol alt, tam görünür
HAMMER_X = 166                 # horozun sol kenarı: bunun sağı ve HAMMER_Y'nin üstü horoz sayılır
HAMMER_Y = 84
HAMMER_SEED = (175, 39)        # horozun parlak yeri: ön katman buradan dolgu ile bulunur
FINGER_X = 172                 # bunun solundaki turuncu, parmak değil eski fişek kırıntısıdır
FACE_RADIUS = (36.0, 44.0)     # tamburun arka yüzü (ön katman bu elipsin içinde aranır)
FACE_CENTER = (147.0, 83.0)    # yeniden boyanan iç yüz: kenar halkasının içi
FACE_INNER = (34.5, 41.5)
STRAY_MIN_Y = 70                # bunun altında tamburu çevreleyen koyu hat artığı gövdeden silinir
CYL_FACE_RADIUS = (34.5, 44.0)  # ayrılan tamburun arka yüzü (sağında kalan çerçeve gövdede kalır)
HUB_CENTER = (147.5, 84.0)     # ortadaki mil göbeği (korunur)
HUB_RADIUS = (8.5, 9.5)
STEEL = (53, 79, 108)          # yüzün çelik rengi (görselden)
STEEL_LIGHT = (58, 83, 112)


def rim_centers():
    cx, cy = RING_CENTER
    rx, ry = RING_RADIUS
    return [(cx + rx * math.cos(math.radians(-90 + 60 * k)), cy + ry * math.sin(math.radians(-90 + 60 * k)))
            for k in range(6)]


def in_ellipse(shape, center, radius):
    ys, xs = np.mgrid[0:shape[0], 0:shape[1]]
    return ((xs + 0.5 - center[0]) / radius[0]) ** 2 + ((ys + 0.5 - center[1]) / radius[1]) ** 2 <= 1.0


def rim_pixels(rgba):
    """Fişeğe ait renkler: pirinç, kapsülün kahvesi, koyu dış hat. Tamburun çeliği mavimsi (b >= r), fişek sıcak."""
    r, g, b = (rgba[..., i].astype(int) for i in range(3))
    warm = r > b + 8
    dark = rgba[..., :3].max(axis=2) < 60
    return (rgba[..., 3] > 0) & (warm | dark)



# ---- tamburun yan yüzü ----
DRUM_SEED = (85, 80)            # yan yüzün içinde bir nokta (dolgu buradan başlar)
DRUM_BOX = (55, 33, 135, 125)   # x0, y0, x1, y1: dolgu bu kutudan taşmaz
DRUM_X = (62, 110)              # yazı ve oluk çizgilerinin uç noktaları bu iki sütunda ölçülür
DRUM_RAMP = [(35, 50, 75), (53, 79, 108), (100, 118, 139), (168, 175, 159)]  # koyu -> açık (görselden)
OUTLINE = (36, 1, 29)


def drum_mask(rgba):
    """Yan yüzün koyu dış hatla çevrili iç bölgesi; oluk çizgileri (iç delikler) de dahil."""
    h, w = rgba.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    dark = rgba[..., :3].max(axis=2) < 70
    face = in_ellipse(rgba.shape, FACE_CENTER, (FACE_RADIUS[0] + 0.5, FACE_RADIUS[1] - 0.5))
    x0, y0, x1, y1 = DRUM_BOX
    box = (xs >= x0) & (xs <= x1) & (ys >= y0) & (ys <= y1)
    ok = ~dark & (rgba[..., 3] > 0) & ~face & box
    m = np.zeros((h, w), dtype=bool)
    q = deque([DRUM_SEED[::-1]])
    m[DRUM_SEED[1], DRUM_SEED[0]] = True
    while q:
        y, x = q.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if ok[ny, nx] and not m[ny, nx]:
                m[ny, nx] = True
                q.append((ny, nx))
    # delikleri doldur: kutunun kenarına bağlı olmayan her şey yüzeye dahil
    outside = np.zeros((h, w), dtype=bool)
    q = deque((y, x) for y in range(y0, y1 + 1) for x in (x0, x1) if not m[y, x])
    q.extend((y, x) for x in range(x0, x1 + 1) for y in (y0, y1) if not m[y, x])
    for y, x in q:
        outside[y, x] = True
    while q:
        y, x = q.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if x0 <= nx <= x1 and y0 <= ny <= y1 and not m[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                q.append((ny, nx))
    filled = box & ~outside & ~face
    # sütun sütun üstten alta doldur: dış hatla birleşmiş alt oluğun çukuru da yüzeye girsin
    for x in range(x0, x1 + 1):
        col = np.nonzero(filled[:, x])[0]
        if len(col):
            filled[col.min():col.max() + 1, x] = True
    return filled & ~face


def column_span(mask, x):
    ys = np.nonzero(mask[:, x])[0]
    return float(ys.min()), float(ys.max() + 1)


def repaint_drum(rgba, mask):
    """Her sütunda üstten alta silindir gölgesi: tepede ışık, altta koyu; geçişler 2x2 ızgarayla."""
    h, w = rgba.shape[:2]
    for x in range(w):
        col = np.nonzero(mask[:, x])[0]
        if len(col) == 0:
            continue
        top, bot = col.min(), col.max() + 1
        for y in col:
            v = (y + 0.5 - top) / (bot - top)          # 0 tepe .. 1 dip
            shade = max(0.0, 1.0 - 1.25 * abs(v - 0.14)) ** 0.8 * 0.95  # tepeye yakın parlak şerit, dibe doğru koyu
            level = shade * (len(DRUM_RAMP) - 1)
            i = int(level)
            if (level - i) > (0.25 if (x + y) % 2 else 0.75):
                i = min(i + 1, len(DRUM_RAMP) - 1)
            rgba[y, x] = (*DRUM_RAMP[max(0, i)], 255)
    # yazı ve oluk çizgileri için yan yüzün iki ucundaki üst/alt noktalar (sprite pikseli)
    lt, lb = column_span(mask, DRUM_X[0])
    rt, rb = column_span(mask, DRUM_X[1])
    return [(DRUM_X[0], lt), (DRUM_X[0], lb), (DRUM_X[1], rt), (DRUM_X[1], rb)]


def save_flute():
    """Yuvarlanan oluk: koyu yiv, altında ışık alan kenar. Oyunda yan yüz boyunca uzatılır."""
    w = DRUM_X[1] - DRUM_X[0] + 12
    img = np.zeros((3, w, 4), dtype=np.uint8)
    img[0, :] = (*OUTLINE, 255)
    img[1, :] = (*DRUM_RAMP[0], 255)
    img[2, :] = (*DRUM_RAMP[3], 255)
    Image.fromarray(img, "RGBA").save(ART / "menu_oluk.png")


# ---- tambura kazınmış yazılar: 4x6 piksel font (Türkçe harflerle) ----
TEXT_SLOPE_DEG = 13.0  # yazının satırı sağa doğru bu açıyla iner (MainMenuBuilder.LineAngle ile aynı)
LABELS = {"basla": "BAŞLA", "devam": "DEVAM ET", "level": "LEVEL SEÇ", "ayarlar": "AYARLAR", "cikis": "ÇIKIŞ",
          "level1": "LEVEL 1", "level2": "LEVEL 2", "level3": "LEVEL 3", "level4": "LEVEL 4", "geri": "GERİ",
          "ses": "SES", "tamekran": "TAM EKRAN", "sifirla": "SIFIRLA"}
GLYPHS = {
    "A": [".##.", "#..#", "#..#", "####", "#..#", "#..#"],
    "B": ["###.", "#..#", "###.", "#..#", "#..#", "###."],
    "C": [".###", "#...", "#...", "#...", "#...", ".###"],
    "D": ["###.", "#..#", "#..#", "#..#", "#..#", "###."],
    "E": ["####", "#...", "###.", "#...", "#...", "####"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", "###"],
    "K": ["#..#", "#.#.", "##..", "#.#.", "#..#", "#..#"],
    "L": ["#...", "#...", "#...", "#...", "#...", "####"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#"],
    "R": ["###.", "#..#", "###.", "#.#.", "#..#", "#..#"],
    "S": [".###", "#...", ".##.", "...#", "...#", "###."],
    "T": ["###", ".#.", ".#.", ".#.", ".#.", ".#."],
    "V": ["#..#", "#..#", "#..#", "#..#", ".##.", ".##."],
    "Y": ["#..#", "#..#", ".##.", ".#..", ".#..", ".#.."],
    "F": ["####", "#...", "###.", "#...", "#...", "#..."],
    "G": [".###", "#...", "#...", "#.##", "#..#", ".###"],
    "N": ["#..#", "##.#", "#.##", "#..#", "#..#", "#..#"],
    "O": [".##.", "#..#", "#..#", "#..#", "#..#", ".##."],
    "P": ["###.", "#..#", "###.", "#...", "#...", "#..."],
    "U": ["#..#", "#..#", "#..#", "#..#", "#..#", ".##."],
    "Z": ["####", "...#", "..#.", ".#..", "#...", "####"],
    "1": [".#.", "##.", ".#.", ".#.", ".#.", "###"],
    "2": ["###.", "...#", ".##.", "#...", "#...", "####"],
    "3": ["###.", "...#", ".##.", "...#", "...#", "###."],
    "4": ["#..#", "#..#", "####", "...#", "...#", "...#"],
    " ": ["..", "..", "..", "..", "..", ".."],
}
CEDILLA = {"Ç": "C", "Ş": "S"}  # alt çengelli harfler: gövde + altta bir piksel
DOTTED = {"İ": "I", "Ö": "O", "Ü": "U"}  # üst noktalı harfler: gövde + üst pay satırında nokta(lar)


def label_bitmap(text):
    """1 = harf pikseli. Her harf bütün halinde, soldan uzaklığına göre aşağı kaydırılır (TEXT_SLOPE_DEG):
    satır tambur oluklarına paralel iner, harfler dik ve bozulmadan kalır. Üstte 1, altta 1 satır pay."""
    slope = math.tan(math.radians(TEXT_SLOPE_DEG))
    glyphs, x = [], 0
    for ch in text:
        base = DOTTED.get(ch, CEDILLA.get(ch, ch))
        g = tall(GLYPHS[base], base)
        gw = len(g[0])
        glyphs.append((ch, g, x, int(round((x + gw / 2) * slope))))
        x += gw + 1
    width = x - 1
    height = GLYPH_H + 2 + max(dy for *_, dy in glyphs)
    bits = np.zeros((height, width), dtype=np.uint8)
    for ch, g, gx, dy in glyphs:
        for y, row in enumerate(g):
            for xx, c in enumerate(row):
                if c == "#":
                    bits[1 + dy + y, gx + xx] = 1
        if ch in CEDILLA:
            bits[1 + GLYPH_H + dy, gx + len(g[0]) // 2] = 1
        if ch == "İ":
            bits[dy, gx + len(g[0]) // 2] = 1
        elif ch in DOTTED:
            bits[dy, gx] = bits[dy, gx + len(g[0]) - 1] = 1
    return bits


GLYPH_H = 7  # 6 satırlık harfler okunurluk için 7 satıra uzatılır (genişlik aynı, tambura sığar)


def tall(g, ch):
    """Bir satırı tekrarlayarak harfi uzatır: çoğunda üst gövde (1. satır), K'da alt bacak."""
    row = 4 if ch == "K" else 1
    return g[:row + 1] + g[row:]


def render_label(text, selected):
    """Seçili değilse çeliğe kazınmış (koyu harf, altında ışık); seçiliyse koyu hatlı altın harf."""
    bits = label_bitmap(text)
    h, w = bits.shape
    out = np.zeros((h + 2, w + 2, 4), dtype=np.uint8)
    b = np.zeros((h + 2, w + 2), dtype=bool)
    b[1:-1, 1:-1] = bits.astype(bool)
    if selected:
        ring = np.zeros_like(b)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                ring |= np.roll(np.roll(b, dy, 0), dx, 1)
        out[ring & ~b] = (*OUTLINE, 255)
        out[b] = (232, 184, 72, 255)
        top = b & ~np.roll(b, 1, 0)
        out[top] = (251, 230, 168, 255)
    else:
        below = np.roll(b, 1, 0) & ~b
        out[below] = (*DRUM_RAMP[3], 255)
        out[b] = (*DRUM_RAMP[0], 255)
    return out



def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    gun = np.asarray(Image.open(ART / "menu_silah_el.png").convert("RGBA")).copy()
    h, w = gun.shape[:2]
    owned = rim_pixels(gun)
    centers = rim_centers()
    ys, xs = np.mgrid[0:h, 0:w]
    hammer = (xs >= HAMMER_X) & (ys <= HAMMER_Y)

    # 1) şablon fişek: tam görünen bir fişeği elipsle kes
    tx, ty = centers[TEMPLATE_RIM]
    tmask = in_ellipse(gun.shape, (tx, ty), RIM_RADIUS)
    x0, x1 = int(math.floor(tx - RIM_RADIUS[0])), int(math.ceil(tx + RIM_RADIUS[0]))
    y0, y1 = int(math.floor(ty - RIM_RADIUS[1])), int(math.ceil(ty + RIM_RADIUS[1]))
    rim = gun[y0:y1, x0:x1].copy()
    rim[~(tmask[y0:y1, x0:x1] & owned[y0:y1, x0:x1]), 3] = 0
    Image.fromarray(rim, "RGBA").save(ART / "menu_fisek.png")

    # 2) ön katman: tamburun yüzündeki horoz ve tetik parmağı (turuncu)
    face = in_ellipse(gun.shape, RING_CENTER, FACE_RADIUS)
    r, g, b = gun[..., 0].astype(int), gun[..., 1].astype(int), gun[..., 2].astype(int)
    orange = (r > 150) & (g > 70) & (b < 90) & (r - b > 100) & (xs >= FINGER_X)  # tetik parmağı tamburun sağ altında
    # horoz bölgesi dikdörtgen değil horozun kendisi: horozun parlak yerinden koyu dış hattına kadar dolgu,
    # dış hat da dahil. Hattın solunda kalan tambur yüzü şeridi ve eski fişek parçası böylece atılır
    # (yoksa dönen fişeklerin üstünde kare bir köşe kalır).
    dark_px = gun[..., :3].max(axis=2) < 70
    hammer_shape = np.zeros((h, w), dtype=bool)
    q = deque([HAMMER_SEED[::-1]])
    hammer_shape[HAMMER_SEED[1], HAMMER_SEED[0]] = True
    while q:
        y, x = q.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and hammer[ny, nx] and not dark_px[ny, nx]                     and gun[ny, nx, 3] > 0 and not hammer_shape[ny, nx]:
                hammer_shape[ny, nx] = True
                q.append((ny, nx))
    edge = np.zeros_like(hammer_shape)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
        edge |= np.roll(np.roll(hammer_shape, dy, 0), dx, 1)
    hammer_shape |= edge & dark_px & hammer
    front_mask = (gun[..., 3] > 0) & (face | hammer) & (hammer_shape | orange)
    # turuncunun koyu dış hattı da önde kalsın
    grow = front_mask.copy()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        grow |= np.roll(np.roll(front_mask, dy, 0), dx, 1) & (gun[..., :3].max(axis=2) < 60) & (gun[..., 3] > 0)
    front = gun.copy()
    front[~grow, 3] = 0
    Image.fromarray(front, "RGBA").save(ART / "menu_silah_on.png")

    # 3) tamburun arka yüzünü baştan boya: kenar ve göbek kalır, iç kısım düz çelik (sol üstten hafif ışık).
    #    Fişeklerin eski yerleri böylece iz bırakmaz; altı fişek oyunda bu yüzeyin üstünde döner.
    inner = in_ellipse(gun.shape, FACE_CENTER, FACE_INNER)
    hub = in_ellipse(gun.shape, HUB_CENTER, HUB_RADIUS)
    warm_any = (gun[..., 0].astype(int) > gun[..., 2].astype(int) + 8) & (gun[..., 3] > 0)
    repaint = (inner & ~hub & ~grow & ((xs < HAMMER_X + 2) | owned)) | (face & warm_any & ~grow & ~orange)
    empty = gun.copy()
    light = ((xs - FACE_CENTER[0]) / FACE_INNER[0] + (ys - FACE_CENTER[1]) / FACE_INNER[1]) < -0.9
    bayer = (xs % 2) ^ (ys % 2)
    empty[repaint] = (*STEEL, 255)
    empty[repaint & light & (bayer == 1)] = (*STEEL_LIGHT, 255)
    empty[repaint & (((xs - FACE_CENTER[0]) / FACE_INNER[0] + (ys - FACE_CENTER[1]) / FACE_INNER[1]) < -1.15)] = (*STEEL_LIGHT, 255)

    # 4) tamburun yan yüzü: oluksuz düz silindir olarak boya. Oluklar ve menü yazıları oyunda bu yüzeyin
    #    üstünde yuvarlanır (RevolverMenu), maskesi menu_tambur_maske.png.
    drum = drum_mask(empty)
    corners = repaint_drum(empty, drum)
    mask = np.zeros_like(empty)
    mask[drum] = (255, 255, 255, 255)
    Image.fromarray(mask, "RGBA").save(ART / "menu_tambur_maske.png")

    # 5) tamburu silahtan ayır: menüde dışarı açık durur, seçince yerine oturur. Tambur = yan yüz + arka yüz
    #    (+ koyu dış hattı); gövdede o bölge boşalır, tambur açıkken çerçevenin içinden arka plan görünür.
    cyl = drum | (in_ellipse(gun.shape, FACE_CENTER, CYL_FACE_RADIUS) & ~grow)
    outline_px = empty[..., :3].max(axis=2) < 70
    ring_px = np.zeros_like(cyl)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        ring_px |= np.roll(np.roll(cyl, dy, 0), dx, 1)
    cyl |= ring_px & outline_px & (empty[..., 3] > 0) & ~grow
    cylinder = empty.copy()
    # orijinal çizimde horozun ve tetik parmağının altında kalan yüz parçası hiç çizilmemiş: tambur dışarı
    # açılınca çentik görünmesin diye arka yüz tam elips olarak tamamlanır (çelik dolgu + koyu dış hat)
    full_face = in_ellipse(gun.shape, FACE_CENTER, CYL_FACE_RADIUS)
    edge = full_face & ~(np.roll(full_face, 1, 0) & np.roll(full_face, -1, 0) & np.roll(full_face, 1, 1) & np.roll(full_face, -1, 1))
    hidden = full_face & (grow | (empty[..., 3] == 0))
    cylinder[hidden] = (*STEEL, 255)
    cylinder[hidden & edge] = (*OUTLINE, 255)
    cyl |= full_face
    cylinder[~cyl, 3] = 0
    # arka yüz (fişekli yüz) ayrı katman: gövdenin önünde çizilir, yan yüz gövdenin arkasında kalır
    face_layer = cylinder.copy()
    face_layer[~full_face, 3] = 0
    Image.fromarray(face_layer, "RGBA").save(ART / "menu_tambur_yuz.png")
    side_layer = cylinder.copy()
    side_layer[full_face, 3] = 0
    Image.fromarray(side_layer, "RGBA").save(ART / "menu_tambur.png")
    empty[cyl, 3] = 0
    # tamburun eski dış hattından gövdede kalan koyu çizgiler (alt yarıda): tambur yukarı kayınca havada kalmasın
    stray = np.zeros_like(cyl)
    grown = cyl.copy()
    for _ in range(3):
        nxt = grown.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            nxt |= np.roll(np.roll(grown, dy, 0), dx, 1)
        add = nxt & ~grown & (empty[..., :3].max(axis=2) < 90) & (empty[..., 3] > 0) & (ys >= STRAY_MIN_Y) & ~grow
        stray |= add
        grown |= add
    empty[stray, 3] = 0
    # geride kalan tek tük kopuk pikseller
    alpha = empty[..., 3] > 0
    seen = np.zeros_like(alpha)
    for sy, sx in zip(*np.nonzero(alpha)):
        if seen[sy, sx]:
            continue
        comp, q = [(sy, sx)], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and alpha[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
                    comp.append((ny, nx))
        if len(comp) < 10:
            for y, x in comp:
                empty[y, x, 3] = 0
    Image.fromarray(empty, "RGBA").save(ART / "menu_silah_bos.png")
    save_flute()
    for key, text in LABELS.items():
        for selected in (False, True):
            img = render_label(text, selected)
            Image.fromarray(img, "RGBA").save(ART / f"menu_yazi_{key}_{int(selected)}.png")
    print("menu_fisek.png", rim.shape[1], "x", rim.shape[0], "| halka merkezi", RING_CENTER, "yaricap", RING_RADIUS)

    if args.preview:
        sheet = Image.new("RGBA", (w * 3, h), (90, 70, 60, 255))
        rim_img = Image.fromarray(rim, "RGBA")
        for i, turn in enumerate((0, 20, 40)):
            frame = Image.fromarray(empty, "RGBA")
            for k in range(6):
                a = math.radians(-90 + 60 * k + turn)
                px = RING_CENTER[0] + RING_RADIUS[0] * math.cos(a) - rim.shape[1] / 2
                py = RING_CENTER[1] + RING_RADIUS[1] * math.sin(a) - rim.shape[0] / 2
                frame.alpha_composite(rim_img, (round(px), round(py)))
            frame.alpha_composite(Image.fromarray(front, "RGBA"))
            sheet.alpha_composite(frame, (w * i, 0))
        sheet.crop((0, 0, w * 3, h)).resize((w * 6, h * 2), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
