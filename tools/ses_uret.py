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
  Assets/Audio/altin_toplama.wav  altın toplanır: iki hızlı parlak nota, madeni şıngırtı
  Assets/Audio/patlama.wav        dinamit patlaması: sert darbe, derin gümbürtü, toz hışırtısı
  Assets/Audio/kazma.wav          kazma vuruşu: madeni çınlama, tok taş darbesi
  Assets/Audio/pompali.wav        pompalı atışı: tok ve geniş, uzun boğuk kuyruk
  Assets/Audio/pompa.wav          pompa sürgüsü: "çak-çak"
  Assets/Audio/sekme.wav          seken mermi: vuruş ve inen tiz ıslık
  Assets/Audio/silah_al.wav       silah alma: kavrama ve madeni şıkırtı
  Assets/Audio/savurma.wav        savurma: dinamit atma, kazma sallama
  Assets/Audio/fitil.wav          dinamit fitili: çıtırdayan cızırtı (1,3 sn)

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


def coin_chime(rng, seconds=0.45):
    """Altın: yükselen iki hızlı nota (madeni armoniklerle), altında kısa şıngırtı."""
    t = np.arange(int(seconds * RATE)) / RATE

    def bell(freq, start):
        u = np.maximum(t - start, 0.0)
        on = (t >= start).astype(float)
        tone = np.sin(2 * np.pi * freq * u) + 0.4 * np.sin(2 * np.pi * freq * 2.76 * u) + 0.2 * np.sin(2 * np.pi * freq * 5.4 * u)
        return tone * np.exp(-u * 9.0) * np.minimum(u / 0.003, 1.0) * on

    noise = rng.normal(0.0, 1.0, t.size)
    shards = (noise - np.convolve(noise, np.ones(2) / 2, mode="same")) * np.exp(-t * 40.0)
    return (bell(987.8, 0.0) * 0.4 + bell(1318.5, 0.06) * 0.5 + shards * 0.3) * 0.9


def explosion(rng, seconds=1.1):
    """Dinamit patlaması: sert gürültü darbesi, altında derin bir gümbürtü, sonra dağılan toz hışırtısı."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    boom = np.convolve(noise, np.ones(40) / 40, mode="same") * 6.0 * np.exp(-t * 4.5)  # boğuk gövde
    crack = noise * np.exp(-t * 30.0)                                                  # ilk sert vuruş
    rumble = np.sin(2 * np.pi * (55.0 * t - 18.0 * t ** 2)) * np.exp(-t * 3.5)          # derin gümbürtü
    hiss = np.convolve(noise, np.ones(6) / 6, mode="same") * np.exp(-t * 6.0) * 0.4     # dağılan toz
    attack = np.minimum(t / 0.004, 1.0)
    return (crack * 0.6 + boom * 0.7 + rumble * 0.8 + hiss) * attack * 0.8


def pick_clank(rng, seconds=0.4):
    """Kazma vuruşu: madeni çınlama (uyumsuz tınılar) ve altında tok bir taş darbesi."""
    t = np.arange(int(seconds * RATE)) / RATE
    ring = sum(np.sin(2 * np.pi * f * t) * a for f, a in ((1180.0, 0.5), (1730.0, 0.35), (2610.0, 0.25))) * np.exp(-t * 14.0)
    thud = np.convolve(rng.normal(0.0, 1.0, t.size), np.ones(30) / 30, mode="same") * 5.0 * np.exp(-t * 35.0)
    click = rng.normal(0.0, 1.0, t.size) * np.exp(-t * 90.0) * 0.6
    return (ring * 0.6 + thud * 0.8 + click) * 0.8


def shotgun_blast(rng, seconds=0.6):
    """Pompalı: altıpatlardan tok ve geniş; uzun boğuk kuyruk."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    crack = noise * np.exp(-t * 22.0)
    body = np.convolve(noise, np.ones(24) / 24, mode="same") * 4.0 * np.exp(-t * 7.0)
    thump = np.sin(2 * np.pi * (90.0 * t - 70.0 * t ** 2)) * np.exp(-t * 9.0)
    return (crack * 0.7 + body * 0.8 + thump * 0.9) * 0.75


