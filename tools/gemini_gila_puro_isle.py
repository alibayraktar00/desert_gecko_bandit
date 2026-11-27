"""
Ana menüdeki direğe yaslanmış, puro tüttüren Gila animasyonunu Gemini çıktılarından oyuna hazır karelere çevirir.

Girdi (Assets/Sprites/_Gelen/gila52, Gemini'den sayfadan indirilmiş, 1376x768, tek sıra 4 kare, gri zemin):
  gila52_01.png ... : 4'erli parçalar. Detaylı taban (gila_hd_renk.png) ve oyundaki Gila (renkler) referansıyla çizildi.
Çıktı (Assets/Sprites/UI/MainMenu/Gila):
  gila_puro_00.png ... : LOOP sırasıyla aynı boyutta kareler, ayaklar alt kenarda (her kare LOOP'taki tık sayısı
  kadar ekranda kalır; sayılar MainMenuBuilder.GilaHolds'a yazdırılır)

Gemini parçalar arası devamlılığı tutamadığı için (ör. duman her parçada küçükten başlıyor) döngü, karelerden
seçilerek kurulur (SEQUENCE: (parça, kare) çiftleri; puroyu ağza geri götürme, indirme karelerinin tersidir).
Gemini görseli yatayda sıkıştırdığından piksel ızgarası iki eksende farklıdır (FACTOR_X / FACTOR_Y); kareler bu
ızgarayla küçültülür, yani her sanat pikseli Gemini'nin bir pikselidir (kare piksel). Sonra:
  1) her kare ilk kareye kaydırılarak hizalanır (ön plan maskesinin örtüşmesi en büyük olacak şekilde),
  2) tek bir palete indirilir (duman grileri ayrı),
  3) BASE karesi "taban" olur. Her kare iki katmana ayrılır: gövde pozu (POSE_REGION içinde tabandan belirgin
     farklı, yeterince büyük piksel kümeleri: kol, puro, ağız, göz; gerisi tabandan) ve duman (gövdenin dışındaki
     açık griler). Direk, kuyruk, bacaklar, şapka böylece hiç kıpırdamaz; LOOP poz ve dumanı ayrı ayrı seçer,
  4) direk inceltilir (Gila'nın dayandığı sol kenar korunur).

Kullanım (proje kökünden):
  python tools/gemini_gila_puro_isle.py [--preview onizleme.png]
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from scipy import ndimage
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen" / "gila52"
OUT = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu" / "Gila"

FRAMES_PER_PART = 4
FACTOR_X = 4.75       # Gemini ızgarası (kaynak pikseli / sanat pikseli), yatay
FACTOR_Y = 5.54       # dikey
PALETTE = 48
SHIFT = 40            # hizalamada denenecek en büyük kayma (kaynak pikseli); bazı parçalarda karakter 30+ px kaymış
CHANGE = 95           # tabandan bu kadar (RGB toplam fark) farklı piksel "değişti" sayılır
MIN_BLOB = 4          # değişen bölge en az bu kadar sanat pikseli olmalı (yoksa ızgara titremesidir)

# Pozun karelerden alınabileceği bölge, direğin sol üst köşesine göre (x0, x1, y0, y1) sanat pikseli: başın önü,
# ağız, göz, puro ve puroyu tutan kol. Bu bölgede bile sadece tabandan gerçekten farklı pikseller kareden gelir;
# dikdörtgen bir kutu bütünüyle kopyalanmaz (Gemini'nin her karede yeniden çizdiği baş kutunun içinde titriyordu).
POSE_REGION = (-64, -6, 38, 78)
HEAD_REF = (-22, 0, 22, 52)   # hizalama için şapka ve başın arkası (puro/kol yok)
BASE = (2, 0)                 # taban kare (parça, kare): temiz kesilmiş, puro ağızda, köz sönük
# Kaynak kareler: bütün parçalar (6 devamlılığı bozduğu için yok)
SEQUENCE = [(p, f) for p in (1, 2, 3, 4, 5, 7, 8, 9, 10) for f in range(4)]
BASE_INDEX = SEQUENCE.index(BASE)
TICK_FPS = 12                 # MainMenuBuilder.GilaFps ile aynı; her kare eşit süre ekranda kalır
# Ana pozlar: (gövde pozu, duman karesi ya da None, bekleme karesi, sonrakine geçiş karesi). Gövde geçişin
# ortasında yeni poza geçer (Gemini'nin gerçek ara kareleri; karıştırınca kol/puro beneklenir). Duman ise geçiş
# boyunca eski yerinden yenisine kayarak Bayer düzeniyle erir, yenisi belirir: büyüyüp dağılması atlamaz.
# Son anahtar ilkine geçer (döngü). Her kare eşit süre (TICK_FPS), ~10 sn.
KEYS = [
    ((2, 0), None, 10, 2),       # kısa bekleme
    ((2, 2), None, 3, 2),        # 1. çekiş: köz parlar
    ((2, 3), None, 4, 2),
    ((3, 1), None, 6, 2),        # gözler kısılır
    ((2, 3), None, 3, 2),
    ((2, 2), None, 2, 2),
    ((2, 0), None, 4, 2),
    ((2, 2), None, 2, 2),        # 2. çekiş, daha derin
    ((2, 3), None, 3, 2),
    ((3, 1), None, 5, 2),
    ((2, 3), None, 2, 2),
    ((2, 0), None, 3, 2),
    ((5, 1), None, 2, 2),        # puroyu indirir
    ((5, 2), None, 2, 2),
    ((5, 3), None, 3, 2),
    ((7, 2), (7, 1), 2, 3),      # ağzı açılır, duman çıkar
    ((7, 3), (7, 3), 2, 4),
    ((7, 3), (8, 3), 3, 4),      # büyük bulut
    ((5, 3), (9, 2), 1, 4),
    ((5, 2), (10, 0), 1, 3),     # duman dağılırken puroyu ağzına götürür
    ((5, 1), (10, 1), 1, 3),
    ((2, 0), (10, 2), 2, 4),
    ((2, 0), (10, 3), 2, 4),
]
POST_EXTEND = 0       # direk zaten şapkanın epey üstüne çıkıyor; tepesine akbaba oturur


def background(rgb):
    """Kenara bağlı düz gri zemin. Gemini zemini tek renk (~122,122,122); direğin grimsi ahşabı ona yakın
    olduğundan sadece o renge çok yakın pikseller zemin sayılır."""
    h, w = rgb.shape[:2]
    bg_color = np.median(np.concatenate([rgb[:4].reshape(-1, 3), rgb[:, :4].reshape(-1, 3)]), axis=0)
    spread = rgb.max(axis=2) - rgb.min(axis=2)
    like = (np.abs(rgb - bg_color).max(axis=2) <= 12) & (spread <= 8)
    # Kenara bağlı zemin ve karakterin içinde kapalı kalan zemin parçaları (kuyruk kıvrımının içi, kolla gövde
    # arası). Zemin tek düz renk olduğu için yeterince büyük her eşleşen bölge zemindir; küçük kırıntılar
    # (gözdeki gri parıltı gibi) çizime aittir.
    labels, n = ndimage.label(like)
    sizes = ndimage.sum(like, labels, index=np.arange(1, n + 1))
    edge = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    keep = np.zeros(n + 1, dtype=bool)
    for k in range(1, n + 1):
        keep[k] = k in edge or sizes[k - 1] >= ENCLOSED_MIN
    return keep[labels]


ENCLOSED_MIN = 60  # kaynak pikseli: bundan büyük kapalı gri alan zemindir


def split_strip(img, fg):
    """Şeridi figürlerine ayırır: (x0, x1) listesi. Gemini kareleri her zaman eşit aralıklı koymadığı için önce
    ön planın sütun doluluğuna bakılır (aralarında boş sütun olan gruplar figürdür). Figürler birbirine değiyorsa
    (direk ile sonraki kuyruk, büyük duman bulutu) tam FRAMES_PER_PART grup çıkmaz; o zaman eşit dilimlenir."""
    occupied = fg.mean(axis=0) > 0.01
    runs, start = [], None
    for x in range(len(occupied) + 1):
        if x < len(occupied) and occupied[x]:
            start = x if start is None else start
        elif start is not None:
            if runs and start - runs[-1][1] < 4:
                runs[-1] = (runs[-1][0], x)
            else:
                runs.append((start, x))
            start = None
    runs = [r for r in runs if r[1] - r[0] > 40]
    if len(runs) == FRAMES_PER_PART:
        return runs
    w = img.shape[1] / FRAMES_PER_PART
    return [(int(round(k * w)), int(round((k + 1) * w))) for k in range(FRAMES_PER_PART)]


def post_right(fg):
    """Figürdeki direğin sağ kenarı: neredeyse tam boy dolu sütunların en sağdakisi."""
    cols = np.nonzero(fg.mean(axis=0) > 0.6)[0]
    return int(cols.max()) + 1 if len(cols) else fg.shape[1]


def load_frames():
    """SEQUENCE'taki kareler ve ön plan maskeleri, hepsi aynı boyutta: zemin bütün şeritte bir kez bulunur (kare
    kenarları figürü kesse de doğru çalışsın), figürler direklerinin sağ kenarı üst üste gelecek şekilde dizilir."""
    cache, cuts = {}, []
    for part, k in SEQUENCE:
        if part not in cache:
            path = SRC / f"gila52_{part:02d}.png"
            if not path.exists():
                raise SystemExit(f"eksik: {path}")
            img = np.asarray(Image.open(path).convert("RGB")).astype(np.int32)
            fg = ~background(img)
            cache[part] = (img, fg, split_strip(img, fg))
        img, fg, runs = cache[part]
        x0, x1 = runs[k]
        c, m = img[:, x0:x1], fg[:, x0:x1].copy()
        # kesim çizgisine değen yabancı parçalar (komşu karenin direği/kuyruğu): sol/sağ kenara değen ince parçalar
        lab, n = ndimage.label(m)
        for i in range(1, n + 1):
            ys_, xs_ = np.nonzero(lab == i)
            if (xs_.min() == 0 or xs_.max() == m.shape[1] - 1) and xs_.max() - xs_.min() < 40:
                m[lab == i] = False
        right = post_right(m)
        cuts.append((c[:, :right], m[:, :right]))
    width = max(c.shape[1] for c, _ in cuts) + 8
    frames, masks = [], []
    for c, m in cuts:
        h, w = c.shape[:2]
        canvas = np.empty((h, width, 3), dtype=np.int32)
        canvas[...] = np.median(c[~m], axis=0) if (~m).any() else 128
        cm = np.zeros((h, width), dtype=bool)
        canvas[:, width - 4 - w:width - 4] = c
        cm[:, width - 4 - w:width - 4] = m
        frames.append(canvas)
        masks.append(cm)
    return frames, masks


def align(ref_mask, mask):
    """mask'ı ref_mask'a en iyi oturtan (dy, dx) kayması: kaydırılmış maskeyle (sarmasız) farkı en az olan.
    Önce yarım çözünürlükte kaba, sonra tam çözünürlükte ±2 piksel ince arama."""
    def cost(r, m, dy, dx):
        return np.count_nonzero(r ^ shift(m, dy, dx, False))
    r2, m2 = ref_mask[::2, ::2], mask[::2, ::2]
    s = SHIFT // 2
    best = min(((cost(r2, m2, dy, dx), dy, dx) for dy in range(-s, s + 1) for dx in range(-s, s + 1)))
    _, by, bx = best
    fine = min(((cost(ref_mask, mask, 2 * by + dy, 2 * bx + dx), 2 * by + dy, 2 * bx + dx)
                for dy in range(-2, 3) for dx in range(-2, 3)))
    return fine[1], fine[2]


def shift(img, dy, dx, fill):
    out = np.empty_like(img)
    out[...] = fill
    h, w = img.shape[:2]
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = img[ys, xs]
    return out


def downscale(rgb, fg, out_w, out_h):
    h, w = rgb.shape[:2]
    fy, fx = h / out_h, w / out_w
    out = np.zeros((out_h, out_w, 4), dtype=np.uint8)
    for y in range(out_h):
        y0, y1 = int(y * fy), max(int(y * fy) + 1, int((y + 1) * fy))
        for x in range(out_w):
            x0, x1 = int(x * fx), max(int(x * fx) + 1, int((x + 1) * fx))
            m = fg[y0:y1, x0:x1]
            if m.mean() >= 0.5:
                out[y, x, :3] = np.median(rgb[y0:y1, x0:x1][m], axis=0)
                out[y, x, 3] = 255
    return out


def is_smoke(px):
    """Duman pikseli: açık ve doygunluğu düşük (gekonun turuncusu, direğin kahvesi değil)."""
    px = px.astype(np.int32)
    return (px.max(axis=-1) - px.min(axis=-1) < 28) & (px.max(axis=-1) > 150)


def quantize_pixels(pixels, colors):
    strip = Image.fromarray(pixels.reshape(1, -1, 3).astype(np.uint8), "RGB")
    pal = strip.quantize(colors=colors, method=Image.Quantize.MEDIANCUT).getpalette()[:colors * 3]
    return np.array(pal, dtype=np.int32).reshape(-1, 3)


def palette_of(sprites, colors):
    """Gövde renkleri ve duman grileri ayrı nicelenir; yoksa az sayıdaki duman pikseli turuncuya çekilir."""
    pixels = np.concatenate([s[s[..., 3] > 0][:, :3] for s in sprites])
    smoke = is_smoke(pixels)
    pal = quantize_pixels(pixels[~smoke], colors)
    if smoke.sum() > 16:
        pal = np.concatenate([pal, quantize_pixels(pixels[smoke], SMOKE_COLORS)])
    return pal


SMOKE_COLORS = 5


def snap(sprite, pal):
    out = sprite.copy()
    opaque = sprite[..., 3] > 0
    px = sprite[opaque][:, :3].astype(np.int32)
    idx = ((px[:, None, :] - pal[None]) ** 2).sum(axis=2).argmin(axis=1)
    out[opaque, :3] = pal[idx]
    return out


def blobs(mask, min_size):
    """min_size'dan küçük bağlı bileşenleri atar."""
    h, w = mask.shape
    keep = np.zeros_like(mask)
    seen = np.zeros_like(mask)
    for sy, sx in zip(*np.nonzero(mask)):
        if seen[sy, sx]:
            continue
        comp, q = [], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            comp.append((y, x))
            for ny in range(y - 1, y + 2):
                for nx in range(x - 1, x + 2):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
        if len(comp) >= min_size:
            ys, xs = zip(*comp)
            keep[list(ys), list(xs)] = True
    return keep


