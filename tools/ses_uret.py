"""
Yılan silahşorün geçici ses efektlerini üretir (gerçek sesler gelene kadar):
  Assets/Audio/cingirak.wav  çıngırak uyarısı, uyarı süresi kadar (0.8 sn)
  Assets/Audio/silah.wav     silah patlaması
  Assets/Audio/fener.wav     checkpoint feneri tutuşması
  Assets/Audio/diken_gerilme.wav  Diken Mutantı'nın dikenleri dikilir (uyarı, 0.9 sn)
  Assets/Audio/diken_atis.wav     tek diken fırlatma vızıltısı
  Assets/Audio/diken_fis.wav      dikenler yeniden çıkar: tok "Fııışş"
  Assets/Audio/bocek_slap.wav     dil böceği kapar: ıslak "şlap"
  Assets/Audio/bocek_yutma.wav    yutkunma ve ardından cızırdayan enerji

Kullanım (proje kökünden):  python tools/ses_uret.py
Gereken paket: numpy
"""
import wave
from pathlib import Path

import numpy as np

OUTPUT_DIR = Path(__file__).resolve().parent.parent / "Assets" / "Audio"
RATE = 44100


def save(name, samples):
    data = (np.clip(samples, -1.0, 1.0) * 32767 * 0.9).astype(np.int16)
    with wave.open(str(OUTPUT_DIR / name), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())
    print(f"{name}: {len(data) / RATE:.2f} sn")


def rattle(rng, seconds=0.8, clicks_per_second=38.0):
    """Çıngırak: tiz gürültüden hızlı tıkırtılar. Yarı yolda biraz hızlanıp güçlenir."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    noise = noise - np.convolve(noise, np.ones(8) / 8, mode="same")  # kalın sesleri at
    rate = clicks_per_second * (1.0 + 0.25 * t / seconds)
    phase = np.cumsum(rate) / RATE % 1.0
    clicks = np.exp(-phase * 7.0)
    envelope = np.minimum(t / 0.04, 1.0) * np.minimum((seconds - t) / 0.03, 1.0) * (0.65 + 0.35 * t / seconds)
    return noise * clicks * envelope * 0.8


def gunshot(rng, seconds=0.35):
    """Silah: keskin gürültü patlaması ve altında hızla düşen boğuk bir vuruş."""
    t = np.arange(int(seconds * RATE)) / RATE
    crack = rng.normal(0.0, 1.0, t.size) * np.exp(-t * 28.0)
    thump = np.sin(2 * np.pi * (140.0 * t - 180.0 * t ** 2)) * np.exp(-t * 16.0)
    return crack * 0.8 + thump * 0.7


def lantern(rng, seconds=0.55):
    """Fener tutuşması: kısa kibrit çakması, ardından sönen alev hışırtısı."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.uniform(-1.0, 1.0, t.size)
    # Yumuşak gürültü: basit ortalama filtre alevin boğuk sesini verir.
    soft = np.convolve(noise, np.ones(14) / 14, mode="same") * 3.0
    strike = noise * np.exp(-t * 34.0) * 0.8
    flame = soft * np.minimum(1.0, t * 12.0) * np.exp(-t * 5.0)
    return strike + flame


def spike_tension(rng, seconds=0.9):
    """Gerilme: yükselen rüzgâr uğultusu ve altında titreşen gergin bir tel."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    wind = np.convolve(noise, np.ones(24) / 24, mode="same") * 4.0
    string = np.sin(2 * np.pi * (90.0 * t + 60.0 * t ** 2)) * (0.6 + 0.4 * np.sin(2 * np.pi * 22.0 * t))
    envelope = (t / seconds) ** 1.5 * np.minimum((seconds - t) / 0.03, 1.0)
    return (wind * 0.6 + string * 0.45) * envelope


def spike_shot(rng, seconds=0.12):
    """Diken atışı: kısa, tiz bir vızıltı."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    hiss = noise - np.convolve(noise, np.ones(6) / 6, mode="same")
    zip_ = np.sin(2 * np.pi * (1400.0 * t - 3000.0 * t ** 2))
    return (hiss * 0.7 + zip_ * 0.3) * np.exp(-t * 30.0) * np.minimum(t / 0.004, 1.0)


def spike_regrow(rng, seconds=0.7):
    """Fııışş: boğuk bir pof, ardından sönen basınçlı tıslama."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    hiss = noise - np.convolve(noise, np.ones(3) / 3, mode="same")
    pop = np.sin(2 * np.pi * (110.0 * t - 120.0 * t ** 2)) * np.exp(-t * 18.0)
    envelope = np.minimum(t / 0.02, 1.0) * np.exp(-t * 3.5)
    return hiss * envelope * 0.8 + pop * 0.6


def tongue_slap(rng, seconds=0.16):
    """Şlap: ıslak, kısa bir şaklama; gürültü patlaması ve altında hızla düşen yumuşak vuruş."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    wet = np.convolve(noise, np.ones(4) / 4, mode="same") * 2.0 * np.exp(-t * 45.0)
    smack = np.sin(2 * np.pi * (260.0 * t - 700.0 * t ** 2)) * np.exp(-t * 35.0)
    return (wet * 0.7 + smack * 0.6) * np.minimum(t / 0.002, 1.0)


def gulp_sizzle(rng, seconds=0.6):
    """Yutkunma: aşağı kayan boğuk 'glup', ardından jalapeño gibi cızırdayan sıcak enerji."""
    t = np.arange(int(seconds * RATE)) / RATE
    gulp_env = np.exp(-((t - 0.07) / 0.045) ** 2)
    gulp = np.sin(2 * np.pi * np.cumsum(220.0 - 300.0 * t) / RATE) * gulp_env
    noise = rng.normal(0.0, 1.0, t.size)
    hiss = noise - np.convolve(noise, np.ones(3) / 3, mode="same")
    crackle = (rng.random(t.size) < 0.004) * rng.uniform(0.5, 1.0, t.size)
    crackle = np.convolve(crackle, np.exp(-np.arange(80) / 12.0), mode="same")
    sizzle_env = np.clip((t - 0.1) / 0.06, 0.0, 1.0) * np.exp(-np.maximum(t - 0.1, 0.0) * 5.0)
    return gulp * 0.8 + (hiss * 0.35 + crackle * 0.5) * sizzle_env


def main():
    rng = np.random.default_rng(7)  # her çalıştırmada aynı ses
    save("cingirak.wav", rattle(rng))
    save("silah.wav", gunshot(rng))
    save("fener.wav", lantern(rng))
    save("diken_gerilme.wav", spike_tension(rng))
    save("diken_atis.wav", spike_shot(rng))
    save("diken_fis.wav", spike_regrow(rng))
    save("bocek_slap.wav", tongue_slap(rng))
    save("bocek_yutma.wav", gulp_sizzle(rng))


if __name__ == "__main__":
    main()
