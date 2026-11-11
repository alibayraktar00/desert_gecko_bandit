"""
Gila'nın silah kareleri ve silah görselleri: elle çizilmiş piksel haritalarından üretilir.

Gila'nın kendi koşusu ve kolları olduğu gibi kalır: silah her gövde karesinde öndeki yumruğun içine yerleştirilir,
gövdenin önünde çizilir ama gövdeyle çakışan kısmı kırpılır: kabza yumruğun içinde kaybolur, namlu yumruğun önünden
çıkar; yumruk koşarken ileri geri gittikçe silah da onunla gider. Kolu göğsünde sarkan karelere (duruş) koşudaki
öne uzanmış ön kol taşınır, silah dururken de elde görünür.

Çıktılar:
  Assets/Sprites/Player/Gila/silah/<silah>/<govde>_<nisan>_<0..2>.png  her gövde karesi için tutuş, ateş, toparlanma
  Assets/Sprites/Player/Gila/silah/<silah>/pozlar.json                 her karede namlu ucu (WeaponSetup okur)
  Assets/Sprites/Weapons/<silah>.png                                   yerde duran / arayüz ikonu (büyük çizim)
  Assets/Sprites/Weapons/mermi.png, namlu_<0..2>.png, mermi_ikon(_bos).png

Çalıştırma: python tools/gila_silah_ciz.py
"""
import json
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
GILA = ROOT / "Assets/Sprites/Player/Gila"
GILA_OUT = GILA / "silah"
WEAPON_OUT = ROOT / "Assets/Sprites/Weapons"

PPU = 32
PIVOT = (32, 48)
OUTLINE = (4, 0, 1, 255)

PALETTE = {
    "d": (46, 44, 56, 255),     # koyu metal
    "m": (98, 100, 116, 255),   # metal
    "l": (156, 160, 176, 255),  # açık metal
    "h": (218, 222, 232, 255),  # parlama
    "w": (108, 47, 21, 255),    # ahşap kabza
    "W": (158, 64, 30, 255),    # ahşap parlak
    "g": (232, 184, 72, 255),   # pirinç
    # Çakal'ın tüfeğinin paleti (cakal_14.png): mat haki-zeytin namlu, kahve-kırmızı ahşap
    "q": (161, 136, 92, 255),   # namlu üst parlaması
    "K": (135, 120, 83, 255),   # namlu açık
    "k": (106, 94, 69, 255),    # namlu
    "u": (42, 29, 22, 255),     # koyu metal (mekanizma, boru)
    "v": (115, 54, 43, 255),    # ahşap
    "b": (96, 68, 51, 255),     # ahşap koyu
    # Yarasa'nın dinamitinin paleti (yarasa_dinamit.png): bordo çubuk, açık fitil
    "R": (150, 52, 64, 255),    # çubuk parlak
    "r": (100, 29, 46, 255),    # çubuk
    "x": (71, 10, 54, 255),     # çubuk koyu
    "f": (150, 84, 78, 255),    # fitil
    "y": (255, 214, 90, 255),   # kıvılcım
    "o": (108, 47, 21, 255),    # demeti bağlayan ip
    # Armadillo'nun kazmasının paleti (armadillo_0.png): paslı kahve demir, koyu sap
    "P": (152, 109, 80, 255),   # kazma ağzı parlak
    "n": (135, 94, 71, 255),    # kazma demiri
    "E": (101, 74, 59, 255),    # demir koyu / sap açık
    "A": (76, 56, 47, 255),     # sap
    "z": (68, 42, 35, 255),     # sap gölgesi
}
JACKAL_OUTLINE = (42, 20, 18, 255)  # çakalın kahverengi dış çizgisi

# Altıpatlar, sağa bakar. 'H' kabzanın tutulduğu nokta (elin oturduğu yer).
REVOLVER = [
    ".d..........g",
    "dllhhhhhhhhhh",
    "dmlmmmmmmmmmd",
    "ddmld........",
    "WHdd.d.......",
    "Wwd.dd.......",
    "Ww...........",
]
REVOLVER_MUZZLE = (13, 1.5)  # namlunun hemen önü (yerel piksel)

# Elde tutulan küçük altıpatlar: yumruk boyunda, 'H' kabzanın yumruğa oturduğu nokta.
REVOLVER_HELD = [
    ".d......g",
    "dlhhhhhhh",
    "dmHmmmmmd",
    "ddwd.....",
    "Wwd......",
    "Ww.......",
]
REVOLVER_HELD_MUZZLE = (9, 1.5)

