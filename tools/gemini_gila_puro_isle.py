"""
Ana menüdeki direğe yaslanmış, puro tüttüren Gila animasyonunu Gemini çıktılarından oyuna hazır karelere çevirir.

Girdi (Assets/Sprites/_Gelen, Gemini'den sayfadan indirilmiş, 2064x512, tek sıra 4 kare, gri #808080 zemin):
  gila_puro_parca1.png ... gila_puro_parca6.png : 24 karelik döngünün 4'erli parçaları
Çıktı (Assets/Sprites/UI/MainMenu/Gila):
  gila_puro_00.png ... gila_puro_23.png : aynı boyutta kareler, ayaklar alt kenarda, direk üste doğru uzatılmış

Gemini'nin piksel ızgarası kareden kareye biraz değiştiği için her kare doğrudan küçültülürse gövde titrer. Bu yüzden:
  1) her kare ilk kareye kaydırılarak hizalanır (ön plan maskesinin örtüşmesi en büyük olacak şekilde),
  2) hepsi aynı sabit oranla küçültülür (blok medyanı) ve tek bir palete indirilir,
  3) ilk kare "taban" olur; her karede sadece tabandan belirgin farklı, yeterince büyük bölgeler (kol, puro, yüz,
     göğüs, duman) o karenin kendi piksellerinden alınır. Direk, kuyruk ve bacaklar böylece kıpırdamaz.

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
SRC = ROOT / "Assets" / "Sprites" / "_Gelen"
OUT = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu" / "Gila"

PARTS = [f"gila_puro_parca{i}.png" for i in range(1, 7)]
FRAMES_PER_PART = 4
FACTOR = 5.0          # kaynakta bir oyun pikseli kaç piksel (Gemini ızgarası ~9.6x12.5; yarısı kadar ince örnekle)
PALETTE = 40
SHIFT = 24            # hizalamada denenecek en büyük kayma (kaynak pikseli)
CHANGE = 95           # tabandan bu kadar (RGB toplam fark) farklı piksel "değişti" sayılır
MIN_BLOB = 10          # değişen bölge en az bu kadar oyun pikseli olmalı (yoksa ızgara titremesidir)
POST_EXTEND = 28      # direği yukarı doğru bu kadar oyun pikseli uzat (tepesine akbaba oturur)


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


def load_frames():
    frames = []
    for name in PARTS:
        path = SRC / name
        if not path.exists():
            raise SystemExit(f"eksik: {path}")
        img = np.asarray(Image.open(path).convert("RGB")).astype(np.int32)
        w = img.shape[1] / FRAMES_PER_PART
        for k in range(FRAMES_PER_PART):
            frames.append(img[:, int(round(k * w)):int(round((k + 1) * w))])
    return frames


def align(ref_mask, mask):
    """mask'ı ref_mask'a en iyi oturtan (dy, dx) kayması."""
    small_r = ref_mask[::2, ::2].astype(np.float32)
    small_m = mask[::2, ::2].astype(np.float32)
    best, arg = -1.0, (0, 0)
    s = SHIFT // 2
    for dy in range(-s, s + 1):
        for dx in range(-s, s + 1):
            shifted = np.roll(np.roll(small_m, dy, axis=0), dx, axis=1)
            score = (shifted * small_r).sum()
            if score > best:
                best, arg = score, (dy * 2, dx * 2)
    return arg


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
    solid = (sprite[..., 3] > 0).mean(axis=0) > 0.85
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


POST_KEEP = 6
POST_WIDTH = 13


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    frames = load_frames()
    masks = [~background(f) for f in frames]
    gray = np.array([128, 128, 128])
    aligned, amasks = [frames[0]], [masks[0]]
    for f, m in zip(frames[1:], masks[1:]):
        dy, dx = align(masks[0], m)
        aligned.append(shift(f, dy, dx, gray))
        amasks.append(shift(m.astype(np.uint8), dy, dx, 0).astype(bool))
        print(f"kare {len(aligned) - 1:2d}: kayma dy={dy} dx={dx}")

    h, w = frames[0].shape[:2]
    out_w, out_h = int(round(w / FACTOR)), int(round(h / FACTOR))
    small = [downscale(f, m, out_w, out_h) for f, m in zip(aligned, amasks)]
    pal = palette_of(small, PALETTE)
    small = [snap(s, pal) for s in small]

    base = small[0]
    result = [base]
    for s in small[1:]:
        diff = np.abs(s[..., :3].astype(int) - base[..., :3].astype(int)).sum(axis=2)
        changed = (diff > CHANGE) | ((s[..., 3] > 0) != (base[..., 3] > 0))
        region = dilate(blobs(changed, MIN_BLOB))
        frame = base.copy()
        frame[region] = s[region]
        result.append(frame)

    # ortak kırpma: bütün karelerin opak alanını kapsayan kutu, alt kenar ayaklar
    alpha = np.any([r[..., 3] > 0 for r in result], axis=0)
    ys, xs = np.nonzero(alpha)
    y0, x0, x1 = ys.min(), xs.min(), xs.max() + 1
    result = [r[y0:, x0:x1] for r in result]
    px0, pwidth = post_columns(result[0])
    result = [extend_post(thin_post(r, px0, pwidth), POST_EXTEND) for r in result]
    print(f"direk: sol {px0}, genişlik {POST_WIDTH}, orta {px0 + POST_WIDTH / 2:.1f} (MainMenuBuilder.GilaPostCenterArt)")

    OUT.mkdir(parents=True, exist_ok=True)
    for i, r in enumerate(result):
        Image.fromarray(r, "RGBA").save(OUT / f"gila_puro_{i:02d}.png")
    print(f"{len(result)} kare -> {OUT} ({result[0].shape[1]}x{result[0].shape[0]})")

    if args.preview:
        ph, pw = result[0].shape[:2]
        sheet = Image.new("RGBA", (pw * 8, ph * 3), (128, 128, 128, 255))
        for i, r in enumerate(result):
            tile = Image.fromarray(r, "RGBA")
            sheet.alpha_composite(tile, ((i % 8) * pw, (i // 8) * ph))
        sheet = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
