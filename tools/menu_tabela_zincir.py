"""
Başlık tabelasının zincirlerini baştan çizer ve yukarı uzatır: tabela ekranın tepesinden gerçek bir zincirle
sarkıyormuş gibi görünsün.

Girdi : Assets/Sprites/UI/MainMenu/menu_tabela.png  (gemini_menu_isle.py çıktısı; üstte iki kısa zincir)
Çıktı : Assets/Sprites/UI/MainMenu/menu_tabela_asili.png

Gemini'nin zinciri yukarı açık bir U ile bittiği için yarım görünüyordu. Onun yerine klasik zincir çizilir:
önden görünen oval halka (ortası delik) ile yandan görünen ince halka sırayla birbirine geçer; ince halka ovalin
deliğinden görünür. En alttaki ince halka tabelanın metal kulakçığına girer (kulakçık, satır 12'den aşağısı, olduğu
gibi kalır ve zincirin önünde durur). Zincir EXTEND piksel yukarı uzar; MainMenuBuilder tabelayı o kadar yukarı asar
(ChainExtendPx), zincirin tepesi ekran dışında kalır.

Kullanım (proje kökünden):
  python tools/menu_tabela_zincir.py [--preview onizleme.png]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu"

EXTEND = 36             # zincir bu kadar yukarı uzar (MainMenuBuilder.ChainExtendPx ile aynı)
CHAIN_X = (48, 170)     # iki zincirin orta sütunu (menu_tabela.png'deki zincirler)
BRACKET_ROW = 12        # bu satırdan aşağısı tabelanın kulakçığı ve tahtası (korunur)
ENTER_ROW = 15          # en alttaki halka kulakçığın deliğine kadar iner

OUTLINE = (39, 16, 21)
DARK = (86, 51, 35)
MID = (119, 80, 50)
LIGHT = (160, 109, 56)
SHINE = (196, 146, 84)

OVAL_W, OVAL_H = 8, 12
SIDE_W, SIDE_H = 4, 12
STEP = 8                # halkalar arası (üst üste binme: 4 satır)


def ellipse(w, h, rx, ry):
    ys, xs = np.mgrid[0:h, 0:w]
    return ((xs + 0.5 - w / 2) / rx) ** 2 + ((ys + 0.5 - h / 2) / ry) ** 2 <= 1.0


def oval_link():
    """Önden görünen halka: dışı 8x12 elips, ortası 2x6 delik. Sol üst ışık alır, sağ alt koyu."""
    ring = ellipse(OVAL_W, OVAL_H, OVAL_W / 2, OVAL_H / 2) & ~ellipse(OVAL_W, OVAL_H, 1.25, 3.4)
    out = np.zeros((OVAL_H, OVAL_W, 4), dtype=np.uint8)
    pad = np.pad(ring, 1)
    edge = ring & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    for y in range(OVAL_H):
        for x in range(OVAL_W):
            if not ring[y, x]:
                continue
            if edge[y, x]:
                c = OUTLINE
            elif x < OVAL_W / 2:
                c = SHINE if y < OVAL_H / 2 - 1 else LIGHT
            else:
                c = MID if y < OVAL_H / 2 else DARK
            out[y, x] = (*c, 255)
    return out


def side_link():
    """Yandan görünen ince halka: 4 sütun (hat, ışık, gölge, hat), uçları yuvarlak."""
    out = np.zeros((SIDE_H, SIDE_W, 4), dtype=np.uint8)
    for y in range(SIDE_H):
        if y in (0, SIDE_H - 1):
            out[y, 1:3] = (*OUTLINE, 255)
        else:
            out[y] = [(*OUTLINE, 255), (*LIGHT, 255), (*DARK, 255), (*OUTLINE, 255)]
    return out


def paste(canvas, sprite, x0, y0):
    h, w = sprite.shape[:2]
    for y in range(h):
        for x in range(w):
            cy, cx = y0 + y, x0 + x
            if sprite[y, x, 3] and 0 <= cy < canvas.shape[0] and 0 <= cx < canvas.shape[1]:
                canvas[cy, cx] = sprite[y, x]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    sign = np.asarray(Image.open(ART / "menu_tabela.png").convert("RGBA"))
    h, w = sign.shape[:2]
    out = np.zeros((h + EXTEND, w, 4), dtype=np.uint8)
    body = sign.copy()
    body[:BRACKET_ROW] = 0                      # Gemini'nin yarım zinciri silinir
    oval, side = oval_link(), side_link()
    bottom = EXTEND + ENTER_ROW                 # en alttaki ince halkanın alt ucu
    for cx in CHAIN_X:
        # aşağıdan yukarı: ince, oval, ince, oval... önce ince halkalar (ovallerin deliğinden görünsünler)
        links, y, kind = [], bottom - SIDE_H, "side"
        while y + OVAL_H > 0:
            links.append((kind, y))
            y -= STEP
            kind = "oval" if kind == "side" else "side"
        for kind, y in links:
            if kind == "side":
                paste(out, side, cx - SIDE_W // 2, y)
        for kind, y in links:
            if kind == "oval":
                paste(out, oval, cx - OVAL_W // 2, y)
    # tabela ve kulakçık zincirin önünde
    m = body[..., 3] > 0
    out[EXTEND:][m] = body[m]
    Image.fromarray(out, "RGBA").save(ART / "menu_tabela_asili.png")
    print(f"menu_tabela_asili.png {out.shape[1]}x{out.shape[0]} (+{EXTEND} satır zincir)")

    if args.preview:
        im = Image.new("RGBA", (w, out.shape[0]), (90, 120, 160, 255))
        im.alpha_composite(Image.fromarray(out, "RGBA"))
        im.crop((0, 0, w, EXTEND + 30)).resize((w * 4, (EXTEND + 30) * 4), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