# Pompalı (Çakal'ın tüfeği): ince uzun haki namlu, altında ahşap pompa, arkada ahşap kundak.
# Kundak yumruğun arkasında, gövdenin ardında kalır.
SHOTGUN_HELD = [
    "vbuKKKKKKKKKKKKKKKKKq",
    "vvHuuuvvvvvbuuuuuu...",
    ".b.u.................",
]
# Ateşten sonra pompa geri çekilir (toparlanma karesi): "çak-çak"
SHOTGUN_HELD_PUMPED = [
    "vbuKKKKKKKKKKKKKKKKKq",
    "vvHvvvvvbuuuuuuuuu...",
    ".b.u.................",
]
SHOTGUN_HELD_MUZZLE = (21, 0.0)
SHOTGUN = [
    "vb..uuKKKKKKKKKKKKKKKKKKKq",
    "vvbbuuuuuvvvvvvvbuuuuuu...",
    ".vvbb.u.u.................",
    "..vbb..uu.................",
]

# Dinamit (Yarasa'dan): yumrukta tek çubuk, ucunda yanan fitil. Atılınca dönerek uçan çubuk, yerde 3'lü demet.
DYNAMITE_HELD = [
    "RRRRRR.y",
    "rHrrrrff",
    "xxxxxx..",
]
DYNAMITE_HELD_MUZZLE = (6, 1.0)
DYNAMITE_STICK = [
    "RRRRRR.y",
    "rrrrrrf.",
    "xxxxxx..",
]
DYNAMITE = [
    "RRRoRRRR...",
    "rrrorrrrf..",
    "RRRoRRRRff.",
    "rrrorrrr.fy",
    "RRRoRRRR...",
    "xxxoxxxx...",
]

# Kazma (Armadillo'dan): uzun sap, ucunda hilal ağız. Yalnız ileri sallanır; kareler sallamanın evreleri.
PICKAXE_HELD = [
    "..........nPP..",
    "...........nPn.",
    "............nE.",
    "............nE.",
    "nHnnnnnnnnnnnEn",
    "EEEEEEEEEEEEnEE",
    "............nE.",
    "............EA.",
    "...........EA..",
    "..........EA...",
]
PICKAXE_HELD_HEAD = (13, 4.5)  # kazma ağzının ortası: vuruş kıvılcımı buradan
# Sallama: (geri çekilme px, açı, gövdenin arkasında mı, el kayması dx, dy): 0 tutuş (omuzda), 1 kaldırma (el başın
# hizasına çıkar, kazma gövdenin ardında: ağzı şapkanın üstünden görünür), 2 vuruş (önüne indirir), 3 takip
PICKAXE_SWING = [(0.0, 50, False, 0, 0), (0.0, 100, True, -3, -9), (0.0, -38, False, 0, 0), (0.0, -15, False, 0, 0)]
PICKAXE = [
    "..............nPP..",
    "...............nPn.",
    "................nE.",
    "................nE.",
    "nnnnnnnnnnnnnnnnnEn",
    "EEEEEEEEEEEEEEEEnEE",
    "................nE.",
    "................EA.",
    "...............EA..",
    "..............EA...",
]

# Nişan yönleri: silah açısı (derece, saat yönünün tersi = yukarı)
AIMS = {"ileri": 0, "capraz": 45, "yukari": 90}

BODIES = [f"gila_idle_{i}" for i in range(4)] + [f"gila_run_{i}" for i in range(8)] +          ["gila_rise_0", "gila_apex_0", "gila_fall_0"]
# Kolu göğsünde sarkan karelerde (duruş, yükselme) silah gövdenin içinde kaybolurdu: bu karelere koşu
# karesinin (run_0) öne uzanmış ön kolu ve yumruğu kendi pikselleriyle taşınır, silah o yumrukta durur.
#   gövde karesi: (kaydırma dx, dy)
HAND_SOURCE = ("gila_run_0", (34, 31, 39, 36), (37, 34))  # kaynak kare, kol kutusu (dahil), yumruk ortası
HAND_PATCH = {
    "gila_idle_0": (2, 1), "gila_idle_1": (2, 1), "gila_idle_2": (2, 2), "gila_idle_3": (2, 2),
    "gila_rise_0": (2, 0),
}


def parse(rows):
    pixels, hand = {}, None
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == ".":
                continue
            if ch == "H":
                hand = (x, y)
                ch = "w"
            pixels[(x, y)] = PALETTE[ch]
    return pixels, hand


