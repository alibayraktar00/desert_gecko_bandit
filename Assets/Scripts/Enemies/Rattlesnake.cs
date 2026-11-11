using System.Collections;
using DesertGecko.Core;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Çıngıraklı Yılan Silahşor: sağa sola devriye gezer, kovalamaz. Önündeki görüş konisinde Gila'yı
    /// (yerde ya da havada süzülürken) görünce durur, çıngırağını titretir (uyarı + ses), sonra ateş eder.
    /// Mermi, tetiğe basıldığı anda Gila'nın bulunduğu noktaya gider: oyuncu yer değiştirerek kaçabilir.
    /// Her vuruluşta çıngırağıyla haber verip karşılık ateşi açar.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar, namlu ve mermi de yön değiştirir.
    /// </summary>
    public class Rattlesnake : EnemyBase
    {
        public enum AttackState { None, Rattle, Aim, Shoot, Recover }

        [Header("Görüş")]
        [SerializeField] float sightRange = 9f;
        [Tooltip("Nişan alınabilen en büyük açı (yatayın üstü/altı, derece)")]
        [SerializeField] float maxAimAngle = 65f;
        [Tooltip("Hedef nokta: Gila'nın ayağından gövde ortasına")]
        [SerializeField] float targetHeight = 0.45f;

        [Header("Atış")]
        [SerializeField] EnemyBullet bulletPrefab;
        [Tooltip("Gövdeye göre namlu ucu, sağa bakarken (ateş karesindeki alev)")]
        [SerializeField] Vector2 muzzleOffset = new Vector2(0.66f, 0.54f);
        [Tooltip("Çıngırak uyarısı: oyuncunun tepki penceresi")]
        [SerializeField] float telegraphDuration = 0.6f;
        [SerializeField] float aimDuration = 0.15f;
        [SerializeField] float shootPoseDuration = 0.12f;
        [SerializeField] float recoverDuration = 0.25f;
        [Tooltip("Atış bittikten sonra yeniden nişan almadan önceki bekleme")]
        [SerializeField] float attackCooldown = 0.5f;
        [SerializeField] float hurtDuration = 0.3f;
        [Header("Karşı atış")]
        [Tooltip("Her vuruluştan sonra çıngırakla haber verip ateş eder; bu, karşı atışın uyarı süresi")]
        [SerializeField] float counterTelegraph = 0.4f;

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip rattleClip;
        [SerializeField] AudioClip shotClip;

        [Header("Kareler")]
        [SerializeField] Sprite[] walkFrames;
        [SerializeField] float walkFps = 5f;
        [SerializeField] Sprite[] rattleFrames;
        [SerializeField] float rattleFps = 16f;
        [SerializeField] Sprite aimFrame;
        [SerializeField] Sprite shootFrame;
        [SerializeField] Sprite hissFrame;
        [SerializeField] Sprite hurtFrame;
        [SerializeField] Sprite deathFrame;
        [SerializeField] float corpseDuration = 1f;

        public AttackState State { get; private set; }

        readonly RaycastHit2D[] sightHits = new RaycastHit2D[4];
        ContactFilter2D sightFilter;
        Coroutine attack;
        float nextAttackAt;
        float hurtUntil;
        bool countering;

        protected override bool CanChase => false;
        protected override bool HoldPosition => State != AttackState.None;

        protected override void Awake()
        {
            base.Awake();
            // Tetikleyiciler (checkpoint sütunu gibi) görüşü kesmesin.
            sightFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | GameLayers.PlayerMask | LayerMask.GetMask("Default"),
                useTriggers = false,
            };
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled || State != AttackState.None || Time.time < nextAttackAt || Time.time < hurtUntil)
                return;
            if (IsGrounded() && SeesPlayer())
                attack = StartCoroutine(AttackRoutine());
        }

        Vector2 BodyOrigin => (Vector2)transform.position + new Vector2(0f, muzzleOffset.y);
        Vector2 Muzzle => (Vector2)transform.position + new Vector2(muzzleOffset.x * FacingDirection, muzzleOffset.y);
        Vector2 PlayerTarget => (Vector2)Player.transform.position + Vector2.up * targetHeight;

        /// <summary>
        /// Önündeki koni: Gila menzilde, nişan açısının içinde ve arada katı bir şey yoksa görmüş sayılır.
        /// Havada süzülen Gila da görülür; tek yönlü platformlar görüşü kesmez.
        /// </summary>
        bool SeesPlayer()
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = PlayerTarget - BodyOrigin;
            // Işın collider içinde başlayınca da değer: içinden geçen oyuncu arkasındayken görülmesin.
            if (toPlayer.x * FacingDirection < 0f || toPlayer.magnitude > sightRange)
                return false;
            if (Mathf.Atan2(Mathf.Abs(toPlayer.y), Mathf.Abs(toPlayer.x)) * Mathf.Rad2Deg > maxAimAngle)
                return false;
            int count = Physics2D.Raycast(BodyOrigin, toPlayer.normalized, sightFilter, sightHits, toPlayer.magnitude + 0.5f);
            for (int i = 0; i < count; i++)
            {
                if (sightHits[i].collider.usedByEffector)
                    continue;
                return sightHits[i].collider.GetComponentInParent<PlayerHealth>() != null;
            }
            return false;
        }

        IEnumerator AttackRoutine(bool counter = false)
        {
            countering = counter;
            if (counter)
            {
                // Sersemler, sonra kısa bir çıngırakla karşılık vereceğini haber verir.
                yield return new WaitForSeconds(hurtDuration);
                FacePlayer();
            }
            State = AttackState.Rattle;
            PlaySound(rattleClip);
            yield return new WaitForSeconds(counter ? counterTelegraph : telegraphDuration);

            State = AttackState.Aim;
            yield return new WaitForSeconds(aimDuration);

            State = AttackState.Shoot;
            Fire();
            yield return new WaitForSeconds(shootPoseDuration);

            State = AttackState.Recover;
            yield return new WaitForSeconds(recoverDuration);

            State = AttackState.None;
            attack = null;
            countering = false;
            nextAttackAt = Time.time + attackCooldown;
        }

        void FacePlayer()
        {
            if (Player != null)
                TurnTowards(Player.transform.position.x >= transform.position.x ? 1 : -1);
        }

        void Fire()
        {
            PlaySound(shotClip);
            if (bulletPrefab == null)
                return;
            Vector2 muzzle = Muzzle;
            // Tetik anındaki konuma nişan: mermi takip etmez, Gila kayarak kaçabilir.
            Vector2 dir = Player != null && Player.IsAlive
                ? EnemyBullet.AimDirection(BodyOrigin, muzzle, PlayerTarget, FacingDirection, maxAimAngle)
                : Vector2.right * FacingDirection;
            EnemyBullet bullet = Instantiate(bulletPrefab, muzzle, Quaternion.identity);
            // Gövdeden itibaren denetler: namlunun içindeki oyuncu ya da duvar atlanmasın.
            bullet.Owner = this;
            bullet.Launch(dir, BodyOrigin);
        }

        void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        void CancelAttack()
        {
            if (attack != null)
                StopCoroutine(attack);
            attack = null;
            countering = false;
            // Yalnız çıngırak kesilir; biten atışın silah sesi yarıda kalmasın.
            if (State == AttackState.Rattle && audioSource != null)
                audioSource.Stop();
            State = AttackState.None;
        }

        // Obje kapanınca coroutine de durur; durum takılı kalırsa yılan yerinde donardı.
        void OnDisable()
        {
            attack = null;
            countering = false;
            State = AttackState.None;
        }

        protected override void OnDamaged()
        {
            hurtUntil = Time.time + hurtDuration;
            // Karşı atış zaten hazırlanıyorsa bozulmaz: art arda vurmak yılanı susturmasın.
            if (countering)
                return;
            // Vurulunca nişanı bozulur, saldırana döner ve her seferinde karşılık verir.
            CancelAttack();
            FacePlayer();
            if (isActiveAndEnabled)
            {
                State = AttackState.Recover; // sersemlerken yeni saldırı başlamasın
                attack = StartCoroutine(AttackRoutine(counter: true));
            }
        }

        /// <summary>Altıpatlar, ceset karesi kaybolunca görünür.</summary>
        protected override float WeaponDropDelay => deathFrame != null ? corpseDuration : base.WeaponDropDelay;

        protected override void OnDied()
        {
            CancelAttack();
            if (deathFrame == null || spriteRenderer == null)
                return;
            // Düşman gizlenir; ölü karesi kısa süre yerde kalır.
            var corpse = new GameObject("YilanOlu").AddComponent<SpriteRenderer>();
            corpse.transform.position = transform.position;
            corpse.sprite = deathFrame;
            corpse.flipX = spriteRenderer.flipX;
            corpse.sortingOrder = spriteRenderer.sortingOrder;
            Destroy(corpse.gameObject, corpseDuration);
        }

        public override void ResetEnemy()
        {
            CancelAttack();
            base.ResetEnemy();
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
            if (Time.time < hurtUntil)
                return hurtFrame;
            return State switch
            {
                AttackState.Rattle => Loop(rattleFrames, rattleFps),
                AttackState.Aim => aimFrame,
                AttackState.Shoot => shootFrame,
                AttackState.Recover => hissFrame,
                _ => Loop(walkFrames, walkFps),
            };
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
            Gizmos.color = Color.cyan;
            float a = maxAimAngle * Mathf.Deg2Rad;
            Gizmos.DrawRay(BodyOrigin, Vector2.right * FacingDirection * sightRange);
            Gizmos.DrawRay(BodyOrigin, new Vector2(Mathf.Cos(a) * FacingDirection, Mathf.Sin(a)) * sightRange);
            Gizmos.DrawRay(BodyOrigin, new Vector2(Mathf.Cos(a) * FacingDirection, -Mathf.Sin(a)) * sightRange);
        }
    }
}
