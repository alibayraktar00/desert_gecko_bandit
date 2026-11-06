using System.Collections;
using DesertGecko.Core;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Çıngıraklı Yılan Silahşor: sağa sola devriye gezer, kovalamaz. Önüne bakan görüş ışını
    /// Gila'ya değince durur, çıngırağını titretir (uyarı + ses), sonra alçaktan ateş eder.
    /// Uyarı süresinde oyuncu mermiden kaçabilir: üstünden zıplar, süzülür ya da arkasına geçer.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar, namlu ve mermi de yön değiştirir.
    /// </summary>
    public class Rattlesnake : EnemyBase
    {
        public enum AttackState { None, Rattle, Aim, Shoot, Recover }

        [Header("Görüş")]
        [SerializeField] float sightRange = 9f;

        [Header("Atış")]
        [SerializeField] EnemyBullet bulletPrefab;
        [Tooltip("Gövdeye göre namlu ucu, sağa bakarken (ateş karesindeki alev)")]
        [SerializeField] Vector2 muzzleOffset = new Vector2(0.66f, 0.54f);
        [Tooltip("Çıngırak uyarısı: oyuncunun tepki penceresi")]
        [SerializeField] float telegraphDuration = 0.8f;
        [SerializeField] float aimDuration = 0.15f;
        [SerializeField] float shootPoseDuration = 0.12f;
        [SerializeField] float recoverDuration = 0.4f;
        [Tooltip("Atış bittikten sonra yeniden nişan almadan önceki bekleme")]
        [SerializeField] float attackCooldown = 1.5f;
        [SerializeField] float hurtDuration = 0.3f;
        [SerializeField] float interruptedCooldown = 0.8f;
        [Header("Karşı atış")]
        [Tooltip("Vurulunca çıngıraksız, hemen ateş eder; bu sürede en fazla bir kez")]
        [SerializeField] float counterCooldown = 2.5f;

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

        readonly RaycastHit2D[] sightHits = new RaycastHit2D[1];
        ContactFilter2D sightFilter;
        Coroutine attack;
        float nextAttackAt;
        float hurtUntil;
        float nextCounterAt;

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

        /// <summary>Namlu hizasında öne bakan ışın: ilk değdiği şey Gila ise görmüş sayılır.</summary>
        bool SeesPlayer()
        {
            if (Player == null || !Player.IsAlive)
                return false;
            // Işın collider içinde başlayınca da değer: içinden geçen oyuncu arkasındayken görülmesin.
            if ((Player.transform.position.x - transform.position.x) * FacingDirection < 0f)
                return false;
            int count = Physics2D.Raycast(BodyOrigin, Vector2.right * FacingDirection, sightFilter, sightHits, sightRange);
            return count > 0 && sightHits[0].collider.GetComponentInParent<PlayerHealth>() != null;
        }

        IEnumerator AttackRoutine(bool counter = false)
        {
            if (counter)
            {
                yield return new WaitForSeconds(hurtDuration);
            }
            else
            {
                State = AttackState.Rattle;
                PlaySound(rattleClip);
                yield return new WaitForSeconds(telegraphDuration);
            }

            State = AttackState.Aim;
            yield return new WaitForSeconds(aimDuration);

            State = AttackState.Shoot;
            Fire();
            yield return new WaitForSeconds(shootPoseDuration);

            State = AttackState.Recover;
            yield return new WaitForSeconds(recoverDuration);

            State = AttackState.None;
            attack = null;
            nextAttackAt = Time.time + attackCooldown;
        }

        void Fire()
        {
            PlaySound(shotClip);
            if (bulletPrefab == null)
                return;
            Vector2 muzzle = (Vector2)transform.position + new Vector2(muzzleOffset.x * FacingDirection, muzzleOffset.y);
            EnemyBullet bullet = Instantiate(bulletPrefab, muzzle, Quaternion.identity);
            // Gövdeden itibaren denetler: namlunun içindeki oyuncu ya da duvar atlanmasın.
            bullet.Launch(FacingDirection, BodyOrigin);
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
            // Yalnız çıngırak kesilir; biten atışın silah sesi yarıda kalmasın.
            if (State == AttackState.Rattle && audioSource != null)
                audioSource.Stop();
            State = AttackState.None;
        }

        // Obje kapanınca coroutine de durur; durum takılı kalırsa yılan yerinde donardı.
        void OnDisable()
        {
            attack = null;
            State = AttackState.None;
        }

        protected override void OnDamaged()
        {
            // Vurulunca nişanı bozulur ve saldırana döner.
            CancelAttack();
            hurtUntil = Time.time + hurtDuration;
            nextAttackAt = Mathf.Max(nextAttackAt, Time.time + interruptedCooldown); // vurmak bekleme süresini kısaltmasın
            if (Player != null)
                TurnTowards(Player.transform.position.x >= transform.position.x ? 1 : -1);
            // Hâlâ ayaktaysa uyarmadan karşılık verir.
            if (isActiveAndEnabled && Time.time >= nextCounterAt)
            {
                nextCounterAt = Time.time + counterCooldown;
                State = AttackState.Recover; // sersemlerken yeni saldırı başlamasın
                attack = StartCoroutine(AttackRoutine(counter: true));
            }
        }

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
            nextCounterAt = 0f;
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
            Gizmos.DrawRay(BodyOrigin, Vector2.right * FacingDirection * sightRange);
        }
    }
}
