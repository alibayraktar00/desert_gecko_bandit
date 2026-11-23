using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Mavi Dev'in yere çarpınca çıkardığı şok dalgası. Görsel pixelfx'in ground-slam efekti (dev_sok_*.png, 30 fps):
    /// iki yarım ay cephe zeminde sağa sola koşar, toz sütunu yükselip söner.
    /// Hasar görselle aynı zamanlamada: cephe merkezden reach'e ease-out ile shockDuration içinde açılır.
    /// Cephenin geçtiği yerde yerdeki (ya da yere çok yakın) Gila 2 kalp kaybeder; zamanında zıplayan kurtulur.
    /// Her dalga Gila'ya en fazla bir kez vurur.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GiantShockwave : MonoBehaviour
    {
        const float Fps = 30f;

        Sprite[] frames;
        SpriteRenderer sr;
        float age, reach, duration, height;
        int damage;
        bool hitDone;
        PlayerHealth player;

        /// <param name="reach">Cephenin varacağı en uzak mesafe (birim, her iki yöne)</param>
        /// <param name="duration">Cephenin reach'e varma süresi</param>
        /// <param name="height">Cephenin boyu: Gila'nın ayakları bundan yüksekteyse dalga altından geçer</param>
        public static GiantShockwave Spawn(Vector2 groundPoint, Sprite[] frames, int order, int damage, float reach, float duration, float height)
        {
            var go = new GameObject("SokDalgasi");
            go.transform.position = groundPoint;
            var wave = go.AddComponent<GiantShockwave>();
            wave.frames = frames;
            wave.sr = go.GetComponent<SpriteRenderer>();
            wave.sr.sortingOrder = order;
            if (frames != null && frames.Length > 0)
                wave.sr.sprite = frames[0];
            wave.damage = damage;
            wave.reach = reach;
            wave.duration = duration;
            wave.height = height;
            wave.player = FindAnyObjectByType<PlayerHealth>();
            return wave;
        }

        /// <summary>Cephenin merkeze uzaklığı: efektteki gibi küçük bir çekirdekten başlar, hızla açılıp yavaşlar.</summary>
        float Front(float t)
        {
            float k = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            return Mathf.Lerp(0.22f, reach, eased);
        }

        void Update()
        {
            age += Time.deltaTime;
            int index = Mathf.FloorToInt(age * Fps);
            if (frames == null || index >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            sr.sprite = frames[index];
        }

        void FixedUpdate()
        {
            if (hitDone || age > duration || player == null || !player.IsAlive)
                return;
            Vector2 origin = transform.position;
            Vector2 feet = player.transform.position;
            float dx = Mathf.Abs(feet.x - origin.x);
            float front = Front(age);
            // Cephe kalın bir duvar değil: önündeki ince şerit vurur, geçtiği yer artık güvenli.
            bool inBand = dx <= front + 0.35f && dx >= front - 1.1f;
            bool low = feet.y - origin.y < height && feet.y - origin.y > -0.6f;
            if (!inBand || !low)
                return;
            hitDone = true;
            float side = feet.x >= origin.x ? 1f : -1f;
            player.TakeDamage(damage, new Vector2(side, 0f));
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            float f = Front(age);
            Vector3 o = transform.position;
            Gizmos.DrawLine(o + new Vector3(-f, 0f), o + new Vector3(-f, height));
            Gizmos.DrawLine(o + new Vector3(f, 0f), o + new Vector3(f, height));
        }
    }
}
