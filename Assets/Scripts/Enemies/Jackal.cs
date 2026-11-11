using System.Collections;
using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Çakal Eşkıya: yavaş ve sinsi devriye gezer. Gila menzile girince irkilir, sonra oyuncudan hızlı
    /// koşarak üstüne atılır. Yakına gelince fren yapar, çiftesini doğrultur ve kısa menzilli, geniş açılı
    /// bir saçma atar; geri tepmeyle yarım adım kayar ve tüfeğini doldururken savunmasız kalır.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar.
    /// </summary>
    public class Jackal : EnemyBase
    {
        public enum JackalState { None, Alert, Aim, Fire, Reload }

        [Header("Fark etme")]
        [SerializeField] float alertDuration = 0.45f;

        [Header("Saçma atışı")]
        [SerializeField] float attackRange = 2.6f;
        [Tooltip("Bundan daha yukarıda/aşağıdaki oyuncuya nişan almaz")]
        [SerializeField] float attackHeight = 1.2f;
        [Tooltip("Gövdeye göre namlu ucu, sağa bakarken")]
        [SerializeField] Vector2 muzzleOffset = new Vector2(1f, 0.8f);
        [Tooltip("Doğrultma süresi: oyuncunun zıplayıp kaçma penceresi")]
        [SerializeField] float aimDuration = 0.35f;
        [SerializeField] float firePoseDuration = 0.15f;
        [SerializeField] float reloadDuration = 1f;
        [SerializeField] float attackCooldown = 0.4f;
        [SerializeField] int blastDamage = 2;
        [SerializeField] float blastRange = 3.2f;
        [Tooltip("Saçmanın namlu ucundan itibaren yayılma açısı (derece, tek yön)")]
        [SerializeField] float blastSpreadAngle = 30f;
        [SerializeField] float recoilDistance = 0.4f;
        [SerializeField] Color pelletColor = new Color32(0xFF, 0xD0, 0x50, 0xFF);

        [Header("Hasar")]
        [SerializeField] float hurtDuration = 0.3f;

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip shotClip;

        [Header("Kareler")]
        [SerializeField] Sprite[] patrolFrames;
        [SerializeField] float patrolFps = 5f;
        [SerializeField] Sprite alertFrame;
        [SerializeField] Sprite[] runFrames;
        [SerializeField] float runFps = 14f;
        [SerializeField] Sprite aimFrame;
        [SerializeField] Sprite fireFrame;
        [SerializeField] Sprite[] reloadFrames;
        [SerializeField] Sprite hurtFrame;
        [SerializeField] Sprite[] victoryFrames;
        [SerializeField] float victoryFps = 5f;
        [SerializeField] Sprite defeatFrame;
        [SerializeField] Sprite hatFrame;

        public JackalState State { get; private set; }

        readonly RaycastHit2D[] sightHits = new RaycastHit2D[4];
        ContactFilter2D solidFilter;
        Coroutine routine;
        bool alerted;
        float nextAttackAt;
        float hurtUntil;
        float reloadStartedAt;

        bool PlayerDown => Player != null && !Player.IsAlive;

        protected override bool HoldPosition => State != JackalState.None || PlayerDown;

        protected override void Awake()
        {
            base.Awake();
            solidFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | LayerMask.GetMask("Default"),
                useTriggers = false,
            };
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled || State != JackalState.None || Time.time < hurtUntil || !IsGrounded())
                return;

            if (!IsChasing)
            {
                alerted = false; // gözden kaybetti: yeniden görünce yine irkilir
                return;
            }
            if (!alerted)
            {
                alerted = true;
                routine = StartCoroutine(AlertRoutine());
            }
            else if (Time.time >= nextAttackAt && PlayerInAttackRange())
            {
                routine = StartCoroutine(AttackRoutine());
            }
        }

        bool PlayerInAttackRange()
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = Player.transform.position - transform.position;
            return Mathf.Abs(toPlayer.x) <= attackRange && Mathf.Abs(toPlayer.y) <= attackHeight;
        }

        int DirectionToPlayer => Player.transform.position.x >= transform.position.x ? 1 : -1;

        IEnumerator AlertRoutine()
        {
            State = JackalState.Alert;
            TurnTowards(DirectionToPlayer);
            yield return new WaitForSeconds(alertDuration);
            State = JackalState.None;
            routine = null;
        }

        IEnumerator AttackRoutine()
        {
            State = JackalState.Aim;
            TurnTowards(DirectionToPlayer);
            yield return new WaitForSeconds(aimDuration);

            State = JackalState.Fire;
            FireBlast();
            yield return Recoil();
            yield return new WaitForSeconds(firePoseDuration);

            State = JackalState.Reload;
            reloadStartedAt = Time.time;
            yield return new WaitForSeconds(reloadDuration);

            State = JackalState.None;
            routine = null;
            nextAttackAt = Time.time + attackCooldown;
        }

        void FireBlast()
        {
            if (audioSource != null && shotClip != null)
                audioSource.PlayOneShot(shotClip);

            int dir = FacingDirection;
            Vector2 muzzle = (Vector2)transform.position + new Vector2(muzzleOffset.x * dir, muzzleOffset.y);
            // Saçma görseli: namludan uzaklaştıkça genişleyen üç kıvılcım öbeği.
            for (int i = 1; i <= 3; i++)
                PixelBurst.Spawn(muzzle + Vector2.right * (dir * blastRange * i / 4f), pelletColor, 3 + i * 2, 1.5f + i, 0.2f);

            if (Player == null || !Player.IsAlive)
                return;
            Vector2 target = (Vector2)Player.transform.position + Vector2.up * 0.45f;
            if (InBlastCone(muzzle, target, dir) && !SolidBetween(transform.position + Vector3.up * muzzleOffset.y, target))
                Player.TakeDamage(blastDamage, new Vector2(dir, 0f));
        }

        bool InBlastCone(Vector2 muzzle, Vector2 target, int dir)
        {
            Vector2 toTarget = target - muzzle;
            float forward = toTarget.x * dir;
            // Namlu oyuncunun içinden geçebilir: gövdeye kadar olan yakın mesafe de vurulmuş sayılır.
            if (forward < -muzzleOffset.x || forward > blastRange)
                return false;
            float halfWidth = 0.6f + Mathf.Max(0f, forward) * Mathf.Tan(blastSpreadAngle * Mathf.Deg2Rad);
            return Mathf.Abs(toTarget.y) <= halfWidth;
        }

        bool SolidBetween(Vector2 from, Vector2 to)
        {
            Vector2 path = to - from;
            int count = Physics2D.Raycast(from, path.normalized, solidFilter, sightHits, path.magnitude);
            for (int i = 0; i < count; i++)
                if (!sightHits[i].collider.usedByEffector)
                    return true;
            return false;
        }

        /// <summary>Geri tepme: yarım adım geriye kayar, uçuruma ya da duvara girmez.</summary>
        IEnumerator Recoil()
        {
            const int steps = 5;
            int back = -FacingDirection;
            Bounds b = box.bounds;
            Vector2 foot = new Vector2(b.center.x + back * (b.extents.x + recoilDistance), b.min.y + 0.1f);
            bool canSlide = Physics2D.Raycast(foot, Vector2.down, 0.6f, GameLayers.GroundMask)
                && !Physics2D.Raycast(b.center, Vector2.right * back, b.extents.x + recoilDistance, GameLayers.GroundMask);
            for (int i = 0; canSlide && i < steps; i++)
            {
                body.position += Vector2.right * (back * recoilDistance / steps);
                yield return new WaitForFixedUpdate();
            }
        }

        void CancelRoutine()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            State = JackalState.None;
        }

        // Obje kapanınca coroutine de durur; durum takılı kalırsa çakal yerinde donardı.
        void OnDisable()
        {
            routine = null;
            State = JackalState.None;
        }

        protected override void OnDamaged()
        {
            // Vurulunca nişanı bozulur; irkilmeden saldırana döner.
            CancelRoutine();
            alerted = true;
            hurtUntil = Time.time + hurtDuration;
            // Beklemesi sıfırlanır: yakındaysa sersemliği biter bitmez saçmayı patlatır.
            nextAttackAt = Time.time + hurtDuration;
            if (Player != null)
                TurnTowards(DirectionToPlayer);
        }

        protected override void OnDied()
        {
            CancelRoutine();
            int knock = Player != null ? -DirectionToPlayer : -FacingDirection;
            JackalDefeat.Spawn(transform.position, defeatFrame, hatFrame, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, knock);
        }

        public override void ResetEnemy()
        {
            CancelRoutine();
            base.ResetEnemy();
            alerted = false;
            nextAttackAt = 0f;
            hurtUntil = 0f;
        }

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;
        }

        Sprite CurrentFrame()
        {
            if (PlayerDown)
                return Loop(victoryFrames, victoryFps);
            if (Time.time < hurtUntil)
                return hurtFrame;
            switch (State)
            {
                case JackalState.Alert: return alertFrame;
                case JackalState.Aim: return aimFrame;
                case JackalState.Fire: return fireFrame;
                case JackalState.Reload:
                    if (reloadFrames == null || reloadFrames.Length == 0)
                        return aimFrame;
                    int i = Mathf.FloorToInt((Time.time - reloadStartedAt) / reloadDuration * reloadFrames.Length);
                    return reloadFrames[Mathf.Clamp(i, 0, reloadFrames.Length - 1)];
                default:
                    // Kenarda ya da duvarda EnemyBase onu durdurur; yerinde koşmasın.
                    // Kovalarken kenarda bekliyorsa çiftesi Gila'ya doğrultulu, hazır bekler.
                    bool moving = Mathf.Abs(body.linearVelocity.x) > 0.1f;
                    if (IsChasing)
                        return moving ? Loop(runFrames, runFps) : aimFrame;
                    return moving ? Loop(patrolFrames, patrolFps) : (patrolFrames != null && patrolFrames.Length > 0 ? patrolFrames[0] : null);
            }
        }

        static Sprite Loop(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0)
                return null;
            return frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Vector2 muzzle = (Vector2)transform.position + new Vector2(muzzleOffset.x * FacingDirection, muzzleOffset.y);
            Gizmos.DrawRay(muzzle, Vector2.right * FacingDirection * blastRange);
        }
    }
}
