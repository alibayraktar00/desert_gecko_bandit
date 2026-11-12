using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Tavandaki sarmaşıktan sarkan diken kozası. Kendi saatine göre titrer, açılıp dikenli top olarak düşer,
    /// değdiği oyuncunun bir kalbini götürür, lava düşünce sıçratıp kaybolur ve yeniden büyür.
    /// Düşüşler düzenli: sallanan oyuncu ritmi görüp halkadan halkaya geçişini ona göre ayarlar.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ThornPod : MonoBehaviour
    {
        enum Phase { Growing, Hanging, Shaking, Falling, Gone }

        [SerializeField] Sprite hangingSprite;
        [SerializeField] Sprite fallingSprite;
        [SerializeField] SpriteRenderer splash;
        [SerializeField] float cycle = 3.2f;
        [SerializeField] float phaseOffset;
        [SerializeField] float shakeTime = 0.55f;
        [SerializeField] float shakeAmount = 0.06f;
        [SerializeField] float gravity = 22f;
        [SerializeField] float maxFallSpeed = 14f;
        [SerializeField] float growTime = 0.6f;
        [SerializeField] float floorY = -1f;
        [SerializeField] int damage = 2;
        [SerializeField] float hitRadius = 0.5f;
        [SerializeField] Color burstColor = new Color32(0xF5, 0xB0, 0x10, 0xFF);

        SpriteRenderer sr;
        Vector3 home;
        Phase phase;
        float phaseStarted, speed, splashUntil;
        bool hitPlayer;
        readonly Collider2D[] hits = new Collider2D[1];

        public void Configure(Sprite hanging, Sprite falling, SpriteRenderer splashRenderer, float cycleTime, float offset, float lavaY)
        {
            hangingSprite = hanging;
            fallingSprite = falling;
            splash = splashRenderer;
            cycle = cycleTime;
            phaseOffset = offset;
            floorY = lavaY;
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            home = transform.position;
            if (splash != null)
                splash.enabled = false;
            PlayerHealth.PlayerRespawned += Regrow;
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Regrow;
        }

        void Start() => Regrow();

        // Düzenli saat: her tur, turun sonuna denk gelen anda düşer. Böylece yeniden doğunca da ritim aynı kalır.
        float CycleTime => Mathf.Repeat(Time.time + phaseOffset, cycle);

        void Update()
        {
            if (splash != null && splash.enabled && Time.time >= splashUntil)
                splash.enabled = false;

            switch (phase)
            {
                case Phase.Growing:
                    float t = Mathf.Clamp01((Time.time - phaseStarted) / growTime);
                    transform.localScale = Vector3.one * Mathf.SmoothStep(0.2f, 1f, t);
                    if (t >= 1f)
                        phase = Phase.Hanging;
                    break;
                case Phase.Hanging:
                    // Hafif sallanma; düşüşe shakeTime kala titremeye geçer.
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2.2f + phaseOffset) * 6f);
                    if (CycleTime >= cycle - shakeTime)
                    {
                        phase = Phase.Shaking;
                        phaseStarted = Time.time;
                    }
                    break;
                case Phase.Shaking:
                    transform.position = home + Vector3.right * (Mathf.Sin(Time.time * 55f) * shakeAmount);
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 40f) * 12f);
                    if (Time.time - phaseStarted >= shakeTime)
                        Drop();
                    break;
                case Phase.Falling:
                    Fall();
                    break;
                case Phase.Gone:
                    // Bir tur boş kalır, sonra yeniden büyür: aynı saatte tekrar düşer.
                    if (CycleTime < cycle - shakeTime - growTime && Time.time - phaseStarted > growTime)
                        Regrow();
                    break;
            }
        }

        void Drop()
        {
            transform.position = home;
            transform.rotation = Quaternion.identity; // görsel tepesinden pivotlu: dönerse yörüngede savrulur
            sr.sprite = fallingSprite;
            phase = Phase.Falling;
            speed = 0f;
            hitPlayer = false;
            PixelBurst.Spawn(home, new Color32(0x5E, 0x6E, 0x2C, 0xFF), 5, 2f, 0.35f); // koptuğu saptan yaprak
        }

        void Fall()
        {
            speed = Mathf.Min(maxFallSpeed, speed + gravity * Time.deltaTime);
            transform.position += Vector3.down * (speed * Time.deltaTime);

            Vector2 center = sr.bounds.center;
            if (!hitPlayer)
            {
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.PlayerMask, useTriggers = false };
                if (Physics2D.OverlapCircle(center, hitRadius, filter, hits) > 0)
                {
                    var health = hits[0].GetComponentInParent<PlayerHealth>();
                    if (health != null)
                    {
                        hitPlayer = true;
                        health.TakeDamage(new DamageInfo(damage, new Vector2(health.transform.position.x >= center.x ? 1f : -1f, 0f), DamageKind.Hazard));
                        PixelBurst.Spawn(center, new Color32(0xC8, 0x5A, 0x28, 0xFF), 8, 4f, 0.4f);
                    }
                }
            }

            if (center.y <= floorY || Physics2D.OverlapCircle(center, hitRadius * 0.6f, GameLayers.GroundMask))
                Vanish(center);
        }

        void Vanish(Vector2 at)
        {
            sr.enabled = false;
            phase = Phase.Gone;
            phaseStarted = Time.time;
            bool inLava = at.y <= floorY + 0.5f;
            PixelBurst.Spawn(new Vector2(at.x, inLava ? floorY : at.y), inLava ? burstColor : new Color32(0x6E, 0x44, 0x30, 0xFF), 10, 4.5f, 0.5f);
            if (inLava && splash != null)
            {
                splash.transform.position = new Vector3(at.x, floorY - 0.3f, 0f);
                splash.enabled = true;
                splashUntil = Time.time + 0.35f;
            }
        }

        void Regrow()
        {
            transform.position = home;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * 0.2f;
            sr.sprite = hangingSprite;
            sr.enabled = true;
            phase = Phase.Growing;
            phaseStarted = Time.time;
        }
    }
}