def rotate(pt, deg):
    a = math.radians(deg)
    x, y = pt
    # Görüntü koordinatında y aşağı: yukarı dönüş için -sin
    return (x * math.cos(a) + y * math.sin(a), -x * math.sin(a) + y * math.cos(a))


def stamp_gun(layer, gun, hand_local, hand_at, deg):
    """Ters eşleme: hedef pikselden kaynağa döner, boşluk kalmaz."""
    xs = [p[0] for p in gun]
    ys = [p[1] for p in gun]
    reach = max(max(xs), max(ys)) + 4
    for ty in range(hand_at[1] - reach, hand_at[1] + reach + 1):
        for tx in range(hand_at[0] - reach, hand_at[0] + reach + 1):
            sx, sy = rotate((tx - hand_at[0], ty - hand_at[1]), -deg)
            key = (round(sx + hand_local[0]), round(sy + hand_local[1]))
            if key in gun and 0 <= tx < 64 and 0 <= ty < 64:
                layer[(tx, ty)] = (gun[key], "gun")


def render(layer, size=(64, 64), outline=OUTLINE):
    """Dış çizgi: dolu piksele 4 komşu boş pikseller."""
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    kinds = {}
    for (x, y), (c, kind) in layer.items():
        if 0 <= x < size[0] and 0 <= y < size[1]:
            img.putpixel((x, y), c)
            kinds[(x, y)] = kind
    out = img.copy()
    for y in range(size[1]):
        for x in range(size[0]):
            if img.getpixel((x, y))[3]:
                continue
            near = [kinds[n] for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)) if n in kinds]
            if not near:
                continue
            out.putpixel((x, y), outline)
    return out


def to_units(p):
    return ((p[0] - PIVOT[0]) / PPU, (PIVOT[1] - p[1]) / PPU)


def body_frame(name):
    folder = name.split("_")[1]
    return Image.open(GILA / folder / f"{name}.png").convert("RGBA")


def hand_patch(name):
    """Bu kareye taşınan ön kol pikselleri ({konum: renk}) ve yumruğun ortası; taşınmıyorsa (None, None)."""
    if name not in HAND_PATCH:
        return None, None
    dx, dy = HAND_PATCH[name]
    src_name, (x0, y0, x1, y1), (fx, fy) = HAND_SOURCE
    src = body_frame(src_name)
    pixels = {}
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            c = src.getpixel((x, y))
            if c[3]:
                pixels[(x + dx, y + dy)] = c
    return pixels, (fx + dx, fy + dy)


def find_fist(name, body):
    """Öndeki yumruk: göğüs hizasında (32-38. satırlar) gövdenin en öne çıkan pikselleri."""
    right = {}
    for y in range(32, 39):
        xs = [x for x in range(64) if body.getpixel((x, y))[3]]
        if xs:
            right[y] = max(xs)
    tip = max(right.values())
    rows = [y for y, x in right.items() if x >= tip - 1]
    return (tip - 1, round(sum(rows) / len(rows)))


def remove_old():
    for old in list(GILA_OUT.glob("gila_*.png")) + list((GILA / "silahli").rglob("*.png")):
        old.unlink()
        meta = old.parent / (old.name + ".meta")
        if meta.exists():
            meta.unlink()
    for folder in (GILA / "silahli" / "kuyruk", GILA / "silahli"):
        if folder.exists():
            folder.rmdir()
            meta = folder.parent / (folder.name + ".meta")
            if meta.exists():
                meta.unlink()


