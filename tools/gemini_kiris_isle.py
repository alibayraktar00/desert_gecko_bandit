"""
Başlık tabelasının asıldığı ahşap kolu (kiriş + payanda + iki askı halkası) Gemini çıktısından sprite'a çevirir.

Girdi : Assets/Sprites/_Gelen/kiris_ham.png  (yatay kiriş, sol ucunda aşağı-sola inen payanda, altında iki halka)
Çıktı : Assets/Sprites/UI/MainMenu/Tabela/tabela_kiris.png

Kiriş sol ucundan yön tabelasının direğine çakılır (MainMenuBuilder). Halkalar başlık tabelasının zincirlerinin
tam üstüne gelsin diye Gemini'nin halkaları kesilip HOOKS_X konumlarına yeniden basılır: zincir aralığı
menu_tabela.png'de 128 ekran pikseli = 64 sanat pikseli; tabela direğe binmesin diye ilk halka direkten uzakta.

Kullanım (proje kökünden):
  python tools/gemini_kiris_isle.py [--preview onizleme.png]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gemini_gila_puro_isle as g  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets" / "Sprites" / "_Gelen" / "kiris_ham.png"
OUT = ROOT / "Assets" / "Sprites" / "UI" / "MainMenu" / "Tabela" / "tabela_kiris.png"

WIDTH = 122               # kirişin boyu (sanat pikseli; oyunda 2x)
HOOKS_X = (42, 106)       # halkaların ortası, kirişin sol ucundan (MainMenuBuilder.BeamHooksArt ile aynı)
PALETTE = 20


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()

    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.int32)
    fg = ~g.background(rgb)
    ys, xs = np.nonzero(fg)
    rgb, fg = rgb[ys.min():ys.max() + 1, xs.min():xs.max() + 1], fg[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = fg.shape
    out_h = max(1, int(round(h * WIDTH / w)))
    sprite = g.snap(g.downscale(rgb, fg, WIDTH, out_h), g.palette_of([g.downscale(rgb, fg, WIDTH, out_h)], PALETTE))

    # kirişin alt kenarı: sağ yarıda (payanda yok) opak sütunların en alt ortak satırı
    alpha = sprite[..., 3] > 0
    right = alpha[:, WIDTH // 2:]
    rows = right.mean(axis=1)
    beam_rows = np.nonzero(rows > 0.6)[0]
    beam_bottom = beam_rows.max()
    # halkalar: kirişin altında, payandanın sağında kalan küçük parçalar
    below = alpha.copy()
    below[:beam_bottom + 1] = False
    below[:, :int(WIDTH * 0.2)] = False
    labels, n = ndimage.label(below, structure=np.ones((3, 3)))
    hooks = []
    for k in range(1, n + 1):
        yy, xx = np.nonzero(labels == k)
        if len(yy) >= 2:
            hooks.append((xx.mean(), sprite[yy.min():yy.max() + 1, xx.min():xx.max() + 1].copy(), labels == k))
    hooks.sort(key=lambda t: t[0])
    if not hooks:
        raise SystemExit("halka bulunamadı")
    template = max(hooks, key=lambda t: t[1].shape[0] * t[1].shape[1])[1]
    for _, _, m in hooks:
        sprite[m] = 0
    th, tw = template.shape[:2]
    pad = th + 1
    sprite = np.concatenate([sprite, np.zeros((pad, WIDTH, 4), dtype=np.uint8)])
    for hx in HOOKS_X:
        x0 = int(round(hx - tw / 2))
        region = sprite[beam_bottom + 1:beam_bottom + 1 + th, x0:x0 + tw]
        m = template[..., 3] > 0
        region[m] = template[m]
    ys = np.nonzero((sprite[..., 3] > 0).any(axis=1))[0]
    sprite = sprite[:ys.max() + 1]

    Image.fromarray(sprite, "RGBA").save(OUT)
    hook_bottom = beam_bottom + th
    print(f"kiriş {sprite.shape[1]}x{sprite.shape[0]}, kiriş altı y={beam_bottom}, halka altı y={hook_bottom} "
          f"(MainMenuBuilder.BeamHookBottomArt), halkalar x={HOOKS_X}")

    if args.preview:
        im = Image.new("RGBA", (sprite.shape[1] + 8, sprite.shape[0] + 8), (90, 120, 160, 255))
        im.alpha_composite(Image.fromarray(sprite, "RGBA"), (4, 4))
        im.resize((im.width * 5, im.height * 5), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
