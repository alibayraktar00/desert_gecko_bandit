using System;
using System.Collections;
using System.Collections.Generic;
using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Environment;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Yakın mesafe dil saldırısı (J). Önündeki alanı tarar: düşmana hasar verir, böceği yakalayıp
    /// can yeniler. Yukarı/aşağı basılıysa o yöne, değilse baktığı yöne uzanır.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader), typeof(PlayerHealth))]
    public class TongueAttack : MonoBehaviour
    {
        [SerializeField] float range = 2.5f;
        [Tooltip("Vuruş alanının kalınlığı; yerdeki alçak düşmanları da kapsasın diye geniş tutuldu")]
        [SerializeField] float width = 1f;
        [SerializeField] int damage = 1;
        [SerializeField] float cooldown = 0.3f;
        [Tooltip("Saldırı boyunca hareket kilitli kalır; dil bu sürede uzayıp geri çekilir")]
        [SerializeField] float lockDuration = 0.2f;
        [SerializeField] float extendTime = 0.06f;
        [SerializeField] Vector2 mouthOffset = new Vector2(0.3f, 0.7f);
        [SerializeField] LayerMask hitMask;
        [Tooltip("Pivotu sol kenarda, 1 birim genişliğinde dil sprite'ı")]
        [SerializeField] SpriteRenderer tongueRenderer;

        [Header("Kancaya yapışma")]
        [SerializeField] float hookRange = 7f;
        [Tooltip("Dil, oyuncuyu kancanın bu kadar altına çeker")]
        [SerializeField] float hookRopeLength = 3.6f;

        /// <summary>J'ye basılırsa yapışılacak kanca; yoksa null.</summary>
        public SwingPoint HookInReach { get; private set; }

        public bool IsAttacking { get; private set; }
        public event Action Attacked;

        PlayerController controller;
        PlayerInputReader input;
        PlayerHealth health;
        float readyAt;
        bool hookShown;
        bool holdHookOverride; // testler tuş basmadan kancada tutabilsin

        /// <summary>Test ve bot için: tuş basılı değilken de kancada kalsın.</summary>
        public void HoldHook(bool hold) => holdHookOverride = hold;

        readonly HashSet<Component> alreadyHit = new HashSet<Component>();
        readonly Collider2D[] hits = new Collider2D[16];

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<PlayerHealth>();
            if (hitMask == 0)
                hitMask = LayerMask.GetMask("Default", GameLayers.Enemy);
            if (tongueRenderer != null)
                tongueRenderer.enabled = false;
        }

        void Update()
        {
            UpdateHookTarget();

            if (input.ConsumeTongue())
            {
                if (!TryLatch())
                    TryAttack();
            }

            if (controller.IsSwinging && !input.TongueHeld && !holdHookOverride)
                controller.EndSwing();
        }

        void LateUpdate()
        {
            if (!controller.IsSwinging || controller.SwingAnchor == null)
            {
                if (hookShown && !IsAttacking && tongueRenderer != null)
                    tongueRenderer.enabled = false;
                hookShown = false;
                return;
            }
            Vector2 mouth = MouthPosition();
            Vector2 delta = (Vector2)controller.SwingAnchor.position - mouth;
            hookShown = true;
            ShowTongue(mouth, delta.normalized, delta.magnitude);
        }

        void UpdateHookTarget()
        {
            bool canReach = !controller.IsSwinging && !controller.MovementLocked && health.IsAlive;
            SwingPoint next = canReach ? SwingPoint.FindBest(MouthPosition(), AttackSide(), hookRange, GameLayers.GroundMask) : null;
            if (HookInReach != null && HookInReach != next)
                HookInReach.SetTargeted(false);
            HookInReach = next;
            if (next != null)
                next.SetTargeted(true);
        }

        /// <returns>Kancaya yapıştıysa true. Dilin menzilinde düşman ya da böcek varsa önce onlar vurulur.</returns>
        public bool TryLatch()
        {
            if (HookInReach == null || IsAttacking || controller.MovementLocked || !health.IsAlive || controller.IsSwinging)
                return false;
            if (TargetInTongueReach())
                return false;
            controller.BeginSwing(HookInReach.transform, hookRopeLength);
            HookInReach.SetTargeted(false);
            HookInReach = null;
            Attacked?.Invoke();
            return true;
        }

        bool TargetInTongueReach()
        {
            Vector2 direction = new Vector2(AttackSide(), 0f);
            Vector2 center = MouthPosition() + direction * (range * 0.5f);
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitMask, useTriggers = true };
            int count = Physics2D.OverlapBox(center, new Vector2(range, width), 0f, filter, hits);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].GetComponentInParent<Bug>() != null)
                    return true;
                var target = hits[i].GetComponentInParent<IDamageable>();
                if (target != null && !(target is PlayerHealth) && target.IsAlive)
                    return true;
            }
            return false;
        }

        // Saldırı ortasında obje kapanırsa coroutine durur; kilit takılı kalmasın.
        void OnDisable()
        {
            if (!IsAttacking)
                return;
            IsAttacking = false;
            if (tongueRenderer != null)
                tongueRenderer.enabled = false;
            if (health.IsAlive)
                controller.MovementLocked = false;
        }

        /// <returns>Saldırı başladıysa true (bekleme süresi, ölüm veya kilit yoksa).</returns>
        public bool TryAttack()
        {
            if (IsAttacking || Time.time < readyAt || controller.MovementLocked || !health.IsAlive)
                return false;
            StartCoroutine(AttackRoutine(AimDirection()));
            return true;
        }

        Vector2 AimDirection()
        {
            float y = input.Move.y;
            if (y > 0.5f) return Vector2.up;
            if (y < -0.5f) return Vector2.down;
            return new Vector2(AttackSide(), 0f);
        }

        // Duvara yapışıkken yüz duvara dönük; dil duvardan uzağa uzansın (TailAttack ile aynı).
        int AttackSide() => controller.State == PlayerController.MoveState.WallCling
            ? -controller.FacingDirection
            : controller.FacingDirection;

        Vector2 MouthPosition() =>
            (Vector2)transform.position + new Vector2(mouthOffset.x * AttackSide(), mouthOffset.y);

        IEnumerator AttackRoutine(Vector2 direction)
        {
            IsAttacking = true;
            readyAt = Time.time + cooldown;
            controller.MovementLocked = true;
            Attacked?.Invoke();

            HitTargets(MouthPosition(), direction);

            // Görsel: hızlıca uzan, kısa dur, geri çekil.
            float t = 0f;
            float retractStart = extendTime * 2f;
            while (t < lockDuration)
            {
                t += Time.deltaTime;
                float extension = t < extendTime ? t / extendTime
                    : t < retractStart ? 1f
                    : 1f - (t - retractStart) / Mathf.Max(0.01f, lockDuration - retractStart);
                ShowTongue(MouthPosition(), direction, range * Mathf.Clamp01(extension));
                yield return null;
            }

            if (tongueRenderer != null)
                tongueRenderer.enabled = false;
            if (health.IsAlive) // ölüm sırasında kilidi PlayerHealth yönetir
                controller.MovementLocked = false;
            IsAttacking = false;
        }

        void HitTargets(Vector2 origin, Vector2 direction)
        {
            bool vertical = Mathf.Abs(direction.y) > 0.5f;
            Vector2 center = origin + direction * (range * 0.5f);
            Vector2 size = vertical ? new Vector2(width, range) : new Vector2(range, width);

            alreadyHit.Clear();
            // Böcekler trigger; global "queriesHitTriggers" ayarından bağımsız olarak bulunsun.
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitMask, useTriggers = true };
            int count = Physics2D.OverlapBox(center, size, 0f, filter, hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                var bug = hit.GetComponentInParent<Bug>();
                if (bug != null)
                {
                    if (alreadyHit.Add(bug))
                        bug.Catch(health);
                    continue;
                }

                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || target is PlayerHealth || !target.IsAlive || !alreadyHit.Add((Component)target))
                    continue;

                // Yukarı/aşağı vuruşta düşman oyuncudan uzağa doğru itilsin.
                Vector2 push = vertical
                    ? new Vector2(Mathf.Sign(hit.transform.position.x - transform.position.x), 0f)
                    : direction;
                target.TakeDamage(damage, push);
            }
        }

        void ShowTongue(Vector2 origin, Vector2 direction, float length)
        {
            if (tongueRenderer == null)
                return;
            tongueRenderer.enabled = length > 0.02f;
            Transform t = tongueRenderer.transform;
            t.position = origin;
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(length, 1f, 1f);
        }

        void OnDrawGizmosSelected()
        {
            var pc = GetComponent<PlayerController>();
            int facing = pc != null && Application.isPlaying ? pc.FacingDirection : 1;
            Vector2 origin = (Vector2)transform.position + new Vector2(mouthOffset.x * facing, mouthOffset.y);
            Gizmos.color = new Color(1f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(origin + new Vector2(facing * range * 0.5f, 0f), new Vector3(range, width));
        }
    }
}