def pump_rack(rng, seconds=0.32):
    """Pompa: iki kısa madeni tık ("çak-çak"): geri ve ileri sürgü."""
    t = np.arange(int(seconds * RATE)) / RATE
    out = np.zeros(t.size)
    for start, pitch in ((0.0, 2400.0), (0.15, 1900.0)):
        u = np.clip(t - start, 0.0, None)
        on = t >= start
        click = rng.normal(0.0, 1.0, t.size) * np.exp(-u * 160.0)
        ring = np.sin(2 * np.pi * pitch * u) * np.exp(-u * 60.0) * 0.5
        slide = np.convolve(rng.normal(0.0, 1.0, t.size), np.ones(6) / 6, mode="same") * np.exp(-u * 40.0) * 0.4
        out += (click + ring + slide) * on
    return out * 0.6


def ricochet(rng, seconds=0.35):
    """Seken mermi: kısa vuruş ve inen tiz bir ıslık ("pıııv")."""
    t = np.arange(int(seconds * RATE)) / RATE
    hit = rng.normal(0.0, 1.0, t.size) * np.exp(-t * 120.0) * 0.7
    freq = 3200.0 * np.exp(-t * 2.2) + 900.0
    phase = np.cumsum(freq) / RATE
    whine = np.sin(2 * np.pi * phase) * np.exp(-t * 9.0) * np.minimum(t / 0.01, 1.0) * 0.45
    return hit + whine


def weapon_pickup(rng, seconds=0.25):
    """Silah alma: tok bir kavrama ve madeni şıkırtı."""
    t = np.arange(int(seconds * RATE)) / RATE
    grab = np.convolve(rng.normal(0.0, 1.0, t.size), np.ones(18) / 18, mode="same") * 4.0 * np.exp(-t * 45.0)
    u = np.clip(t - 0.06, 0.0, None)
    clink = sum(np.sin(2 * np.pi * f * u) * a for f, a in ((2100.0, 0.4), (3150.0, 0.25))) * np.exp(-u * 30.0) * (t >= 0.06)
    return (grab * 0.7 + clink) * 0.7


def whoosh(rng, seconds=0.28):
    """Savurma: yükselip sönen hava hışırtısı (dinamit atma, kazma sallama)."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = np.convolve(rng.normal(0.0, 1.0, t.size), np.ones(10) / 10, mode="same") * 3.0
    envelope = np.sin(np.pi * np.clip(t / seconds, 0.0, 1.0)) ** 2
    return noise * envelope * 0.55


def fuse_hiss(rng, seconds=1.3):
    """Fitil: çıtırdayan tiz cızırtı, biterken hızlanır."""
    t = np.arange(int(seconds * RATE)) / RATE
    noise = rng.normal(0.0, 1.0, t.size)
    hiss = noise - np.convolve(noise, np.ones(4) / 4, mode="same")
    crackle = (rng.random(t.size) < 0.004 * (1.0 + 2.0 * t / seconds)) * rng.normal(0.0, 3.0, t.size)
    envelope = np.minimum(t / 0.05, 1.0) * np.minimum((seconds - t) / 0.05, 1.0)
    return (hiss * 0.25 + crackle * 0.4) * envelope


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
    save("altin_toplama.wav", coin_chime(rng))
    # Yeni sesler kendi tohumuyla: öncekilerin rastgele dizisi bozulmasın.
    save("patlama.wav", explosion(np.random.default_rng(11)))
    save("kazma.wav", pick_clank(np.random.default_rng(12)))
    save("pompali.wav", shotgun_blast(np.random.default_rng(13)))
    save("pompa.wav", pump_rack(np.random.default_rng(14)))
    save("sekme.wav", ricochet(np.random.default_rng(15)))
    save("silah_al.wav", weapon_pickup(np.random.default_rng(16)))
    save("savurma.wav", whoosh(np.random.default_rng(17)))
    save("fitil.wav", fuse_hiss(np.random.default_rng(18)))


if __name__ == "__main__":
    main()
