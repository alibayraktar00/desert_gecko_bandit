using System;
using DesertGecko.Core;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Uzak mesafe kuyruk fırlatma (L). Silah eldeyken de çalışır: silahın ateş tuşu ayrı (K). Kuyruk fırlatılınca karakterden kopar, kısa süre sonra kalçadan ince
    /// çıkar ve bekleme boyunca yavaş yavaş büyür. Tamamen çıkana kadar HasTail false: çift zıplama zayıflar, süzülme kapanır.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader), typeof(PlayerHealth))]
    public class TailAttack : MonoBehaviour
    {
        [SerializeField] TailProjectile projectilePrefab;
        [Tooltip("Koptuktan sonra yeni kuyruk çıkmaya başlayana kadarki süre")]
        [SerializeField] float cooldown = 0.8f;
        [Tooltip("İnce başlayıp eski haline dönene kadarki süre")]
        [SerializeField] float regrowDuration = 9.7f;
        [Tooltip("Hafif yukarı atış + düşük yerçekimi: yerdeki düşmana isabet eder, ~8 birim menzil")]
        [SerializeField] Vector2 launchVelocity = new Vector2(13f, 0.8f);
        [SerializeField] Vector2 spawnOffset = new Vector2(0.4f, 0.4f);

        /// <summary>0 = yeni fırlatıldı, 1 = kuyruk hazır. Arayüzdeki çember bunu gösterir.</summary>
        public float CooldownProgress => controller.HasTail ? 1f : Mathf.Clamp01(1f - (readyAt - Time.time) / (cooldown + regrowDuration));
        public bool IsReady => controller.HasTail;

        /// <summary>Kuyruğun görünen boyu: 0 = yok, 1 = tam. PlayerVisual karesini buna göre seçer.</summary>
        public float TailGrowth => controller == null || controller.HasTail ? 1f
            : regrowDuration <= 0f ? 0f
            : Mathf.Clamp01(1f - (readyAt - Time.time) / regrowDuration);

        public event Action Thrown;
        public event Action TailRegrown;

        PlayerController controller;
        PlayerInputReader input;
        PlayerHealth health;
        PlayerWeapon weapon;
        float readyAt;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<PlayerHealth>();
            weapon = GetComponent<PlayerWeapon>();
        }

        void OnEnable() => health.Respawned += RegrowTail;
        void OnDisable() => health.Respawned -= RegrowTail;

        void Update()
        {
            // Kuyruk kendi tuşunda (L): silah eldeyken de fırlatılır.
            if (input.ConsumeTail())
                TryThrow();

            if (!controller.HasTail && Time.time >= readyAt)
                RegrowTail();
        }

        /// <returns>Kuyruk fırlatıldıysa true.</returns>
        public bool TryThrow()
        {
            if (projectilePrefab == null || !controller.HasTail || controller.MovementLocked || !health.IsAlive)
                return false;

            // Duvara yapışıkken yüz duvara dönük; kuyruğu duvardan uzağa at.
            int direction = controller.State == PlayerController.MoveState.WallCling
                ? -controller.FacingDirection
                : controller.FacingDirection;

            Vector2 spawn = (Vector2)transform.position + new Vector2(spawnOffset.x * direction, spawnOffset.y);

            // Duvara dayalıyken mermi duvarın içinde doğup hemen kırılırdı; kuyruğu boşa harcama.
            Vector2 body = (Vector2)transform.position + Vector2.up * spawnOffset.y;
            if (Physics2D.Linecast(body, spawn + new Vector2(direction * 0.25f, 0f), GameLayers.GroundMask))
                return false;

            TailProjectile projectile = Instantiate(projectilePrefab, spawn, Quaternion.identity);
            projectile.Launch(new Vector2(launchVelocity.x * direction, launchVelocity.y));

            controller.HasTail = false;
            readyAt = Time.time + cooldown + regrowDuration;
            Thrown?.Invoke();
            return true;
        }

        void RegrowTail()
        {
            if (controller.HasTail)
                return;
            controller.HasTail = true;
            readyAt = 0f;
            TailRegrown?.Invoke();
        }
    }
}
