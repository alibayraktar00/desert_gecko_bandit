"""
Kasaba sayfalarını (bina ve eşya paketleri) oyun görsellerine çevirir: Level 4 ve 5'in kasabaları.

Her nesne düz gri zemin üstünde, altında ince bir toprak çizgisiyle çizili. Kutunun içinde gri zemin atılır,
alttaki toprak çizgisi kesilir (nesne oyunun zeminine oturur), görsel 2'ye küçültülür ve boşlukları kırpılır.
Çıktılar Props klasörüne kasaba_ önekiyle gider (alt orta pivot).

Kaynaklar: Assets/Sprites/_Gelen/kasaba_bina_1..4.webp, kasaba_esya_1.webp, kasaba_part2.webp, kasaba_part3.webp
Kullanım (proje kökünden):  python tools/kasaba_paketi_isle.py [--preview onizleme.png]
Gereken paketler: pillow, numpy
"""
import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "Assets" / "Sprites" / "_Gelen"
PROPS = ROOT / "Assets" / "Sprites" / "Environment" / "Props"
PIXEL = 2

# ad: (kaynak, kutu x0 y0 x1 y1). Kutunun alt kenarı nesnenin altındaki toprak çizgisinin biraz altında.
PIECES = {
    # Binalar
    "kasaba_saloon": ("kasaba_bina_1", (55, 75, 478, 378)),
    "kasaba_serif": ("kasaba_bina_1", (535, 140, 978, 378)),
    "kasaba_dukkan": ("kasaba_bina_1", (55, 450, 462, 718)),
    "kasaba_ev": ("kasaba_bina_1", (545, 440, 978, 718)),
    "kasaba_banka": ("kasaba_bina_2", (55, 100, 482, 378)),
    "kasaba_demirci": ("kasaba_bina_2", (538, 140, 978, 378)),
    "kasaba_silahci": ("kasaba_bina_2", (55, 450, 468, 718)),
    "kasaba_su_kulesi": ("kasaba_bina_2", (585, 440, 978, 718)),
    "kasaba_cenazeci": ("kasaba_bina_3", (55, 100, 482, 378)),
    "kasaba_istasyon": ("kasaba_bina_3", (545, 85, 978, 378)),
    "kasaba_otel": ("kasaba_bina_3", (55, 425, 472, 718)),
    "kasaba_kilise": ("kasaba_bina_3", (560, 375, 978, 718)),
    "kasaba_hapishane": ("kasaba_bina_4", (55, 100, 492, 378)),
    "kasaba_maden_ofisi": ("kasaba_bina_4", (545, 140, 978, 378)),
    "kasaba_berber": ("kasaba_bina_4", (55, 450, 468, 718)),
    "kasaba_ahir": ("kasaba_bina_4", (508, 455, 978, 718)),
    "kasaba_demirci_2": ("kasaba_part2", (38, 155, 302, 382)),
    "kasaba_banka_2": ("kasaba_part2", (302, 170, 512, 382)),
    "kasaba_ambar": ("kasaba_part2", (515, 160, 778, 382)),
    "kasaba_degirmen": ("kasaba_part2", (800, 135, 992, 382)),
    # Eşyalar
    "kasaba_kuyu": ("kasaba_esya_1", (140, 70, 382, 252)),
    "kasaba_yalak": ("kasaba_esya_1", (560, 135, 958, 232)),
    "kasaba_tuvalet": ("kasaba_esya_1", (120, 315, 412, 503)),
    "kasaba_daragaci": ("kasaba_esya_1", (628, 285, 922, 503)),
    "kasaba_sandiklar": ("kasaba_esya_1", (52, 555, 462, 716)),
    "kasaba_ilan_tahtasi": ("kasaba_esya_1", (638, 550, 978, 716)),
    "kasaba_araba": ("kasaba_part2", (38, 415, 318, 566)),
    "kasaba_pazar": ("kasaba_part2", (328, 425, 505, 566)),
    "kasaba_odun": ("kasaba_part2", (518, 465, 722, 566)),
    "kasaba_kumes": ("kasaba_part2", (745, 435, 978, 566)),
    "kasaba_fici_teker": ("kasaba_part2", (52, 605, 212, 719)),
    "kasaba_ficilar": ("kasaba_part2", (228, 610, 308, 719)),
    "kasaba_tabela_sandik": ("kasaba_part2", (498, 595, 692, 719)),
    "kasaba_yalak_2": ("kasaba_part2", (718, 615, 978, 719)),
    "kasaba_kuyu_tas": ("kasaba_part3", (68, 155, 252, 381)),
    "kasaba_firin": ("kasaba_part3", (268, 185, 442, 381)),
    "kasaba_posta_diregi": ("kasaba_part3", (478, 155, 592, 381)),
    "kasaba_el_arabasi": ("kasaba_part3", (618, 235, 792, 381)),
    "kasaba_kopek_kulubesi": ("kasaba_part3", (798, 225, 978, 381)),
    "kasaba_saman": ("kasaba_part3", (58, 425, 282, 566)),
    "kasaba_camasir": ("kasaba_part3", (288, 415, 522, 566)),
    "kasaba_bostan": ("kasaba_part3", (533, 445, 768, 566)),
    "kasaba_kutuk": ("kasaba_part3", (772, 455, 978, 566)),
    "kasaba_cit": ("kasaba_part3", (52, 610, 418, 719)),
    "kasaba_bank_sandalye": ("kasaba_part3", (428, 610, 722, 719)),
    "kasaba_erzak": ("kasaba_part3", (738, 615, 978, 719)),
}


