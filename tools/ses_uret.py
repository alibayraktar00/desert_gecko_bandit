"""
Yılan silahşorün geçici ses efektlerini üretir (gerçek sesler gelene kadar):
  Assets/Audio/cingirak.wav  çıngırak uyarısı, uyarı süresi kadar (0.8 sn)
  Assets/Audio/silah.wav     silah patlaması
  Assets/Audio/fener.wav     checkpoint feneri tutuşması

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


def main():
    rng = np.random.default_rng(7)  # her çalıştırmada aynı ses
    save("cingirak.wav", rattle(rng))
    save("silah.wav", gunshot(rng))
    save("fener.wav", lantern(rng))


if __name__ == "__main__":
    main()
