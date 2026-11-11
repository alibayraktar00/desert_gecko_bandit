using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Fırlatılan kuyruk: fizikle uçar; gergin, sivri ucu önde, uçuş yönüne bakar ve dalgalanarak ilerler
    /// (kareler kuyruk boyunca akan S dalgası, tools/kuyruk_mermi_ciz.py). Düşmana çarparsa hasar verip kırılır,
    /// zemine/duvara çarparsa ya da süresi dolarsa yok olur.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TailProjectile : MonoBehaviour
    {
        [SerializeField] int damage = 3;
        [SerializeField] float lifetime = 3f;
        [Tooltip("Dalgalanma kareleri (sağa bakar, kopan uç arkada)")]
        [SerializeField] Sprite[] frames;
        [SerializeField] float fps = 16f;
        [SerializeField] Transform visual;
        [SerializeField] Color burstColor = new Color32(0xE8, 0xA0, 0x4A, 0xFF);

        Rigidbody2D body;
        SpriteRenderer sr;
        float destroyAt, launchedAt;
        bool broken;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (visual != null)
                sr = visual.GetComponent<SpriteRenderer>();
            destroyAt = Time.time + lifetime;
            launchedAt = Time.time;
        }

        public void Launch(Vector2 velocity)
        {
            body.linearVelocity = velocity;
            destroyAt = Time.time + lifetime;
            launchedAt = Time.time;
            Aim(velocity);
        }

        /// <summary>Sivri uç uçuş yönüne bakar; sola giderken ters durmasın diye dikey aynalanır.</summary>
        void Aim(Vector2 velocity)
        {
            if (visual == null || velocity.sqrMagnitude < 0.01f)
                return;
            visual.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            if (sr != null)
                sr.flipY = velocity.x < 0f;
        }

        void Update()
        {
            Aim(body.linearVelocity);
            if (sr != null && frames != null && frames.Length > 0)
                sr.sprite = frames[Mathf.FloorToInt((Time.time - launchedAt) * fps) % frames.Length];
            if (Time.time >= destroyAt)
                Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (broken)
                return;

            if (((1 << other.gameObject.layer) & GameLayers.GroundMask) != 0)
            {
                Break();
                return;
            }

            var target = other.GetComponentInParent<IDamageable>();
            if (target == null || target is PlayerHealth || !target.IsAlive)
                return;

            target.TakeDamage(new DamageInfo(damage, body.linearVelocity.normalized, DamageKind.Tail));
            Break();
        }

        void Break()
        {
            broken = true;
            PixelBurst.Spawn(transform.position, burstColor, 6, 3f, 0.3f);
            Destroy(gameObject);
        }
    }
}
