using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Ekran sarsıntısı ("travma" modeli): her sarsıntı 0-1 arası travma ekler, travma zamanla söner.
    /// Kayma ve dönme travmanın karesiyle büyür: küçük vuruşlar hafif titrer, patlama sert sallar.
    /// Gürültü Perlin'den gelir, titreme rastgele zıplamaz, sallanır. CameraFollow uygular.
    /// </summary>
    public static class CameraShake
    {
        const float MaxOffset = 0.45f;   // birim
        const float MaxAngle = 2.2f;     // derece
        const float Decay = 1.6f;        // saniyede sönen travma
        const float Frequency = 22f;     // sallanma hızı
        const float DefaultRange = 14f;  // bu uzaklıktaki sarsıntı hissedilmez

        static float trauma;
        static float lastTime;

        /// <summary>Sarsıntı ekler (0-1). Üst üste gelenler birikir ama 1'i geçmez.</summary>
        public static void Add(float amount)
        {
            Settle();
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>Kaynağı belli sarsıntı: kameradan uzaklaştıkça azalır, menzil dışında hiç hissedilmez.</summary>
        public static void AddAt(Vector2 position, float amount, float range = DefaultRange)
        {
            Camera cam = Camera.main;
            float distance = cam != null ? Vector2.Distance(cam.transform.position, position) : 0f;
            float falloff = 1f - Mathf.Clamp01(distance / range);
            if (falloff > 0f)
                Add(amount * falloff);
        }

        /// <summary>Bu karenin kayması (birim) ve dönmesi (derece).</summary>
        public static void Sample(out Vector2 offset, out float angle)
        {
            Settle();
            float power = trauma * trauma;
            float t = Time.time * Frequency;
            offset = new Vector2(Mathf.PerlinNoise(t, 0.1f) * 2f - 1f, Mathf.PerlinNoise(0.7f, t) * 2f - 1f) * (MaxOffset * power);
            angle = (Mathf.PerlinNoise(t, 3.3f) * 2f - 1f) * MaxAngle * power;
        }

        public static void Clear() => trauma = 0f;

        static void Settle()
        {
            float now = Time.time;
            // Sahne yeniden başlayınca Time.time sıfırlanır: geri giden zaman travmayı büyütmesin.
            trauma = Mathf.Max(0f, trauma - Decay * Mathf.Max(0f, now - lastTime));
            lastTime = now;
        }
    }
}
