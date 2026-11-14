using System.Collections;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Weapons;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Çakal Eşkıya: yavaş ve sinsi devriye gezer. Gila menzile girince irkilir, sonra oyuncudan hızlı
    /// koşarak üstüne atılır. Yakına gelince fren yapar, çiftesini doğrultur ve kısa menzilli, geniş açılı
    /// bir saçma atar; geri tepmeyle yarım adım kayar ve tüfeğini doldururken savunmasız kalır.
    /// Havada süzülen Gila'ya da namlusunu kaldırır: doğrulttuğu anda Gila'nın olduğu yere nişan kilitlenir,
    /// oyuncu doğrultma süresinde koniden çıkarak kaçabilir. Tam tepesinden süzülen Gila'ya gövdesini geriye
    /// yatırıp çifteyi yukarı kaldırır: tepesi artık güvenli değil.
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
        [Tooltip("Havadaki Gila'ya namlu kaldırabileceği en büyük açı (derece)")]
        [SerializeField] float maxAimAngle = 60f;
        [Tooltip("Havadaki Gila'ya ateş menzili (namlu hizasından)")]
        [SerializeField] float airAttackRange = 3f;
        [Tooltip("Hedef nokta: Gila'nın ayağından gövde ortasına")]
        [SerializeField] float targetHeight = 0.45f;
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

        [Header("Tepeye atış")]
        [Tooltip("Tam tepesinden (maxAimAngle'dan dik) geçen Gila'ya ateş menzili")]
        [SerializeField] float overheadRange = 6f;
        [Tooltip("Tepeye nişan alırken gövdesini geriye yatırma açısı (derece); çifte namlusu yukarı bakar")]
        [SerializeField] float overheadLean = 35f;
        [Tooltip("Tepeye doğrultma süresi: süzülen Gila'nın kaçma penceresi")]
        [SerializeField] float overheadAimDuration = 0.45f;
        [SerializeField] float overheadSpreadAngle = 20f;
        [Tooltip("Görünen saçma taneleri (yalnız görsel; hasarı koni hesabı verir). Boşsa kıvılcım öbekleri")]
        [SerializeField] Sprite pelletSprite;
        [SerializeField] int visiblePellets = 7;

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
        Vector2 aimDirection = Vector2.right;
        bool overhead; // bu atış tepeye: gövde geriye yatık, namlu yukarıda

        int lifeId; // dirilince artar: önceki ölümün yenilgi animasyonu silah bırakmasın

        bool PlayerDown => Player != null && !Player.IsAlive;
        /// <summary>Ceset savrulup uzağa düşer: pompalı, ceset yere oturunca orada çıkar.</summary>
        protected override bool DropsWeaponAfterDefeat => true;

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

            // Tepesinde süzülen Gila'yı kovalamaz (EnemyBase bırakır) ama menzildeyse yine ateş eder.
            bool inRange = Time.time >= nextAttackAt && PlayerInAttackRange();
            if (!IsChasing && !inRange)
            {
                alerted = false; // gözden kaybetti: yeniden görünce yine irkilir
                return;
            }
            if (!alerted)
            {
                alerted = true;
                routine = StartCoroutine(AlertRoutine());
            }
            else if (inRange)
            {
                routine = StartCoroutine(AttackRoutine());
            }
        }

        bool PlayerInAttackRange()
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = Player.transform.position - transform.position;
            bool ground = Mathf.Abs(toPlayer.x) <= attackRange && Mathf.Abs(toPlayer.y) <= attackHeight;
            Vector2 toTarget = PlayerTarget - Eye;
            bool air = toTarget.magnitude <= airAttackRange
                && Mathf.Atan2(Mathf.Abs(toTarget.y), Mathf.Abs(toTarget.x)) * Mathf.Rad2Deg <= maxAimAngle;
            return (ground || air || PlayerOverhead()) && !SolidBetween(Eye, PlayerTarget);
        }

        /// <summary>Gila tepesinde: normal namlu açısından dik, yukarıda ve tepe menzilinde.</summary>
        bool PlayerOverhead()
        {
            Vector2 toTarget = PlayerTarget - Eye;
            return toTarget.y > 0.5f && toTarget.magnitude <= overheadRange
                && Mathf.Atan2(toTarget.y, Mathf.Abs(toTarget.x)) * Mathf.Rad2Deg > maxAimAngle;
        }

        Vector2 Eye => (Vector2)transform.position + Vector2.up * muzzleOffset.y;

        // Tepeye atışta gövde ayaklarının üstünde geriye yatar: namlu ucu da onunla döner.
        Vector2 Muzzle
        {
            get
            {
                var offset = new Vector2(muzzleOffset.x * FacingDirection, muzzleOffset.y);
                if (overhead)
                    offset = Quaternion.Euler(0f, 0f, overheadLean * FacingDirection) * offset;
                return (Vector2)transform.position + offset;
            }
        }

        void SetLean(bool lean)
        {
            overhead = lean;
            if (spriteRenderer != null && spriteRenderer.transform != transform)
                spriteRenderer.transform.localRotation = lean ? Quaternion.Euler(0f, 0f, overheadLean * FacingDirection) : Quaternion.identity;
        }
        Vector2 PlayerTarget => (Vector2)Player.transform.position + Vector2.up * targetHeight;

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
            bool alive = Player != null && Player.IsAlive;
            // Tepedeki Gila: gövdesini geriye yatırıp çifteyi yukarı kaldırır, nişan doğrudan Gila'ya.
            SetLean(alive && PlayerOverhead());
            // Doğrulttuğu anda nişan kilitlenir: Gila doğrultma süresinde yer değiştirip koniden çıkabilir.
            if (!alive)
                aimDirection = Vector2.right * FacingDirection;
            else if (overhead)
                aimDirection = (PlayerTarget - Muzzle).normalized;
            else
                aimDirection = EnemyBullet.AimDirection(Eye, Muzzle, PlayerTarget, FacingDirection, maxAimAngle);
            yield return new WaitForSeconds(overhead ? overheadAimDuration : aimDuration);

            State = JackalState.Fire;
            FireBlast();
            if (!overhead)
                yield return Recoil(); // yukarı atınca geri tepme onu aşağı bastırır, kaymaz
            yield return new WaitForSeconds(firePoseDuration);
            SetLean(false);

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
            Vector2 muzzle = Muzzle;
            Vector2 aim = overhead || aimDirection.x * dir > 0f ? aimDirection : Vector2.right * dir; // geri dönmüşse düz atar
            float aimAngle = Mathf.Atan2(aim.y, aim.x);
            float range = overhead ? overheadRange : blastRange;
            float spread = overhead ? overheadSpreadAngle : blastSpreadAngle;
            if (pelletSprite != null)
            {
                // Saçma görseli: koninin içinde dağılan taneler, menzilin sonunda söner.
                PixelBurst.Spawn(muzzle, pelletColor, 5, 2.5f, 0.15f);
                int order = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 9;
                for (int i = 0; i < visiblePellets; i++)
                {
                    float angle = aimAngle + Random.Range(-spread, spread) * 0.85f * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    PlayerBullet.SpawnHarmless(pelletSprite, Random.Range(15f, 19f), range * Random.Range(0.85f, 1.05f),
                        transform.position + Vector3.up * muzzleOffset.y, muzzle, direction, order);
                }
            }
            else
            {
                // Saçma görseli: namludan uzaklaştıkça genişleyen üç kıvılcım öbeği.
                for (int i = 1; i <= 3; i++)
                    PixelBurst.Spawn(muzzle + aim * (range * i / 4f), pelletColor, 3 + i * 2, 1.5f + i, 0.2f);
            }

            if (Player == null || !Player.IsAlive)
                return;
            Vector2 target = PlayerTarget;
            if (InBlastCone(muzzle, target, aim, range, spread) && !SolidBetween(Eye, target))
                Player.TakeDamage(blastDamage, new Vector2(dir, 0f));
        }

        /// <summary>Nişan yönü ekseninde koni: ileri mesafe ve eksene dik sapma.</summary>
        bool InBlastCone(Vector2 muzzle, Vector2 target, Vector2 aim, float range, float spread)
        {
            Vector2 toTarget = target - muzzle;
            float forward = Vector2.Dot(toTarget, aim);
            // Namlu oyuncunun içinden geçebilir: gövdeye kadar olan yakın mesafe de vurulmuş sayılır.
            if (forward < -muzzleOffset.x || forward > range)
                return false;
            float lateral = Mathf.Abs(toTarget.x * aim.y - toTarget.y * aim.x);
            float halfWidth = 0.6f + Mathf.Max(0f, forward) * Mathf.Tan(spread * Mathf.Deg2Rad);
            return lateral <= halfWidth;
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
            SetLean(false);
        }

        // Obje kapanınca coroutine de durur; durum takılı kalırsa çakal yerinde donardı.
        void OnDisable()
        {
            routine = null;
            State = JackalState.None;
            SetLean(false);
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
            JackalDefeat defeat = JackalDefeat.Spawn(transform.position, defeatFrame, hatFrame, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, knock);
            int life = lifeId;
            defeat.Finished += position =>
            {
                if (life == lifeId) // bu arada oyuncu ölüp çakal dirildiyse eski ceset silah bırakmaz
                    DropWeapon(position);
            };
        }

        public override void ResetEnemy()
        {
            CancelRoutine();
            lifeId++;
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
            Gizmos.DrawRay(Muzzle, Vector2.right * FacingDirection * blastRange);
            float a = maxAimAngle * Mathf.Deg2Rad;
            Gizmos.DrawRay(Eye, new Vector2(Mathf.Cos(a) * FacingDirection, Mathf.Sin(a)) * airAttackRange);
        }
    }
}