def held_frames(name, gun_rows, muzzle_local, recover_rows=None, outline=OUTLINE, poses=None, aims=None):
    """
    Her gövde karesi için yumruktaki silah: 3 nişan x 3 kare (tutuş, ateş, toparlanma). Namlu uçları JSON'a.
    recover_rows verilirse toparlanma karesi o çizimle yapılır (pompanın geri çekilmesi gibi).
    poses verilirse kareler bu (geri çekilme, açı) listesinden çizilir (kazmanın sallanma evreleri);
    aims verilirse yalnız o nişan yönleri çizilir.
    """
    gun, hand_local = parse(gun_rows)
    recover = parse(recover_rows)[0] if recover_rows else gun
    out = GILA_OUT / name
    out.mkdir(parents=True, exist_ok=True)
    for old in out.glob("*.png"):
        old.unlink()

    kicks = poses or [(0.0, 0), (1.2, 16), (0.5, 7)]  # geri çekilme (px), yukarı tepme (derece)
    aim_items = [(k, v) for k, v in AIMS.items() if aims is None or k in aims]
    poses = []
    kicks = [tuple(k) + (False, 0, 0)[len(k) - 2:] for k in kicks]
    for body_name in BODIES:
        body = body_frame(body_name)
        patch, fist = hand_patch(body_name)
        if fist is None:
            fist = find_fist(body_name, body)
        entry = {"body": body_name, "yumruk": list(fist)}
        for aim, deg in aim_items:
            dir_x, dir_y = math.cos(math.radians(deg)), -math.sin(math.radians(deg))
            for i, (back, kick, behind, hx, hy) in enumerate(kicks):
                h = (round(fist[0] - dir_x * back) + hx, round(fist[1] - dir_y * back) + hy)
                a = deg + kick
                layer = {}
                stamp_gun(layer, recover if i == 2 and recover_rows else gun, hand_local, h, a)
                img = render(layer, outline=outline)
                # Silah gövdenin önünde çizilir; yumruğun gerisinde kalıp gövdeyle (ya da taşınan yumrukla)
                # çakışan kısmı kırpılır: kabza ve kundak yumruğun/gövdenin ardında kalır, yumruğun önündeki
                # namlu her zaman görünür (yukarı nişanda yüzün önünden geçse de).
                ax, ay = math.cos(math.radians(a)), -math.sin(math.radians(a))
                for y in range(64):
                    for x in range(64):
                        ahead = (x - h[0]) * ax + (y - h[1]) * ay
                        covered = body.getpixel((x, y))[3] or (patch and (x, y) in patch)
                        if covered and (behind or ahead <= 1.5):
                            img.putpixel((x, y), (0, 0, 0, 0))
                for p, c in (patch or {}).items():
                    img.putpixel(p, c)
                img.save(out / f"{body_name}_{aim}_{i}.png")
                if i == 0:
                    mx, my = rotate((muzzle_local[0] - hand_local[0], muzzle_local[1] - hand_local[1]), a)
                    ux, uy = to_units((h[0] + mx, h[1] + my))
                    entry[aim] = [round(ux, 3), round(uy, 3)]
        poses.append(entry)
    (out / "pozlar.json").write_text(json.dumps({"poses": poses}, indent=1), encoding="utf-8")
    print(f"{name}: {len(poses)} gövde karesi")


def item_sprite(name, gun_rows, outline=OUTLINE):
    gun, _ = parse(gun_rows)
    w = max(p[0] for p in gun) + 3
    h = max(p[1] for p in gun) + 3
    layer = {(x + 1, y + 1): (c, "gun") for (x, y), c in gun.items()}
    WEAPON_OUT.mkdir(parents=True, exist_ok=True)
    render(layer, (w, h), outline).save(WEAPON_OUT / f"{name}.png")


