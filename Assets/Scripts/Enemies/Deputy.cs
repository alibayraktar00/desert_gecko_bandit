using System.Collections;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Şerif Yardımcısı: kasabanın silahşoru. Eli kabzada, ağır adımlarla devriye gezer; Gila'yı görünce
    /// üstüne koşmaz, durur, şapkasını iter ve altıpatlarını çeker (namluda parıltı: "ateş geliyor").
    /// Sonra sabit tempoda üç el ateş eder. Yılandan farkı: her mermide yeniden nişan alır ve Gila'nın
    /// gittiği yöne önleme yapar, mermisi de daha hızlıdır. Üçüncü mermiden sonra topu açıp doldurur:
    /// oyuncunun yaklaşma penceresi budur. Gila fazla yaklaşırsa bir iki adım geri çekilip menzilini korur.
    /// Silahı çekiliyken Gila'yı gözden kaçırmaz: arkasına geçilse de döner.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar.
    /// </summary>
    public class Deputy : EnemyBase
    {
        public enum DeputyState { None, Alert, Guard, Backstep, Aim, Burst, Smoke, Reload }

        [Header("Görüş")]
        [Tooltip("Ekranın (20 birim) yaklaşık %60'ı")]
        [SerializeField] float sightRange = 13f;
        [Tooltip("Nişan alınabilen en büyük açı (yatayın üstü/altı, derece)")]
        [SerializeField] float maxAimAngle = 50f;
        [Tooltip("Hedef nokta: Gila'nın ayağından göğüs hizasına")]
        [SerializeField] float targetHeight = 0.45f;
        [Tooltip("Silahı çekiliyken Gila'yı bu kadar süre göremezse kılıfına sokup devriyeye döner")]
        [SerializeField] float giveUpTime = 2f;

        [Header("Fark etme")]
        [Tooltip("Şapkayı itip silahı çekme: oyuncunun ilk tepki penceresi")]
        [SerializeField] float alertDuration = 0.35f;
        [SerializeField] Color glintColor = Color.white;

        [Header("Üçlü atış")]
        [SerializeField] EnemyBullet bulletPrefab;
        [Tooltip("Gövdeye göre namlu ucu, sağa bakarken")]
        [SerializeField] Vector2 muzzleOffset = new Vector2(0.95f, 0.9f);
        [SerializeField] int burstSize = 3;
        [SerializeField] float shotInterval = 0.22f;
        [Tooltip("Her seriden önce nişan alma: namlu parlar")]
        [SerializeField] float aimDuration = 0.2f;
        [Tooltip("Yılanın mermisi 9")]
        [SerializeField] float bulletSpeed = 16f;
        [Tooltip("Serinin ilk mermisinden sonrakilerde Gila'nın hızına göre önleme (0 = olduğu yere, 1 = tam önleme)")]
        [Range(0f, 1f)]
        [SerializeField] float leadFactor = 0.75f;
        [SerializeField] float firePoseDuration = 0.07f;
        [SerializeField] float recoilPoseDuration = 0.1f;
        [SerializeField] float smokeDuration = 0.15f;

        [Header("Doldurma")]
        [Tooltip("Savunmasızlık penceresi: ateş edemez, kıpırdayamaz")]
        [SerializeField] float reloadDuration = 1f;
        [SerializeField] Color dustColor = new Color32(0xB8, 0xB4, 0xAC, 0xFF);

        [Header("Mesafe koruma")]
        [Tooltip("Gila bundan yakınsa seriden önce geri çekilir")]
        [SerializeField] float tooCloseRange = 3f;
        [SerializeField] float backstepDistance = 1.3f;
        [SerializeField] float backstepDuration = 0.18f;

        [Header("Hasar")]
        [Tooltip("Hasar kareleri bu süreye yayılır: sarsılma, denge kaybı, büzülme, doğrulma")]
        [SerializeField] float hurtDuration = 0.4f;

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip drawClip;
        [SerializeField] AudioClip shotClip;

        [Header("Kareler")]
        [SerializeField] Sprite idleFrame;
        [SerializeField] Sprite[] walkFrames;
        [SerializeField] float walkFps = 6f;
        [SerializeField] Sprite hatTipFrame;
        [SerializeField] Sprite drawFrame;
        [SerializeField] Sprite aimFrame;
        [SerializeField] Sprite fireFrame;
        [SerializeField] Sprite recoilFrame;
        [SerializeField] Sprite smokeFrame;
        [Tooltip("Doldurma süresine yayılıp bir kez oynar: kovanları boşaltma, mermi yükleme, silindiri kapatma")]
        [SerializeField] Sprite[] reloadFrames;
        [SerializeField] Sprite guardFrame;
        [SerializeField] Sprite[] hurtFrames;
        [SerializeField] Sprite[] victoryFrames;
        [SerializeField] float victoryFps = 4f;
        [Tooltip("Yenilgi: havada savrulurken oynar (sarsılma, geriye yatış, devrilme)")]
        [SerializeField] Sprite[] fallFrames;
        [Tooltip("Yenilgi: yere değince oynar (çarpma, sarsıntı, serilme, hareketsiz)")]
        [SerializeField] Sprite[] landFrames;
        [SerializeField] float defeatFps = 9f;
        [Tooltip("Çakal gibi uçmaz: olduğu yerde geriye devrilir")]
        [SerializeField] Vector2 defeatKnock = new Vector2(3.2f, 4.5f);
        [SerializeField] Sprite hatFrame;
        [Tooltip("Yenilgide şapkanın gövdeden ayrıldığı yükseklik")]
        [SerializeField] float hatHeight = 1.1f;

        public DeputyState State { get; private set; }
        /// <summary>Toptaki mermi. Vurulup serisi bozulsa da dolmaz: doldurma penceresi hep gelir.</summary>
        public int ShotsLeft { get; private set; }

        readonly RaycastHit2D[] sightHits = new RaycastHit2D[4];
        ContactFilter2D sightFilter;
        Rigidbody2D playerBody;
        Coroutine routine;
        float stateStartedAt;
        float lastShotAt = -10f;
        float hurtUntil;
        float reloadEndsAt;
        bool reloading;
        int lifeId; // dirilince artar: önceki ölümün yenilgi animasyonu silah bırakmasın

        bool PlayerDown => Player != null && !Player.IsAlive;

        protected override bool CanChase => false;
        protected override bool HoldPosition => State != DeputyState.None || PlayerDown;
        /// <summary>Ceset geriye savrulur: altıpatlar, ceset yere oturunca orada çıkar.</summary>
        protected override bool DropsWeaponAfterDefeat => true;

        protected override void Awake()
        {
            base.Awake();
            ShotsLeft = burstSize;
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
            if (!isActiveAndEnabled || State != DeputyState.None || Time.time < hurtUntil)
                return;
            if (IsGrounded() && SeesPlayer(onlyAhead: true))
                routine = StartCoroutine(EngageRoutine(firstSight: true));
        }

        Vector2 BodyOrigin => (Vector2)transform.position + new Vector2(0f, muzzleOffset.y);
        Vector2 Muzzle => (Vector2)transform.position + new Vector2(muzzleOffset.x * FacingDirection, muzzleOffset.y);
        Vector2 PlayerTarget => (Vector2)Player.transform.position + Vector2.up * targetHeight;
        int DirectionToPlayer => Player.transform.position.x >= transform.position.x ? 1 : -1;

        /// <summary>
        /// Gila menzilde, nişan açısının içinde ve arada katı bir şey yoksa görür. Devriyede yalnız önüne
        /// bakar; silahı çekiliyken arkasına da kulak kesilir. Tek yönlü platformlar görüşü kesmez.
        /// </summary>
        bool SeesPlayer(bool onlyAhead)
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = PlayerTarget - BodyOrigin;
            if ((onlyAhead && toPlayer.x * FacingDirection < 0f) || toPlayer.magnitude > sightRange)
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

        void Enter(DeputyState state)
        {
            State = state;
            stateStartedAt = Time.time;
        }

        /// <summary>
        /// Çatışma döngüsü: (fark etme) → nişan → üçlü seri → doldurma → yeniden nişan... Gila gözden kaybolunca biter.
        /// </summary>
        IEnumerator EngageRoutine(bool firstSight)
        {
            if (firstSight)
            {
                Enter(DeputyState.Alert);
                TurnTowards(DirectionToPlayer);
                yield return new WaitForSeconds(alertDuration * 0.5f);
                // Silah kılıftan çıkar: ses ve namluda parıltı.
                PlaySound(drawClip);
                Glint();
                yield return new WaitForSeconds(alertDuration * 0.5f);
            }
            else
            {
                // Vuruldu: sersemliği geçince kaldığı yerden sürdürür.
                Enter(DeputyState.Guard);
                yield return new WaitForSeconds(hurtDuration);
            }

            while (true)
            {
                if (reloading || ShotsLeft <= 0)
                    yield return ReloadRoutine();

                // Hedefi bekler: görünmüyorsa silahı hazır, tetikte durur.
                Enter(DeputyState.Guard);
                float lostAt = Time.time;
                while (!SeesPlayer(onlyAhead: false))
                {
                    if (Time.time - lostAt > giveUpTime || PlayerDown)
                    {
                        State = DeputyState.None;
                        routine = null;
                        yield break;
                    }
                    yield return null;
                }
                TurnTowards(DirectionToPlayer);

                if (Mathf.Abs(Player.transform.position.x - transform.position.x) < tooCloseRange)
                    yield return BackstepRoutine();

                if (!firstSight)
                {
                    Enter(DeputyState.Aim);
                    Glint();
                    yield return new WaitForSeconds(aimDuration);
                }
                firstSight = false;

                Enter(DeputyState.Burst);
                while (ShotsLeft > 0)
                {
                    bool first = ShotsLeft == burstSize;
                    ShotsLeft--;
                    Fire(first ? 0f : leadFactor);
                    if (ShotsLeft > 0)
                        yield return new WaitForSeconds(shotInterval);
                }
                yield return new WaitForSeconds(firePoseDuration + recoilPoseDuration);

                Enter(DeputyState.Smoke);
                yield return new WaitForSeconds(smokeDuration);
            }
        }

        IEnumerator ReloadRoutine()
        {
            // Vurulunca doldurma baştan başlamaz: kalan süre işler, pencere uzatılamaz.
            if (!reloading)
            {
                reloading = true;
                reloadEndsAt = Time.time + reloadDuration;
            }
            Enter(DeputyState.Reload);
            float nextDust = Time.time + 0.2f;
            while (Time.time < reloadEndsAt)
            {
                if (Time.time >= nextDust)
                {
                    nextDust = Time.time + 0.35f;
                    PixelBurst.Spawn(Muzzle + Vector2.left * (0.4f * FacingDirection), dustColor, 2, 1.2f, 0.3f);
                }
                yield return null;
            }
            reloading = false;
            ShotsLeft = burstSize;
        }

        /// <summary>Yüzü Gila'ya dönük, geri geri çekilir. Arkası uçurum ya da duvarsa yerinde kalır.</summary>
        IEnumerator BackstepRoutine()
        {
            int back = -FacingDirection;
            Bounds b = box.bounds;
            Vector2 foot = new Vector2(b.center.x + back * (b.extents.x + backstepDistance), b.min.y + 0.1f);
            bool canStep = Physics2D.Raycast(foot, Vector2.down, 0.6f, GameLayers.GroundMask)
                && !Physics2D.Raycast(b.center, Vector2.right * back, b.extents.x + backstepDistance, GameLayers.GroundMask);
            if (!canStep)
                yield break;

            Enter(DeputyState.Backstep);
            float moved = 0f;
            while (moved < backstepDistance)
            {
                float step = Mathf.Min(backstepDistance / backstepDuration * Time.fixedDeltaTime, backstepDistance - moved);
                body.position += Vector2.right * (back * step);
                moved += step;
                yield return new WaitForFixedUpdate();
            }
        }

        void Fire(float lead)
        {
            lastShotAt = Time.time;
            PlaySound(shotClip);
            if (bulletPrefab == null)
                return;
            // Seri sürerken Gila arkaya geçtiyse döner: namlu hep ondadır.
            bool alive = Player != null && Player.IsAlive;
            if (alive)
                TurnTowards(DirectionToPlayer);
            Vector2 muzzle = Muzzle;
            Vector2 dir = alive
                ? EnemyBullet.AimDirection(BodyOrigin, muzzle, LeadTarget(muzzle, lead), FacingDirection, maxAimAngle)
                : Vector2.right * FacingDirection;
            EnemyBullet bullet = Instantiate(bulletPrefab, muzzle, Quaternion.identity);
            bullet.Owner = this;
            bullet.Speed = bulletSpeed;
            // Gövdeden itibaren denetler: namlunun içindeki oyuncu ya da duvar atlanmasın.
            bullet.Launch(dir, BodyOrigin);
            PixelBurst.Spawn(muzzle, new Color32(0xFF, 0xD8, 0x5A, 0xFF), 3, 2f, 0.12f);
        }

        /// <summary>Merminin varacağı anda Gila'nın olacağı yer: hızına göre önleme, dikeyde yarım (zıplama yay çizer).</summary>
        Vector2 LeadTarget(Vector2 muzzle, float lead)
        {
            Vector2 target = PlayerTarget;
            if (lead <= 0f)
                return target;
            if (playerBody == null)
                playerBody = Player.GetComponent<Rigidbody2D>();
            if (playerBody == null)
                return target;
            float travel = Vector2.Distance(muzzle, target) / Mathf.Max(1f, bulletSpeed);
            Vector2 velocity = playerBody.linearVelocity;
            return target + new Vector2(velocity.x, velocity.y * 0.5f) * (travel * lead);
        }

        void Glint() => PixelBurst.Spawn(Muzzle, glintColor, 4, 0.8f, 0.25f);

        void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        void CancelRoutine()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            State = DeputyState.None;
        }

        // Obje kapanınca coroutine de durur; durum takılı kalırsa şerif yerinde donardı.
        void OnDisable()
        {
            routine = null;
            State = DeputyState.None;
        }

        protected override void OnDamaged()
        {
            // Serisi bozulur ama topundaki mermi ve süren doldurma aynen kalır; saldırana döner.
            CancelRoutine();
            hurtUntil = Time.time + hurtDuration;
            if (Player != null)
                TurnTowards(DirectionToPlayer);
            if (isActiveAndEnabled)
                routine = StartCoroutine(EngageRoutine(firstSight: false));
        }

        protected override void OnDied()
        {
            CancelRoutine();
            int knock = Player != null ? -DirectionToPlayer : -FacingDirection;
            Sprite first = fallFrames != null && fallFrames.Length > 0 ? fallFrames[0] : null;
            JackalDefeat defeat = JackalDefeat.Spawn(transform.position, first, hatFrame, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, knock, hatHeight);
            defeat.Animate(fallFrames, landFrames, defeatFps, defeatKnock);
            int life = lifeId;
            defeat.Finished += position =>
            {
                if (life == lifeId) // bu arada oyuncu ölüp şerif dirildiyse eski ceset silah bırakmaz
                    DropWeapon(position);
            };
        }

        public override void ResetEnemy()
        {
            CancelRoutine();
            lifeId++;
            base.ResetEnemy();
            ShotsLeft = burstSize;
            reloading = false;
            hurtUntil = 0f;
            lastShotAt = -10f;
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
            if (Time.time < hurtUntil && hurtFrames != null && hurtFrames.Length > 0)
            {
                float progress = 1f - (hurtUntil - Time.time) / hurtDuration;
                return hurtFrames[Mathf.Clamp(Mathf.FloorToInt(progress * hurtFrames.Length), 0, hurtFrames.Length - 1)];
            }
            float sinceShot = Time.time - lastShotAt;
            switch (State)
            {
                case DeputyState.Alert:
                    return Time.time - stateStartedAt < alertDuration * 0.5f ? hatTipFrame : drawFrame;
                case DeputyState.Guard: return guardFrame;
                case DeputyState.Backstep: return Loop(walkFrames, walkFps * 2f);
                case DeputyState.Aim: return aimFrame;
                case DeputyState.Burst:
                    if (sinceShot < firePoseDuration)
                        return fireFrame;
                    return sinceShot < firePoseDuration + recoilPoseDuration ? recoilFrame : aimFrame;
                case DeputyState.Smoke: return smokeFrame;
                case DeputyState.Reload:
                    if (reloadFrames == null || reloadFrames.Length == 0)
                        return guardFrame;
                    // Vurulup kaldığı yerden sürse de kare, doldurmanın neresinde olduğunu gösterir.
                    float progress = 1f - (reloadEndsAt - Time.time) / reloadDuration;
                    return reloadFrames[Mathf.Clamp(Mathf.FloorToInt(progress * reloadFrames.Length), 0, reloadFrames.Length - 1)];
                default:
                    // Kenarda ya da duvarda EnemyBase onu durdurur; yerinde yürümesin.
                    return Mathf.Abs(body.linearVelocity.x) > 0.1f ? Loop(walkFrames, walkFps) : idleFrame;
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
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            float a = maxAimAngle * Mathf.Deg2Rad;
            Gizmos.DrawRay(BodyOrigin, Vector2.right * FacingDirection * sightRange);
            Gizmos.DrawRay(BodyOrigin, new Vector2(Mathf.Cos(a) * FacingDirection, Mathf.Sin(a)) * sightRange);
            Gizmos.DrawRay(BodyOrigin, new Vector2(Mathf.Cos(a) * FacingDirection, -Mathf.Sin(a)) * sightRange);
        }
    }
}
