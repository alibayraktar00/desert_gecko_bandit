using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Collectibles
{
    /// <summary>
    /// Altın: oyuncu dokununca toplanır (+1). Seviyeye yerleştirilenler yerinde süzülür;
    /// düşmandan düşenler Launch ile fırlatılır, yere düşüp orada kalır.
    /// Arkasındaki sıcak parıltı yavaşça zonklar, arada bir yüzeyinde bir ışık gezinip çakar.
    /// Toplanınca ışınlı bir patlamayla ikiye kırılır, kırıntıları saçılıp söner (CrystalCollect), şıngırdar.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Crystal : MonoBehaviour
    {
        [SerializeField] int value = 1;
        [SerializeField] float hoverHeight = 0.08f;
        [SerializeField] float hoverSpeed = 3f;
        [Tooltip("Düşerken yerden yükseklik (sprite yarıçapı)")]
        [SerializeField] float radius = 0.25f;
        [SerializeField] float gravity = 22f;
        [Tooltip("Fırlatıldıktan sonra bu süre toplanamaz, böylece saçılması görünür")]
        [SerializeField] float pickupDelay = 0.35f;
        [SerializeField] LayerMask groundMask;
        [SerializeField] Color burstColor = new Color32(0xFF, 0xC8, 0x3C, 0xFF);

        [Header("Görsel")]
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] float pulseSpeed = 0.7f;
        [SerializeField] Vector2 glowAlpha = new Vector2(0.15f, 0.45f);
        [Tooltip("Arada bir oynayan parıltı: ışık yüzeyde gezinir ve çakar. İlk kare bekleme hali")]
        [SerializeField] Sprite[] glintFrames;
        [SerializeField] float glintFps = 10f;
        [Tooltip("İki parıltı arası (saniye, rastgele aralık)")]
        [SerializeField] Vector2 glintInterval = new Vector2(1.6f, 3.2f);
        [Tooltip("Işınlı patlama, kırılma, saçılan kırıntılar")]
        [SerializeField] Sprite[] collectFrames;
        [Tooltip("Toplanma efekti gece de parlasın: ışıktan etkilenmeyen malzeme")]
        [SerializeField] Material unlitMaterial;
        [SerializeField] AudioClip collectClip;

        public int Value => value;

        Vector3 restPosition;
        Vector2 velocity;
        bool falling;
        float pickupAllowedAt;
        float phase;
        bool collected;
        Color glowBaseColor = Color.white;
        float nextGlintAt;

        void Awake()
        {
            if (groundMask == 0)
                groundMask = GameLayers.GroundMask;
            restPosition = transform.position;
            phase = Random.value * 10f;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (glow != null)
                glowBaseColor = glow.color;
            nextGlintAt = Time.time + Random.Range(0f, glintInterval.y);
        }

        public void Launch(Vector2 launchVelocity)
        {
            velocity = launchVelocity;
            falling = true;
            pickupAllowedAt = Time.time + pickupDelay;
        }

        void Update()
        {
            if (falling)
                Fall();
            else
                transform.position = restPosition + Vector3.up * (Mathf.Sin(Time.time * hoverSpeed + phase) * hoverHeight);

            UpdateGlint();
            if (glow != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * pulseSpeed * Mathf.PI * 2f);
                Color c = glowBaseColor;
                c.a = Mathf.Lerp(glowAlpha.x, glowAlpha.y, pulse);
                glow.color = c;
            }
        }

        void Fall()
        {
            velocity.y -= gravity * Time.deltaTime;
            Vector2 step = velocity * Time.deltaTime;
            Vector2 position = transform.position;

            if (Mathf.Abs(step.x) > 0f &&
                Physics2D.Raycast(position, new Vector2(Mathf.Sign(step.x), 0f), Mathf.Abs(step.x) + radius, groundMask))
            {
                velocity.x *= -0.4f; // duvardan sek
                step.x = 0f;
            }

            if (step.y < 0f)
            {
                RaycastHit2D ground = Physics2D.Raycast(position, Vector2.down, -step.y + radius, groundMask);
                if (ground)
                {
                    restPosition = new Vector3(position.x + step.x, ground.point.y + radius, 0f);
                    transform.position = restPosition;
                    falling = false;
                    return;
                }
            }

            transform.position = position + step;
            if (transform.position.y < -30f)
                Destroy(gameObject); // uçuruma düştü
        }

        /// <summary>Çoğu zaman ilk kare; arada bir parıltı karelerini ileri sonra geri oynatır.</summary>
        void UpdateGlint()
        {
            if (spriteRenderer == null || glintFrames == null || glintFrames.Length == 0)
                return;
            int n = glintFrames.Length;
            int steps = n * 2 - 1; // 0 1 2 3 2 1 0
            int step = Mathf.FloorToInt((Time.time - nextGlintAt) * glintFps);
            if (step < 0)
            {
                spriteRenderer.sprite = glintFrames[0];
                return;
            }
            if (step >= steps)
            {
                nextGlintAt = Time.time + Random.Range(glintInterval.x, glintInterval.y);
                spriteRenderer.sprite = glintFrames[0];
                return;
            }
            spriteRenderer.sprite = glintFrames[step < n ? step : steps - 1 - step];
        }

        void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
        void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        void TryCollect(Collider2D other)
        {
            if (collected || Time.time < pickupAllowedAt)
                return;
            var wallet = other.GetComponentInParent<PlayerWallet>();
            if (wallet == null)
                return;

            collected = true;
            wallet.Add(value);
            if (collectFrames != null && collectFrames.Length > 0)
            {
                CrystalCollect.Spawn(transform.position, collectFrames,
                    spriteRenderer != null ? spriteRenderer.sortingOrder : 5, unlitMaterial);
                PixelBurst.Spawn(transform.position, burstColor, 4, 2.5f, 0.3f);
            }
            else
                PixelBurst.Spawn(transform.position, burstColor, 8, 3f, 0.35f);
            OneShotSound.Play(collectClip, 0.8f);
            Destroy(gameObject);
        }
    }
}