def small_sprites():
    # Mermi: sağa uçar; parlak uç, turuncu iz
    bullet = {(0, 1): (232, 120, 40, 200), (1, 1): (240, 160, 60, 255), (2, 1): (255, 220, 120, 255),
              (3, 1): (255, 250, 220, 255), (2, 0): (255, 220, 120, 255), (2, 2): (255, 220, 120, 255),
              (3, 0): (255, 240, 180, 255), (3, 2): (255, 240, 180, 255), (4, 1): (255, 255, 255, 255)}
    img = Image.new("RGBA", (6, 3), (0, 0, 0, 0))
    for p, c in bullet.items():
        img.putpixel(p, c)
    img.save(WEAPON_OUT / "mermi.png")

    # Namlu alevi: 12x12, merkez = namlu ucu, sağa doğru açılır
    flashes = [
        ["............",
         "............",
         "............",
         "....y.......",
         ".....y..y...",
         "...yWWyy....",
         "...yWWWWyy..",
         ".....y..y...",
         "....y.......",
         "............",
         "............",
         "............"],
        ["............",
         "............",
         "......o..o..",
         ".....oy.y...",
         "...oyyWy..o.",
         "...yWWWWWyyo",
         "...yWWWWyy..",
         "...oyyWy..o.",
         ".....oy.y...",
         "......o..o..",
         "............",
         "............"],
        ["............",
         "............",
         "............",
         "......o.....",
         ".....o..o...",
         "....oyyo....",
         "....oyyyo...",
         ".....o..o...",
         "......o.....",
         "............",
         "............",
         "............"],
    ]
    colors = {"W": (255, 252, 230, 255), "y": (255, 214, 90, 255), "o": (232, 120, 40, 220)}
    for i, rows in enumerate(flashes):
        img = Image.new("RGBA", (12, 12), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in colors:
                    img.putpixel((x, y), colors[ch])
        img.save(WEAPON_OUT / f"namlu_{i}.png")

    # Arayüz mermisi: dik pirinç kovan, dolu ve boş
    full = [".o.", "ogo", "oho", "ogo", "ogo", "ooo"]
    empty = [".o.", "o.o", "o.o", "o.o", "o.o", "ooo"]
    pal = {"o": OUTLINE, "g": (232, 184, 72, 255), "h": (255, 236, 160, 255)}
    for name, rows in (("mermi_ikon", full), ("mermi_ikon_bos", empty)):
        img = Image.new("RGBA", (3, 6), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in pal:
                    c = pal[ch] if name == "mermi_ikon" else (70, 60, 60, 200)
                    img.putpixel((x, y), c)
        img.save(WEAPON_OUT / f"{name}.png")


def dynamite_sprites():
    item_sprite("dinamit_cubuk", DYNAMITE_STICK)
    # Arayüz: dik dinamit çubuğu, fitili yukarıda; boşu soluk
    rows = ["..y", ".f.", "RRR", "rrr", "rrr", "xxx"]
    for name, empty in (("dinamit_ikon", False), ("dinamit_ikon_bos", True)):
        img = Image.new("RGBA", (3, 6), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch == ".":
                    continue
                img.putpixel((x, y), (70, 60, 60, 200) if empty else PALETTE[ch])
        img.save(WEAPON_OUT / f"{name}.png")


def shotgun_sprites():
    # Saçma tanesi: 2x2 parlak kurşun
    img = Image.new("RGBA", (2, 2), (0, 0, 0, 0))
    for p, c in {(0, 0): (255, 246, 200, 255), (1, 0): (255, 226, 130, 255),
                 (0, 1): (255, 226, 130, 255), (1, 1): (220, 170, 80, 255)}.items():
        img.putpixel(p, c)
    img.save(WEAPON_OUT / "sacma.png")

    # Arayüz fişeği: kırmızı kovan, pirinç dip; boşu soluk
    full = [".o.", "oRo", "oRo", "oro", "ogo", "ooo"]
    pal = {"o": OUTLINE, "R": (226, 78, 56, 255), "r": (170, 40, 32, 255), "g": (232, 184, 72, 255)}
    for name, empty in (("fisek_ikon", False), ("fisek_ikon_bos", True)):
        img = Image.new("RGBA", (3, 6), (0, 0, 0, 0))
        for y, row in enumerate(full):
            for x, ch in enumerate(row):
                if ch == "." or (empty and ch != "o"):
                    continue
                img.putpixel((x, y), (70, 60, 60, 200) if empty else pal[ch])
        img.save(WEAPON_OUT / f"{name}.png")


def main():
    remove_old()
    held_frames("altipatlar", REVOLVER_HELD, REVOLVER_HELD_MUZZLE)
    held_frames("pompali", SHOTGUN_HELD, SHOTGUN_HELD_MUZZLE, SHOTGUN_HELD_PUMPED, JACKAL_OUTLINE)
    item_sprite("pompali", SHOTGUN, JACKAL_OUTLINE)
    held_frames("dinamit", DYNAMITE_HELD, DYNAMITE_HELD_MUZZLE)
    item_sprite("dinamit", DYNAMITE)
    dynamite_sprites()
    held_frames("kazma", PICKAXE_HELD, PICKAXE_HELD_HEAD, outline=JACKAL_OUTLINE, poses=PICKAXE_SWING, aims=["ileri"])
    item_sprite("kazma", PICKAXE, JACKAL_OUTLINE)
    tick = ["nn", "PE", "nE", "nE", "EA", "AA"]  # arayüz: küçük demir parçası (dayanıklılık)
    for nm, empty in (("kazma_ikon", False), ("kazma_ikon_bos", True)):
        img = Image.new("RGBA", (2, 6), (0, 0, 0, 0))
        for y, row in enumerate(tick):
            for x, ch in enumerate(row):
                img.putpixel((x, y), (70, 60, 60, 200) if empty else PALETTE[ch])
        img.save(WEAPON_OUT / f"{nm}.png")
    shotgun_sprites()
    item_sprite("altipatlar", REVOLVER)
    small_sprites()


if __name__ == "__main__":
    main()
