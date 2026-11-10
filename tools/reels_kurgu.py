"""
Instagram Reels tanıtım videosunu kurgular: Temp/Reels altındaki ham kareleri (AutoplayTests.Reels_Kayit çıktısı)
sıraya dizer, üstlerine yazı koyar, oyunun müziğini ekler ve 1080x1920 mp4 üretir.

Ham kareler 540x960; piksel sanatı bulanmasın diye tam 2 kat (en yakın komşu) büyütülür.
Yazılar da küçük çizilip büyütülür, oyunun piksel görünümüne uyar.

Gerekenler: Pillow, imageio-ffmpeg (ffmpeg'i kendisi getirir).
Video siyah ekranda daktilo gibi yazılan iki satırla açılır, ardından oyunun adı ekrana vurur ve altında Gila koşar.

Ham kareler yoksa (Unity yeniden açılınca Temp temizlenir) sahneler ilk sürümün videosundan
(Tanitim/desert_gecko_reels_v1.mp4, baştaki fırtına sahnesi atlanarak) alınır.

Kullanım (proje kökünden): python tools/reels_kurgu.py [--out Tanitim/desert_gecko_reels.mp4]
"""
import argparse
import subprocess
from pathlib import Path

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageEnhance, ImageFont

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "Temp" / "Reels"
MUSIC = ROOT / "Assets" / "Audio" / "Shadows_on_the_Mesa.mp3"
FPS = 30
SIZE = (1080, 1920)
TEXT_SCALE = 6  # yazı bu kadar küçük çizilip büyütülür
FONT = "C:/Windows/Fonts/arialbd.ttf"

# (klasör, ilk kare, kare sayısı, yazı)
SCENES = [
    ("L3_kanca", 15, 65, "DİLİNLE SALLAN"),
    ("L1_son", 60, 60, "TIRMAN, SÜZÜL"),
    ("L3_gayzer", 20, 60, "GAYZERLE FIRLA"),
    ("L2_asansor", 125, 30, ""),  # Gila asansörü beklerken duruyor: kısa tutulur
    ("L2_arena", 45, 50, "DÖVÜŞ"),
    ("L3_baca", 100, 85, "LAV YÜKSELİYOR"),
    ("L2_ruzgar", 10, 65, "RÜZGÂRI YAKALA"),
    ("L1_akbaba", 20, 60, ""),
    ("L3_safak", 55, 75, "ŞAFAĞA ULAŞ"),
]
END_CARD = ("L1_kamp", 60, 96)  # arka plan karesi, süre
TITLE = ["DESERT GECKO", "BANDIT"]
TAGLINE = "Soğukkanlı bir haydudun hikâyesi"
CAPTION_Y = 1290  # Reels'in alt düğmelerinin üstünde
# Şarkının orta kısmı: 48.07. saniyede sakin bölüm biter ve asıl tema girer. Başlık tam o vuruşta ekrana gelsin diye
# müzik, başlığın videodaki anı (TITLE_FRAME) kadar önceden başlatılır; oynanış sonraki güçlü vuruşta (51.06) açılır.
MUSIC_DROP = 48.07
# Ölçülen güçlü vuruşlar: 48.08 (başlık), 48.65 (slogan belirir), 50.36 (oynanış açılır).
TITLE_FRAME, INTRO_FRAMES = 80, 148
TAGLINE_AFTER = 17  # başlıktan 0,57 sn sonra: 48.65 vuruşu; slogan ~1,7 sn ekranda kalır
MUSIC_START = MUSIC_DROP - TITLE_FRAME / FPS


