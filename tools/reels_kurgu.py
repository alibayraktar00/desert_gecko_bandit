"""
Instagram Reels tanıtım videosunu kurgular: Temp/Reels altındaki ham kareleri (AutoplayTests.Reels_Kayit çıktısı)
sıraya dizer, üstlerine yazı koyar, oyunun müziğini ekler ve 1080x1920 mp4 üretir.

Ham kareler 540x960; piksel sanatı bulanmasın diye tam 2 kat (en yakın komşu) büyütülür.
Yazılar da küçük çizilip büyütülür, oyunun piksel görünümüne uyar.

Gerekenler: Pillow, imageio-ffmpeg (ffmpeg'i kendisi getirir).
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
    ("L3_firtina", 20, 75, "FIRTINA GELİYOR"),
    ("L3_kanca", 15, 65, "DİLİNLE SALLAN"),
    ("L1_son", 60, 60, "TIRMAN, SÜZÜL"),
    ("L3_gayzer", 20, 60, "GAYZERLE FIRLA"),
    ("L2_asansor", 95, 60, ""),
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
MUSIC_START = 8.0  # şarkının girişi atlanır


def pixel_text(lines, size, fill, outline=(31, 15, 11)):
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
    return image.resize((image.width * TEXT_SCALE, image.height * TEXT_SCALE), Image.NEAREST)


def frames_of(folder):
    files = sorted((RAW / folder).glob("*.png"))
    assert files, f"kare yok: {folder} (önce Reels_Kayit testini çalıştırın)"
    return files


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=ROOT / "Tanitim" / "desert_gecko_reels.mp4")
    args = parser.parse_args()
    args.out.parent.mkdir(parents=True, exist_ok=True)

    total = sum(s[2] for s in SCENES) + END_CARD[2]
    duration = total / FPS
    command = [
        imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error",
        "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{SIZE[0]}x{SIZE[1]}", "-r", str(FPS), "-i", "-",
        "-ss", str(MUSIC_START), "-i", str(MUSIC),
        "-map", "0:v", "-map", "1:a",
        "-af", f"afade=t=in:d=0.4,afade=t=out:st={duration - 1.5:.2f}:d=1.5",
        "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "192k", "-t", f"{duration:.3f}", "-movflags", "+faststart", str(args.out),
    ]
    encoder = subprocess.Popen(command, stdin=subprocess.PIPE)

    def emit(image):
        encoder.stdin.write(image.convert("RGB").tobytes())

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

    # Kapanış: karartılmış oyun karesi üstünde başlık
    folder, index, count = END_CARD
    background = Image.open(frames_of(folder)[index]).convert("RGB").resize(SIZE, Image.NEAREST)
    title = pixel_text(TITLE, 19, (244, 196, 72))
    tagline = pixel_text([TAGLINE], 9, (255, 240, 214))
    for i in range(count):
        dim = max(0.38, 1 - i / 12 * 0.62)
        frame = ImageEnhance.Brightness(background).enhance(dim).convert("RGBA")
        if i >= 6:
            frame.alpha_composite(title, ((SIZE[0] - title.width) // 2, 700 - max(0, 12 - i) * 10))
        if i >= 20:
            frame.alpha_composite(tagline, ((SIZE[0] - tagline.width) // 2, 1010))
        emit(frame)

    encoder.stdin.close()
    assert encoder.wait() == 0, "ffmpeg başarısız"
    print(f"hazır: {args.out}  {duration:.1f} sn")


if __name__ == "__main__":
    main()
