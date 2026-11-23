"""
Mavi Dev'in yere çarpınca çıkardığı şok dalgası: pixelfx'in impacts/ground-slam efekti (iki yarım ay şok cephesi
zeminde sağa sola koşar, kıvılcım, taş, toz sütunu). Taş rengi çöl kumu tonuna boyanır.

Önce efekt sprite sheet olarak dışa aktarılır (pixelfx checkout'unda):
  cd ~/.claude/pixelfx
  node tools/render.mjs impacts/ground-slam --sheet --opts '{"stone":"#7a5a3e"}'
Sonra (proje kökünden):
  python tools/dev_sok_isle.py
Kareler Assets/Sprites/Enemies/dev/dev_sok_<i>.png olur (alt orta pivot = zemin, efektin çıkış noktası).
Zemin çizgisinin altındaki çatlaklar kesilir: karolar zaten zemini çiziyor. 30 fps, ilk 2.4 saniye
(sonrası neredeyse boş, sönen toz). Şok cephesinin oyundaki menzili (BlueGiant.shockReach) efektin reach'i
(104 px = 3.25 birim) ile aynı.
"""
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SHEET = Path.home() / ".claude" / "pixelfx" / "records" / "impacts" / "ground-slam" / "ground-slam"
OUT_DIR = ROOT / "Assets" / "Sprites" / "Enemies" / "dev"
TOP = 20          # toz sütununun tepesi
FRAME_COUNT = 72  # 2.4 s @ 30 fps


def main():
    meta = json.loads(SHEET.with_suffix(".json").read_text(encoding="utf-8"))
    info = meta["meta"]["pixelfx"]
    w, h = info["frameSize"]
    px, py = info["pivot"]
    assert px * 2 == w, "pivot yatayda ortada olmalı"
    sheet = Image.open(SHEET.with_suffix(".png")).convert("RGBA")
    frames = meta["frames"]
    frames = (frames if isinstance(frames, list) else list(frames.values()))[:FRAME_COUNT]
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for i, f in enumerate(frames):
        r = f["frame"]
        frame = sheet.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"]))
        frame.crop((0, TOP, w, py)).save(OUT_DIR / f"dev_sok_{i}.png")
    print(f"{len(frames)} kare, {w}x{py - TOP}, {info['fps']} fps")


if __name__ == "__main__":
    main()