def pixel_text(lines, size, fill, outline=(31, 15, 11), scale=TEXT_SCALE):
    """Küçük çizilip en yakın komşuyla büyütülen, konturlu yazı. Saydam zeminli görsel döner."""
    font = ImageFont.truetype(FONT, size)
    probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    boxes = [probe.textbbox((0, 0), line, font=font) for line in lines]
    width = max(b[2] - b[0] for b in boxes) + 6
    line_height = max(b[3] - b[1] for b in boxes) + 4
    image = Image.new("RGBA", (width, line_height * len(lines) + 4), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    for i, (line, box) in enumerate(zip(lines, boxes)):
        x = (width - (box[2] - box[0])) // 2 - box[0]
        y = 2 + i * line_height - box[1]
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1, 2):
                draw.text((x + dx, y + dy), line, font=font, fill=outline)
        draw.text((x, y), line, font=font, fill=fill)
    # Yumuşak kenarları at: piksel görünümü
    alpha = image.getchannel("A").point(lambda a: 255 if a > 110 else 0)
    image.putalpha(alpha)
    return image.resize((image.width * scale, image.height * scale), Image.NEAREST)


GILA_RUN = ROOT / "Assets" / "Sprites" / "Player" / "Gila" / "run"
INTRO_LINES = ("ÇÖLDE GECE.", "DURURSAN DONARSIN.")
V1 = ROOT / "Tanitim" / "desert_gecko_reels_v1.mp4"
V1_SKIP_FRAMES, V1_FRAMES = 75, 751  # ilk sürüm: baştaki fırtına sahnesi atlanır


