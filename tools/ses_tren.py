"""Tren bölümünün sesleri: yerelde, numpy ile sentezlenir (dış servis yok).

tren_ray.wav     : ray tıkırtısı + gövde uğultusu, dikişsiz döngü
tren_dudugu.wav  : buhar düdüğü (üç sesli akor, titreşimli)
tren_fren.wav    : fren çığlığı, metal sürtünmesi
tren_kaza.wav    : çarpışma: gürleme, metal çatırtısı, devrilme

Çalıştır: python tools/ses_tren.py
"""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Audio")
rng = np.random.default_rng(7)


def write(name, x, gain=0.8):
    x = x / (np.max(np.abs(x)) + 1e-9) * gain
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def t_of(sec):
    return np.arange(int(SR * sec)) / SR


def lowpass(x, cutoff):
    # Tek kutuplu alçak geçiren: basit ve yeterli
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def bandpass(x, lo, hi):
    return lowpass(x, hi) - lowpass(x, lo)


def rail_loop():
    # 2.4 sn: her 0.6 sn'de bir "tıkı-tık" (iki tekerlek takımı ray ekinden geçer). Uğultu döngüde dikişsiz.
    t = t_of(2.4)
    n = len(t)
    rumble = lowpass(rng.standard_normal(n), 120) * 3.0
    rumble += np.sin(2 * np.pi * 48 * t) * 0.15
    clicks = np.zeros(n)
    for beat in np.arange(0, 2.4, 0.6):
        for off, amp in ((0.0, 1.0), (0.11, 0.8)):
            s = int((beat + off) * SR)
            k = np.arange(min(2600, n - s))
            env = np.exp(-k / 350)
            clicks[s:s + len(k)] += amp * env * bandpass(rng.standard_normal(len(k)), 300, 2500)[: len(k)] * 4
    x = rumble + clicks
    # Döngü dikişi: baş ve sonu yumuşakça örtüştür
    fade = int(0.05 * SR)
    ramp = np.linspace(0, 1, fade)
    x[:fade] = x[:fade] * ramp + x[-fade:] * (1 - ramp)
    write("tren_ray.wav", x, 0.6)


def whistle():
    t = t_of(1.9)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * t)
    x = sum(np.sin(2 * np.pi * f * vib * t) * a for f, a in ((392, 1.0), (494, 0.8), (587, 0.6), (784, 0.25)))
    breath = bandpass(rng.standard_normal(len(t)), 800, 4000) * 0.35
    env = np.minimum(1, t / 0.12) * np.minimum(1, (1.9 - t) / 0.5)
    write("tren_dudugu.wav", (x + breath) * env, 0.7)


def brakes():
    t = t_of(2.4)
    squeal = np.sin(2 * np.pi * (3100 + 180 * np.sin(2 * np.pi * 7 * t)) * t)
    squeal += 0.5 * np.sin(2 * np.pi * (4650 + 250 * np.sin(2 * np.pi * 11 * t)) * t)
    grind = bandpass(rng.standard_normal(len(t)), 1500, 7000) * 1.8
    env = np.minimum(1, t / 0.08) * np.clip((2.4 - t) / 0.8, 0, 1)
    write("tren_fren.wav", (squeal * 0.5 + grind) * env, 0.55)


def crash():
    t = t_of(3.6)
    n = len(t)
    boom = lowpass(rng.standard_normal(n), 90) * 6 * np.exp(-t / 0.9)
    boom += np.sin(2 * np.pi * 42 * t) * np.exp(-t / 0.7) * 0.8
    metal = np.zeros(n)
    for start in (0.0, 0.18, 0.42, 0.7, 1.05, 1.5, 2.0):
        s = int(start * SR)
        k = np.arange(n - s) / SR
        hit = bandpass(rng.standard_normal(n - s), 900, 6000) * np.exp(-k / 0.18)
        ring = np.sin(2 * np.pi * rng.uniform(600, 1400) * k) * np.exp(-k / 0.5) * 0.3
        metal[s:] += (hit + ring) * rng.uniform(0.5, 1.0) * np.exp(-start / 1.6)
    debris = bandpass(rng.standard_normal(n), 200, 2000) * np.exp(-t / 1.6) * 0.8
    write("tren_kaza.wav", boom + metal * 1.5 + debris, 0.9)


if __name__ == "__main__":
    rail_loop()
    whistle()
    brakes()
    crash()
    print("tamam:", ", ".join(f for f in os.listdir(OUT) if f.startswith("tren_") and f.endswith(".wav")))
