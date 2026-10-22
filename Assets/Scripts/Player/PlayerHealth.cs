using System;
using System.Collections;
using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Oyuncunun canı: 3 kalp = 6 birim (yarım kalp = 1). Hasar alınca geri itilir ve kısa süre
    /// yanıp sönerek hasar almaz. Can bitince son checkpoint'te yeniden doğar, düşmanlar sıfırlanır.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] int maxHealth = 6;

        [Header("Hasar tepkisi")]
        [SerializeField] float invincibleDuration = 1.5f;
        [SerializeField] Vector2 knockbackVelocity = new Vector2(6f, 7f);
        [SerializeField] float knockbackControlLock = 0.25f;
        [SerializeField] float blinkInterval = 0.08f;
        [Tooltip("Yanıp sönecek görsel; boşsa alt objelerdeki ilk SpriteRenderer")]
        [SerializeField] SpriteRenderer blinkRenderer;

        [Header("Ölüm")]
        [SerializeField] float respawnDelay = 1f;
        [SerializeField] float respawnInvincibility = 1f;
        [SerializeField] Color deathBurstColor = new Color32(0xE8, 0xA0, 0x4A, 0xFF);

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public bool IsInvincible => Time.time < invincibleUntil;
        public Vector2 CheckpointPosition { get; private set; }

        public event Action<int> Damaged;
        public event Action<int> Healed;
        public event Action Died;
        public event Action Respawned;

        /// <summary>Oyuncu öldükten sonra yeniden doğunca. Düşmanlar buna bakıp kendini sıfırlar.</summary>
        public static event Action PlayerRespawned;

        PlayerController controller;
        float invincibleUntil;
        Coroutine blinkRoutine;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (blinkRenderer == null)
                blinkRenderer = GetComponentInChildren<SpriteRenderer>();
            CurrentHealth = maxHealth;
            CheckpointPosition = transform.position;
        }

        public void SetCheckpoint(Vector2 position) => CheckpointPosition = position;

        public void TakeDamage(int amount, Vector2 knockbackDirection)
        {
            if (!IsAlive || IsInvincible || amount <= 0)
                return;

            if (LoseHealth(amount))
                return;

            float side = Mathf.Abs(knockbackDirection.x) > 0.01f ? Mathf.Sign(knockbackDirection.x) : -controller.FacingDirection;
            controller.Knockback(new Vector2(side * knockbackVelocity.x, knockbackVelocity.y), knockbackControlLock);
            StartInvincibility(invincibleDuration);
        }

        /// <summary>Uçuruma düşünce: hasar al, ölmediysen son checkpoint'e dön.</summary>
        public void FallIntoPit(int damage)
        {
            if (!IsAlive)
                return;

            if (damage > 0 && LoseHealth(damage))
                return;

            controller.Teleport(CheckpointPosition);
            StartInvincibility(invincibleDuration);
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0 || CurrentHealth >= maxHealth)
                return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            Healed?.Invoke(CurrentHealth);
        }

        /// <returns>Oyuncu bu hasarla öldüyse true.</returns>
        bool LoseHealth(int amount)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Damaged?.Invoke(CurrentHealth);
            if (CurrentHealth > 0)
                return false;

            StartCoroutine(DieRoutine());
            return true;
        }

        IEnumerator DieRoutine()
        {
            Died?.Invoke();
            StopBlink();
            controller.MovementLocked = true;
            controller.Frozen = true;
            if (blinkRenderer != null)
                blinkRenderer.enabled = false;
            PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.5f, deathBurstColor, 16, 5f, 0.7f);

            yield return new WaitForSeconds(respawnDelay);

            controller.Frozen = false;
            controller.Teleport(CheckpointPosition);
            controller.MovementLocked = false;
            CurrentHealth = maxHealth;
            if (blinkRenderer != null)
                blinkRenderer.enabled = true;
            StartInvincibility(respawnInvincibility);

            Respawned?.Invoke();
            PlayerRespawned?.Invoke();
        }

        void StartInvincibility(float duration)
        {
            invincibleUntil = Time.time + duration;
            StopBlink();
            blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        void StopBlink()
        {
            if (blinkRoutine != null)
                StopCoroutine(blinkRoutine);
            blinkRoutine = null;
            if (blinkRenderer != null && IsAlive)
                blinkRenderer.enabled = true;
        }

        IEnumerator BlinkRoutine()
        {
            while (IsInvincible && IsAlive)
            {
                if (blinkRenderer != null)
                    blinkRenderer.enabled = !blinkRenderer.enabled;
                yield return new WaitForSeconds(blinkInterval);
            }
            if (blinkRenderer != null && IsAlive)
                blinkRenderer.enabled = true;
            blinkRoutine = null;
        }
    }
}