def dilate(mask):
    out = mask.copy()
    out[1:] |= mask[:-1]
    out[:-1] |= mask[1:]
    out[:, 1:] |= mask[:, :-1]
    out[:, :-1] |= mask[:, 1:]
    return out


def extend_post(sprite, rows):
    """Direği yukarı uzatır: en üstteki 12 satırlık direk bandını tekrarlar (dikey damarlı tahta)."""
    h, w = sprite.shape[:2]
    band = sprite[:12].copy()
    x0, width = post_columns(sprite)
    cols = np.zeros(w, dtype=bool)
    cols[x0:x0 + width] = True
    band[:, ~cols] = 0
    top = np.concatenate([band] * (rows // 12 + 1))[:rows]
    return np.concatenate([top, sprite])


def post_columns(sprite):
    """Direğin sütunları (başlangıç, genişlik): tepeden tabana neredeyse hep opak olan en geniş bitişik dizi
    (yanındaki kesik sütunlar şapka/köz kalıntısı)."""
    solid = (sprite[..., 3] > 0).mean(axis=0) > 0.75
    best, start = (0, 0), None
    for x in range(len(solid) + 1):
        if x < len(solid) and solid[x]:
            start = x if start is None else start
        elif start is not None:
            best = max(best, (x - start, start))
            start = None
    return best[1], best[0]


def thin_post(sprite, x0, width):
    """Direği POST_WIDTH sütuna indirir: Gila'nın dayandığı sol POST_KEEP sütun olduğu gibi kalır (temas, şapka),
    geri kalanı eşit aralıklı sütunlar seçilerek sıkıştırılır; sağ kenar çizgisi ve damarlar korunur."""
    rest = width - POST_KEEP
    pick = x0 + POST_KEEP + np.round(np.linspace(0, rest - 1, POST_WIDTH - POST_KEEP)).astype(int)
    cols = np.concatenate([np.arange(x0, x0 + POST_KEEP), pick])
    return np.concatenate([sprite[:, :x0], sprite[:, cols]], axis=1)


POST_KEEP = 3
POST_WIDTH = 8


BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16 + 1 / 32


def piece_shift(a, b, pa, pb, reach):
    """a'daki hareketli parçayı (pa) b'dekine (pb) en iyi oturtan (dy, dx): opaklık ve renk uyuşmazlığı en az."""
    if not pa.any() or not pb.any():
        return 0, 0
    ys, xs = np.nonzero(pa | pb)
    y0, y1 = max(0, ys.min() - reach), ys.max() + reach + 1
    x0, x1 = max(0, xs.min() - reach), xs.max() + reach + 1
    ca, cb = a[y0:y1, x0:x1].astype(int), b[y0:y1, x0:x1].astype(int)
    ma, mb = pa[y0:y1, x0:x1], pb[y0:y1, x0:x1]
    best = None
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            sm = shift(ma.astype(np.uint8), dy, dx, 0).astype(bool)
            sc = shift(ca, dy, dx, 0)
            both = sm & mb
            cost = np.count_nonzero(sm ^ mb) * 120 + np.abs(sc[both, :3] - cb[both, :3]).sum()
            cost += 2 * (dy * dy + dx * dx)   # eşitlikte küçük kayma
            if best is None or cost < best[0]:
                best = (cost, dy, dx)
    return best[1], best[2]


def tween(a, b, f, reach):
    """a ile b arası ara kare (0 < f < 1): farklı pikseller hareketli parça sayılır; a'nınki b'ye doğru kayarak
    Bayer düzeniyle silinir, b'ninki ters yönden gelerek belirir. Ortak (değişmeyen) pikseller olduğu gibi kalır."""
    diff = np.any(a != b, axis=-1)
    pa = diff & (a[..., 3] > 0)
    pb = diff & (b[..., 3] > 0)
    dy, dx = piece_shift(a, b, pa, pb, reach)
    out = a.copy()
    out[diff] = 0
    both = pa & pb
    out[both] = (a if f < 0.5 else b)[both]
    h, w = a.shape[:2]
    thr = np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w]
    pa_img = np.where(pa[..., None], a, 0).astype(np.uint8)
    pb_img = np.where(pb[..., None], b, 0).astype(np.uint8)
    sa = shift(pa_img, int(round(f * dy)), int(round(f * dx)), 0)
    sb = shift(pb_img, -int(round((1 - f) * dy)), -int(round((1 - f) * dx)), 0)
    keep_a = (sa[..., 3] > 0) & (thr >= f)
    keep_b = (sb[..., 3] > 0) & (thr < f)
    first, second = ((sa, keep_a), (sb, keep_b)) if f >= 0.5 else ((sb, keep_b), (sa, keep_a))
    for img, keep in (first, second):
        out[keep] = img[keep]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    frames, masks = load_frames()
    # hizalama sadece gövdeye göre: duman (açık gri) ve üstteki bulut bölgesi hariç
    def body(f, m):
        b = m & ~is_smoke(f)
        b[: int(f.shape[0] * 0.35)] = False
        return b
    bodies = [body(f, m) for f, m in zip(frames, masks)]
    gray = np.array([128, 128, 128])
    aligned, amasks = [], []
    for f, m, b in zip(frames, masks, bodies):
        dy, dx = align(bodies[BASE_INDEX], b) if b is not bodies[BASE_INDEX] else (0, 0)
        aligned.append(shift(f, dy, dx, gray))
        amasks.append(shift(m.astype(np.uint8), dy, dx, 0).astype(bool))
        print(f"kare {len(aligned) - 1:2d}: kayma dy={dy} dx={dx}")

    h, w = frames[0].shape[:2]
    out_w, out_h = int(round(w / FACTOR_X)), int(round(h / FACTOR_Y))
    small = [downscale(f, m, out_w, out_h) for f, m in zip(aligned, amasks)]
    pal = palette_of(small, PALETTE)
    small = [snap(s, pal) for s in small]

    # Taban kare ve direğe göre bölge. Gemini her kareyi baştan çizdiği için gövde her karede biraz farklıdır; bu
    # yüzden bacaklar, kuyruk, karın, şapka ve direk tabandan birebir alınır ve hiç kıpırdamaz.
    base = small[BASE_INDEX]
    by, bx = np.nonzero(base[..., 3] > 0)
    bcrop = base[by.min():by.max() + 1, bx.min():bx.max() + 1]
    bpx0, _ = post_columns(bcrop)
    bpost = bcrop[:, bpx0:bpx0 + 3, 3] > 0
    post_x = bx.min() + bpx0                                       # direğin sol kenarı (sanat pikseli)
    post_y = by.min() + int(np.nonzero(bpost.mean(axis=1) > 0.7)[0].min())   # direğin tepesi
    hgt, wid = base.shape[:2]

    def box(x0, x1, y0, y1):
        m = np.zeros((hgt, wid), dtype=bool)
        m[max(0, post_y + y0):max(0, post_y + y1), max(0, post_x + x0):max(0, post_x + x1)] = True
        return m
    region = box(*POSE_REGION)
    base_op = base[..., 3] > 0
    head_ref = box(*HEAD_REF) & base_op
    body_area = ndimage.binary_dilation(base_op, iterations=1)

    def refine(sm):
        """Kareyi şapka ve başın arkasına göre (±3 piksel) hizalar: aktarılan pikseller tabana otursun."""
        best = None
        for dy in range(-3, 4):
            for dx in range(-3, 4):
                t = shift(sm, dy, dx, 0)
                d = np.abs(t[..., :3].astype(int) - base[..., :3].astype(int)).sum(axis=2)
                d = d + 300 * ((t[..., 3] > 0) != base_op)
                c = d[head_ref].sum()
                if best is None or c < best[0]:
                    best = (c, t)
        return best[1]

    def smoke_mask(t):
        return is_smoke(t[..., :3]) & (t[..., 3] > 0) & ~body_area

    def pose(t):
        """Gövde katmanı: POSE_REGION içinde tabandan belirgin farklı piksel kümeleri kareden, gerisi tabandan."""
        t = t.copy()
        t[smoke_mask(t)] = 0
        d = np.abs(t[..., :3].astype(int) - base[..., :3].astype(int)).sum(axis=2)
        op = t[..., 3] > 0
        change = ((op != base_op) | (op & base_op & (d > CHANGE))) & region
        change = ndimage.binary_dilation(blobs(change, MIN_BLOB), iterations=1) & region
        out = base.copy()
        out[change] = t[change]
        return out

    def smoke(t):
        """Duman katmanı: gövdenin dışındaki açık griler (tek tük kırıntılar hariç)."""
        m = blobs(smoke_mask(t), 2)
        out = np.zeros_like(t)
        out[m] = t[m]
        return out

    refined = {}
    def src(key):
        if key not in refined:
            t = refine(small[SEQUENCE.index(key)])
            # komşu figürden kalan ince dikey çizgiler (kesim artığı): iki yanı boş, 8 pikselden uzun dikey diziler
            op = t[..., 3] > 0
            lone = op.copy()
            lone[:, 1:] &= ~op[:, :-1]
            lone[:, :-1] &= ~op[:, 1:]
            lab, n = ndimage.label(lone, structure=[[0, 1, 0], [0, 1, 0], [0, 1, 0]])
            for i in range(1, n + 1):
                if np.count_nonzero(lab == i) >= 8:
                    t[lab == i] = 0
            refined[key] = t
        return refined[key]

    def layers(key):
        body_key, smoke_key = key[0], key[1]
        return pose(src(body_key)), (smoke(src(smoke_key)) if smoke_key is not None else np.zeros_like(base))

    result = []
    for i, key in enumerate(KEYS):
        nxt = KEYS[(i + 1) % len(KEYS)]
        body_a, smoke_a = layers(key)
        body_b, smoke_b = layers(nxt)
        frames = [(body_a, smoke_a)] * key[2]
        n = key[3]
        frames += [(body_a if (k + 1) / (n + 1) < 0.5 else body_b, tween(smoke_a, smoke_b, (k + 1) / (n + 1), 8))
                   for k in range(n)]
        for body_f, smoke_f in frames:
            frame = body_f.copy()
            m = smoke_f[..., 3] > 0
            frame[m] = smoke_f[m]
            result.append(frame)
    print(f"GilaFrames = {len(result)} ({TICK_FPS} fps, {len(result) / TICK_FPS:.1f} sn)")

    # ortak kırpma: bütün karelerin opak alanını kapsayan kutu, alt kenar ayaklar
    alpha = np.any([r[..., 3] > 0 for r in result], axis=0)
    ys, xs = np.nonzero(alpha)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    result = [r[y0:y1, x0:x1] for r in result]
    px0, pwidth = post_columns(result[0])
    result = [extend_post(thin_post(r, px0, pwidth), POST_EXTEND) for r in result]
    post_alpha = result[0][:, px0:px0 + POST_WIDTH, 3] > 0
    post_top = int(np.nonzero(post_alpha.mean(axis=1) > 0.7)[0].min())
    print(f"direk: sol {px0}, genişlik {POST_WIDTH}, orta {px0 + POST_WIDTH / 2:.1f} (MainMenuBuilder.GilaPostCenterArt), "
          f"tepe {post_top} (GilaPostTopArt)")

    OUT.mkdir(parents=True, exist_ok=True)
    names = {f"gila_puro_{i:03d}.png" for i in range(len(result))}
    for old in OUT.glob("gila_puro_*.png"):   # önceki çalıştırmalardan kalan kareler
        if old.name not in names:
            old.unlink()
            meta = old.with_suffix(".png.meta")
            if meta.exists():
                meta.unlink()
    for i, r in enumerate(result):
        Image.fromarray(r, "RGBA").save(OUT / f"gila_puro_{i:03d}.png")
    print(f"{len(result)} kare -> {OUT} ({result[0].shape[1]}x{result[0].shape[0]})")

    if args.preview:
        ph, pw = result[0].shape[:2]
        sheet = Image.new("RGBA", (pw * 12, ph * ((len(result) + 11) // 12)), (128, 128, 128, 255))
        for i, r in enumerate(result):
            tile = Image.fromarray(r, "RGBA")
            sheet.alpha_composite(tile, ((i % 12) * pw, (i // 12) * ph))
        sheet = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
