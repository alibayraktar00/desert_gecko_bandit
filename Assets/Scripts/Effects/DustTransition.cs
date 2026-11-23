using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Sahne geçişinde soldan sağa esen yarı saydam çöl tozu. Tek bir duvar değil, onlarca yumuşak toz topağıdır:
    /// farklı boy ve hızlarda soldan doğar, sağa savrulurken büyüyüp söner; üst üste geldikleri yerde toz koyulaşır,
    /// aralarından oyun görünür. Sahne değişirken ekranı kısa süre hafif bir kum perdesi örter ki atlama görünmesin.
    /// Oynatılınca kendini sahneler arası taşır, bitince siler. Duraklatma etkilemesin diye ölçeksiz zaman.
    /// </summary>
    public class DustTransition : MonoBehaviour
    {
        [Tooltip("Toz topaklarının ebeveyni (tam ekran, 640x360 referanslı kanvas)")]
        [SerializeField] RectTransform area;
        [Tooltip("Yarı saydam toz topağı görseli")]
        [SerializeField] Sprite puff;
        [Tooltip("Sahne değişirken kısa süre koyulaşan tam ekran kum perdesi")]
        [SerializeField] Image veil;
        [SerializeField] int puffCount = 46;
        [Tooltip("Topakların doğduğu süre (saniye)")]
        [SerializeField] float spawnTime = 1.3f;
        [Tooltip("Sahnenin yüklendiği an (saniye)")]
        [SerializeField] float loadTime = 1.25f;
        [Tooltip("Perdenin en yoğun hali (0-1)")]
        [SerializeField] float veilAlpha = 0.55f;
        [SerializeField] Vector2 sizeRange = new Vector2(150f, 300f);
        [SerializeField] Vector2 speedRange = new Vector2(240f, 400f);
        [Tooltip("Kaplarken çalan rüzgâr sesi (yoksa boş)")]
        [SerializeField] AudioSource sound;

        struct Puff
        {
            public RectTransform rect;
            public Image image;
            public float born, speed, rise, size, alpha, y;
        }

        bool playing;

        public bool IsPlaying => playing;

        void Awake()
        {
            if (veil != null)
                veil.enabled = false;
        }

        /// <summary>Tozu çalar; toz en yoğunken load çağrılır (ör. SceneFlow.LoadLevel).</summary>
        public void Play(Action load)
        {
            if (playing)
                return;
            StartCoroutine(Run(load));
        }

        IEnumerator Run(Action load)
        {
            playing = true;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            if (sound != null)
                sound.Play();

            float width = area.rect.width, height = area.rect.height;
            var rng = new System.Random(System.Environment.TickCount);
            float Range(Vector2 r) => r.x + (float)rng.NextDouble() * (r.y - r.x);
            var puffs = new Puff[puffCount];
            for (int i = 0; i < puffs.Length; i++)
            {
                var img = new GameObject($"Topak_{i}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.rectTransform.SetParent(area, false);
                img.sprite = puff;
                img.raycastTarget = false;
                img.enabled = false;
                puffs[i] = new Puff
                {
                    rect = img.rectTransform,
                    image = img,
                    born = spawnTime * i / puffs.Length + (float)rng.NextDouble() * 0.08f,
                    speed = Range(speedRange),
                    rise = -12f + (float)rng.NextDouble() * 28f,
                    size = Range(sizeRange),
                    alpha = 0.55f + (float)rng.NextDouble() * 0.35f,
                    y = ((float)rng.NextDouble() - 0.5f) * height * 1.05f,
                };
            }

            bool loaded = false;
            float start = Time.unscaledTime;
            while (true)
            {
                float t = Time.unscaledTime - start;
                bool any = false;
                for (int i = 0; i < puffs.Length; i++)
                {
                    Puff p = puffs[i]; // yalnızca okunur; görsel alanları referans tipi
                    float age = t - p.born;
                    if (age < 0f)
                    {
                        any = true;
                        continue;
                    }
                    float x = -width * 0.5f - p.size * 0.6f + p.speed * age;
                    if (x - p.size > width * 0.5f)
                    {
                        p.image.enabled = false;
                        continue;
                    }
                    any = true;
                    float grow = 1f + 0.7f * Mathf.Clamp01(age / 2.2f);
                    p.rect.anchoredPosition = new Vector2(x, p.y + p.rise * age);
                    p.rect.sizeDelta = new Vector2(p.size, p.size * 0.8f) * grow;
                    float fadeIn = Mathf.Clamp01(age / 0.25f);
                    float fadeOut = Mathf.Clamp01((width * 0.5f + p.size - x) / (width * 0.45f)); // sağa yaklaştıkça incelir
                    p.image.color = new Color(1f, 1f, 1f, p.alpha * fadeIn * fadeOut);
                    p.image.enabled = true;
                }

                // sahne değişimi: perde kısa süre koyulaşır, yükleme tam ortasında
                if (veil != null)
                {
                    float v = Mathf.Clamp01(1f - Mathf.Abs(t - loadTime) / 0.35f);
                    veil.enabled = v > 0f;
                    veil.color = new Color(veil.color.r, veil.color.g, veil.color.b, veilAlpha * v);
                }
                if (!loaded && t >= loadTime)
                {
                    loaded = true;
                    load?.Invoke();
                }
                if (loaded && !any && t > loadTime + 0.4f)
                    break;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
