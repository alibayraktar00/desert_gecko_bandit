using System;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Gila soğukkanlıdır: gecenin ayazında vücudu soğur, üşüdükçe yavaşlar ve zıplaması kısalır. Tamamen donarsa
    /// canı azalır. Ateş, fener ve zümrüt böcekler ısıtır; yer altının sıcağında fazladan hızlanır.
    /// Sadece gece levellerinde oyuncuya eklenir.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerHealth))]
    public class BodyHeat : MonoBehaviour
    {
        [Tooltip("Tam sıcaktan donmaya kadar geçen süre")]
        [SerializeField] float secondsToFreeze = 25f;
        [Tooltip("Bu sıcaklığın altında yavaşlamaya başlar")]
        [SerializeField] float comfortHeat = 0.55f;
        [SerializeField] float coldestSpeed = 0.68f;
        [SerializeField] float coldestJump = 0.82f;
        [Tooltip("Yer altının sıcağındaki hız çarpanı")]
        [SerializeField] float hotSpeed = 1.18f;
        [Tooltip("Sıcak bölgeden çıktıktan sonra hızın sürdüğü süre")]
        [SerializeField] float hotAfterglow = 4f;
        [Tooltip("Donmuşken bu aralıkla yarım kalp gider")]
        [SerializeField] float freezeDamageInterval = 3.5f;
        [SerializeField] Color coldTint = new Color(0.62f, 0.78f, 1f);
        [SerializeField] Color hotTint = new Color(1f, 0.82f, 0.68f);

        /// <summary>0 = donmuş, 1 = sıcak.</summary>
        public float Heat { get; private set; } = 1f;
        public bool IsHot => Time.time < hotUntil;
        public bool IsFrozen => Heat <= 0f;

        /// <summary>Şafak gibi durumlar soğumayı azaltır: 0 = hiç soğumaz, 1 = tam ayaz.</summary>
        public float Coldness { get; set; } = 1f;

        public event Action Froze;

        PlayerController controller;
        PlayerHealth health;
        SpriteRenderer[] visuals;
        float warmUntil, warmRate, hotUntil, nextFreezeDamage;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            visuals = GetComponentsInChildren<SpriteRenderer>();
        }

        void OnEnable() => health.ReturnedToCheckpoint += Restore;
        void OnDisable()
        {
            health.ReturnedToCheckpoint -= Restore;
            controller.SpeedMultiplier = 1f;
            controller.JumpMultiplier = 1f;
        }

        /// <summary>Isı kaynağı her fizik adımında çağırır: bu hızla ısınır (saniyede ısı).</summary>
        public void WarmAt(float heatPerSecond)
        {
            warmRate = Mathf.Max(warmRate, heatPerSecond);
            warmUntil = Time.time + Time.fixedDeltaTime * 1.5f;
        }

        /// <summary>Tek seferlik ısı: yutulan zümrüt böcek gibi.</summary>
        public void AddHeat(float amount) => Heat = Mathf.Clamp01(Heat + amount);

        /// <summary>Yer altının sıcak bölgesi çağırır: ısıyı doldurur, fazladan hız verir.</summary>
        public void Overheat()
        {
            Heat = 1f;
            hotUntil = Time.time + hotAfterglow;
        }

        void Restore()
        {
            Heat = 1f;
            hotUntil = 0f;
        }

        void FixedUpdate()
        {
            if (Time.time < warmUntil)
                Heat = Mathf.Min(1f, Heat + warmRate * Time.fixedDeltaTime);
            else
            {
                warmRate = 0f;
                Heat = Mathf.Max(0f, Heat - Coldness * Time.fixedDeltaTime / secondsToFreeze);
            }

            float comfort = Mathf.Clamp01(Heat / comfortHeat);
            controller.SpeedMultiplier = IsHot ? hotSpeed : Mathf.Lerp(coldestSpeed, 1f, comfort);
            controller.JumpMultiplier = Mathf.Lerp(coldestJump, 1f, comfort);

            if (!IsFrozen || !health.IsAlive)
            {
                nextFreezeDamage = Time.time + freezeDamageInterval * 0.5f; // donar donmaz değil, kısa bir uyarı süresi sonra
                return;
            }
            if (Time.time >= nextFreezeDamage)
            {
                nextFreezeDamage = Time.time + freezeDamageInterval;
                Froze?.Invoke();
                health.LoseHealthQuietly(1);
            }
        }

        // Üşüyen Gila maviye, sıcakta kalan kızıla döner: göstergeye bakmadan anlaşılsın.
        void LateUpdate()
        {
            Color tint = IsHot ? hotTint : Color.Lerp(coldTint, Color.white, Mathf.Clamp01(Heat / comfortHeat));
            foreach (SpriteRenderer sr in visuals)
                if (sr != null && sr.gameObject.name != "Dil")
                    sr.color = new Color(tint.r, tint.g, tint.b, sr.color.a);
        }
    }
}
