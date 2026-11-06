using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Sıcak hava sütunu: içinde süzülen Gila yükselir. Yukarı akan toz çizgileriyle görünür.
    /// Tepesinde taşıma biter, oyuncu orada dengede kalır ve süzülerek ayrılır.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Thermal : MonoBehaviour
    {
        const int StreakCount = 30;

        [SerializeField] float liftSpeed = 6f;
        [SerializeField] float streakSpeed = 5f;
        [SerializeField] Color streakColor = new Color32(0xFF, 0xF2, 0xD0, 0xE6);
        [SerializeField] SpriteRenderer[] streaks;

        BoxCollider2D zone;

        /// <summary>Editörde bir kez kurulur: bölge ve toz çizgileri sahneye kaydedilir.</summary>
        public void Configure(Vector2 size, float lift, Sprite pixel)
        {
            liftSpeed = lift;
            zone = GetComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = size;

            streaks = new SpriteRenderer[StreakCount];
            for (int i = 0; i < StreakCount; i++)
            {
                var sr = new GameObject("Toz").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = pixel;
                sr.sortingOrder = -5; // arka planın önünde, oyuncunun arkasında
                float length = 0.5f + (i % 4) * 0.3f;
                Vector2 unit = pixel.bounds.size;
                sr.transform.localScale = new Vector3(0.14f / unit.x, length / unit.y, 1f);
                streaks[i] = sr;
            }
            Animate(0f);
        }

        void Awake()
        {
            zone = GetComponent<BoxCollider2D>();
        }

        void Update()
        {
            Animate(Time.time);
        }

        // Kareler yukarı akar, altta ve üstte solar. Konum sadece zamandan hesaplanır.
        void Animate(float time)
        {
            if (streaks == null)
                return;
            Vector2 size = zone.size;
            for (int i = 0; i < streaks.Length; i++)
            {
                // İki ayrı dizi: aynısı kullanılırsa çizgiler tek bir çapraz hatta dizilir.
                float seedX = i * 0.7549f % 1f, seedY = i * 0.5698f % 1f;
                float along = (time * streakSpeed / size.y + seedY) % 1f;
                float x = (seedX - 0.5f) * size.x * 0.92f + Mathf.Sin(time * 2f + i) * 0.12f;
                streaks[i].transform.localPosition = new Vector3(x, (along - 0.5f) * size.y, 0f);
                Color c = streakColor;
                c.a *= Mathf.Sin(along * Mathf.PI);
                streaks[i].color = c;
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                player.RideThermal(liftSpeed);
        }
    }
}