# Kutunun içine düşen etiket yazıları (kaynak pikseli): zemin rengine boyanır.
ERASE = {
    "kasaba_kilise": [(690, 405, 800, 440)],
}


def ground_line(m):
    """
    Nesnenin oturduğu toprak çizgisi: alttan yukarı bakınca genişliğin yarısını kaplayan ilk satırlar. Çizgi en fazla
    birkaç satır kalındır; üstündeki geniş veranda tabanı çizgi sanılmasın diye kalınlık sınırlanır.
    """
    frac = m.mean(1)
    y = m.shape[0] - 1
    while y > 0 and frac[y] <= 0.45:
        y -= 1
    if y <= m.shape[0] * 0.6:
        return m.shape[0]
    bottom = y
    while y > 0 and frac[y - 1] > 0.45 and bottom - y < 3:
        y -= 1
    return y


def components(m):
    h, w = m.shape
    seen = np.zeros_like(m)
    out = []
    for sy, sx in zip(*np.nonzero(m)):
        if seen[sy, sx]:
            continue
        comp, q = [], deque([(sy, sx)])
        seen[sy, sx] = True
        while q:
            y, x = q.popleft()
            comp.append((y, x))
            for ny in (y - 1, y, y + 1):
                for nx in (x - 1, x, x + 1):
                    if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
        out.append(comp)
    return out


def cut(sheet, box, erase=()):
    x0, y0, x1, y1 = box
    region = sheet[y0:y1, x0:x1].copy()
    for ex0, ey0, ex1, ey1 in erase:
        region[max(0, ey0 - y0):max(0, ey1 - y0), max(0, ex0 - x0):max(0, ex1 - x0)] = region[2, 2]
    bg = np.median(np.concatenate([region[:3].reshape(-1, 3), region[:, :3].reshape(-1, 3), region[:, -3:].reshape(-1, 3)]), axis=0)
    m = np.abs(region - bg).sum(2) > 24
    lo, hi = region.min(2), region.max(2)
    m &= ~((lo > 150) & (hi - lo < 28))  # açık gri etiket yazıları
    line = ground_line(m)
    m[line:] = False
    h, w = line // PIXEL, region.shape[1] // PIXEL
    blocks = region[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL, 3).transpose(0, 2, 1, 3, 4).reshape(h, w, -1, 3)
    mblocks = m[:h * PIXEL, :w * PIXEL].reshape(h, PIXEL, w, PIXEL).transpose(0, 2, 1, 3).reshape(h, w, -1)
    small = mblocks.mean(2) >= 0.5
    keep = np.zeros_like(small)
    rgb = np.zeros((h, w, 3))
    for y, x in zip(*np.nonzero(small)):
        rgb[y, x] = np.median(blocks[y, x][mblocks[y, x]], axis=0)
    for comp in components(small):
        if len(comp) < 12:  # toz kırıntıları
            continue
        colors = np.array([rgb[y, x] for y, x in comp])
        lum, sat = colors.mean(), (colors.max(1) - colors.min(1)).mean()
        if len(comp) < 400 and lum > 105 and sat < 22:  # açık gri renksiz parça: etiket yazısı
            continue
        ys = [y for y, _ in comp]
        if max(ys) - min(ys) <= 7 and max(ys) < h * 0.22:  # tepede alçak ayrık şerit: nesnenin adı (building_04 gibi)
            continue
        for y, x in comp:
            keep[y, x] = True
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[keep, :3] = rgb[keep]
    rgba[keep, 3] = 255
    ys, xs = np.nonzero(rgba[..., 3])
    rgba = rgba[ys.min():, xs.min():xs.max() + 1]  # alt kenar korunur: nesne zemine oturur
    return rgba


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview")
    args = parser.parse_args()
    sheets = {}
    made = []
    for name, (src, box) in PIECES.items():
        if src not in sheets:
            sheets[src] = np.array(Image.open(SOURCE_DIR / f"{src}.webp").convert("RGB")).astype(float)
        rgba = cut(sheets[src], box, ERASE.get(name, ()))
        out = PROPS / f"{name}.png"
        Image.fromarray(rgba).save(out)
        made.append(out)
        print(f"{name}: {rgba.shape[1]}x{rgba.shape[0]}")

    if args.preview:
        tiles = [Image.open(p).convert("RGBA") for p in made]
        cols, pad = 8, 8
        cw = max(t.width for t in tiles) + pad
        ch = max(t.height for t in tiles) + pad
        rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new("RGBA", (cols * cw, rows * ch), (150, 120, 90, 255))
        for i, t in enumerate(tiles):
            sheet.alpha_composite(t, ((i % cols) * cw, (i // cols) * ch + ch - pad - t.height))
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