def intro_frames():
    """
    Giriş (4 sn): siyah ekran, yıldızlar; iki satır daktilo gibi yazılır (ikincisi buz mavisi ve titrer);
    beyaz bir parlamayla oyunun adı ekrana vurur, altında Gila koşar; son parlamayla oynanışa geçilir.
    """
    import random
    rng = random.Random(7)
    stars = [(rng.randrange(SIZE[0] // 6) * 6, rng.randrange(SIZE[1] // 6) * 6, rng.random()) for _ in range(90)]
    run = [Image.open(f).convert("RGBA") for f in sorted(GILA_RUN.glob("gila_run_*.png"), key=lambda p: int(p.stem.split("_")[-1]))]
    run = [g.resize((g.width * 9, g.height * 9), Image.NEAREST) for g in run]
    title_lines = TITLE

    def backdrop(i):
        frame = Image.new("RGBA", SIZE, (8, 6, 16, 255))
        draw = ImageDraw.Draw(frame)
        for x, y, phase in stars:
            twinkle = (i * 0.09 + phase) % 1.0
            if twinkle < 0.75:
                shade = 110 + int(phase * 120)
                draw.rectangle([x, y, x + 5, y + 5], fill=(shade, shade, min(255, shade + 30), 255))
        return frame

    first_at, second_at, flash_at, title_at, total = 8, 42, TITLE_FRAME - 2, TITLE_FRAME, INTRO_FRAMES
    for i in range(total):
        frame = backdrop(i)
        if i < flash_at:
            # Daktilo: her 2 karede bir harf
            if i >= first_at:
                shown = INTRO_LINES[0][:max(1, (i - first_at) // 2 + 1)]
                text = pixel_text([shown], 15, (255, 232, 160))
                full = pixel_text([INTRO_LINES[0]], 15, (255, 232, 160))
                frame.alpha_composite(text, ((SIZE[0] - full.width) // 2, 760))
            if i >= second_at:
                shown = INTRO_LINES[1][:max(1, (i - second_at) // 2 + 1)]
                text = pixel_text([shown], 12, (150, 215, 255))
                full = pixel_text([INTRO_LINES[1]], 12, (150, 215, 255))
                done = (i - second_at) // 2 + 1 >= len(INTRO_LINES[1])
                shiver = rng.choice((-6, 0, 6)) if done else 0  # üşüme titremesi
                frame.alpha_composite(text, ((SIZE[0] - full.width) // 2 + shiver, 930))
        elif i < title_at:
            frame = Image.new("RGBA", SIZE, (255, 255, 255, 255))
        else:
            k = i - title_at
            # Başlık büyük gelip yerine oturur
            scale = (10, 8, 7)[k] if k < 3 else TEXT_SCALE  # 6 kat: ekranın genişliğine tam sığar
            title = pixel_text(title_lines, 19, (244, 196, 72), scale=scale)
            frame.alpha_composite(title, ((SIZE[0] - title.width) // 2, 640 - title.height // 2))
            gila = run[(k // 2) % len(run)]  # 15 kare/sn koşu
            ground = 1290
            ImageDraw.Draw(frame).rectangle([0, ground, SIZE[0], ground + 11], fill=(92, 52, 34, 255))
            for d in range(6):  # koşunun altından geriye savrulan toz
                dx = (k * 46 + d * 190) % (SIZE[0] + 200) - 100
                ImageDraw.Draw(frame).rectangle([SIZE[0] - dx, ground - 12 - (d % 3) * 12, SIZE[0] - dx + 35, ground - 7 - (d % 3) * 12], fill=(160, 110, 70, 255))
            frame.alpha_composite(gila, ((SIZE[0] - gila.width) // 2, ground - int(gila.height * 0.75)))
            if k >= TAGLINE_AFTER:
                tag = pixel_text([TAGLINE], 9, (255, 240, 214))
                frame.alpha_composite(tag, ((SIZE[0] - tag.width) // 2, 1400))
            if i >= total - 2:
                frame = Image.new("RGBA", SIZE, (255, 255, 255, 255))  # oynanışa parlamayla geç
        yield frame


def frames_of(folder):
    files = sorted((RAW / folder).glob("*.png"))
    assert files, f"kare yok: {folder} (önce Reels_Kayit testini çalıştırın)"
    return files


CLOSING_CARD = ROOT / "Tanitim" / "kapanis_karti.png"  # Gemini'nin yatay başlık ekranı (1280x720)
CLOSING_FRAMES, CLOSING_FADE_IN, CLOSING_FADE_OUT = 150, 12, 24  # 5 saniye
V1_END_CARD_FRAMES = 96  # ilk sürümün piksel yazılı kapanışı: kullanılmaz
V1_CUT = (260, 289)  # ilk sürümdeki asansör sahnesinin ilk yarısı (bekleme): atlanır


def closing_layers():
    """
    Yatay kartı dikey kadraja oturtur: kartın yazıları kesilmeden ekranın ortasında durur, üstü ve altı
    kartın kendi bulanık ve karartılmış büyütülmüş haliyle dolar, kenarlar yumuşak geçişle birleşir.
    """
    from PIL import ImageFilter
    card = Image.open(CLOSING_CARD).convert("RGB")
    w, h = card.size
    # Arka: kartın yazısız kısımlarından. Üst yarı gökyüzü şeridinin (ilk 130 satır), alt yarı en alttaki koyu
    # zeminin uzatılmasıyla dolar; kartın büyütülmüş hali kullanılsaydı yazıların bulanık kopyası görünürdü.
    half = SIZE[1] // 2
    sky = card.crop((160, 0, 1120, 130)).resize((SIZE[0], half), Image.BICUBIC)
    floor = card.crop((160, h - 12, 1120, h)).resize((SIZE[0], SIZE[1] - half), Image.BICUBIC)
    cover = Image.new("RGB", SIZE)
    cover.paste(sky, (0, 0))
    cover.paste(floor, (0, half))
    cover = cover.filter(ImageFilter.GaussianBlur(40))
    # Ön: yazıların sığdığı orta 1060 piksel, ekran genişliğine büyütülür.
    crop = card.crop((110, 0, 1170, h))  # yazıların iki yanında pay kalsın
    front = crop.resize((SIZE[0], int(h * SIZE[0] / 1060)), Image.LANCZOS)
    feather = 150
    mask = Image.new("L", front.size, 255)
    draw = ImageDraw.Draw(mask)
    for y in range(feather):
        value = int(255 * y / feather)
        draw.line([(0, y), (front.width, y)], fill=value)
        draw.line([(0, front.height - 1 - y), (front.width, front.height - 1 - y)], fill=value)
    return cover, front, mask


def closing_frames(last_frame):
    """Son oynanış karesinden karta geçiş, kart çok yavaş yaklaşır, sonunda siyaha kararır."""
    cover, front, mask = closing_layers()
    black = Image.new("RGB", SIZE, (0, 0, 0))
    for i in range(CLOSING_FRAMES):
        zoom = 1.0 + 0.03 * i / CLOSING_FRAMES
        size = (int(front.width * zoom), int(front.height * zoom))
        frame = cover.copy()
        frame.paste(front.resize(size, Image.BICUBIC), ((SIZE[0] - size[0]) // 2, (SIZE[1] - size[1]) // 2 - 40), mask.resize(size, Image.BICUBIC))
        if i < CLOSING_FADE_IN and last_frame is not None:
            frame = Image.blend(last_frame, frame, (i + 1) / (CLOSING_FADE_IN + 1))
        left = CLOSING_FRAMES - i
        if left <= CLOSING_FADE_OUT:
            frame = Image.blend(black, frame, max(0.0, (left - 4) / (CLOSING_FADE_OUT - 4)))  # son 4 kare tam siyah
        yield frame


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=ROOT / "Tanitim" / "desert_gecko_reels.mp4")
    args = parser.parse_args()
    args.out.parent.mkdir(parents=True, exist_ok=True)

    use_raw = RAW.exists() and all((RAW / s[0]).exists() for s in SCENES)
    intro = list(intro_frames())
    v1_last = V1_FRAMES - V1_END_CARD_FRAMES - 1
    body = sum(s[2] for s in SCENES) if use_raw else v1_last - V1_SKIP_FRAMES + 1 - (V1_CUT[1] - V1_CUT[0] + 1)
    total = len(intro) + body + CLOSING_FRAMES
    duration = total / FPS
    command = [
        imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error",
        "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{SIZE[0]}x{SIZE[1]}", "-r", str(FPS), "-i", "-",
        "-ss", f"{MUSIC_START:.3f}", "-i", str(MUSIC),
        "-map", "0:v", "-map", "1:a",
        "-af", f"afade=t=in:d=0.4,afade=t=out:st={duration - 2.2:.2f}:d=2.2",
        "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "192k", "-t", f"{duration:.3f}", "-movflags", "+faststart", str(args.out),
    ]
    encoder = subprocess.Popen(command, stdin=subprocess.PIPE)
    last = None

    def emit(image):
        encoder.stdin.write(image.convert("RGB").tobytes())

    for frame in intro:
        emit(frame)

    if use_raw:
        for folder, start, count, caption in SCENES:
            files = frames_of(folder)
            text = pixel_text([caption], 15, (255, 232, 160)) if caption else None
            for i in range(count):
                frame = Image.open(files[min(start + i, len(files) - 1)]).convert("RGBA").resize(SIZE, Image.NEAREST)
                if text is not None:
                    # Yazı ilk karelerde yukarıdan yerine oturur.
                    slide = max(0, 5 - i) * 14
                    frame.alpha_composite(text, ((SIZE[0] - text.width) // 2, CAPTION_Y - slide))
                emit(frame)
                last = frame.convert("RGB")
    else:
        # Ham kareler yok: ilk sürümün videosundan; baştaki fırtına sahnesi ve sondaki eski kapanış atlanır.
        assert V1.exists(), f"ne ham kare var ne de {V1}"
        decoder = subprocess.Popen([
            imageio_ffmpeg.get_ffmpeg_exe(), "-loglevel", "error", "-i", str(V1),
            "-vf", f"select='between(n,{V1_SKIP_FRAMES},{V1_CUT[0] - 1})+between(n,{V1_CUT[1] + 1},{v1_last})'", "-fps_mode", "passthrough",
            "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE)
        size = SIZE[0] * SIZE[1] * 3
        data = None
        while True:
            chunk = decoder.stdout.read(size)
            if len(chunk) < size:
                break
            data = chunk
            encoder.stdin.write(data)
        decoder.wait()
        last = Image.frombytes("RGB", SIZE, data)

    for frame in closing_frames(last):
        emit(frame)

    encoder.stdin.close()
    assert encoder.wait() == 0, "ffmpeg başarısız"
    print(f"hazır: {args.out}  {duration:.1f} sn")


if __name__ == "__main__":
    main()
