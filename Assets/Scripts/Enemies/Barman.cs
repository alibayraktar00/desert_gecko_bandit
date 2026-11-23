using System.Collections;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Barmen/Fedai: kasabanın iri kıyım adamı, iki fazlı.
    /// Fıçılı faz: viski fıçısını göğsünde taşıyarak devriye gezer; fıçı onu her darbeden korur. Gila'yı görünce
    /// ağır ağır üstüne yürür, menzile girince fıçıyı başının üstüne kaldırır (uyarı) ve havadan Gila'ya fırlatır.
    /// Tüfekli faz: fıçı gidince sırtındaki pompalıyı çeker ve Gila'nın peşine düşer; menzile girince
    /// doğrultur (uyarı), saçmayı atar, tüfeği kurar. Artık korumasızdır: vurulabilir, vurulunca sendeler.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar.
    /// </summary>
    public class Barman : EnemyBase
    {
        public enum BarmanState { None, Windup, Swing, FollowThrough, Draw, Aim, Fire, Pump, Reload }

        [Header("Fıçı fırlatma")]
        [SerializeField] BarmanBarrel barrelPrefab;
        [Tooltip("Gila bu kadar yaklaşınca (ve arada engel yoksa) fıçıyı fırlatır")]
        [SerializeField] float throwRange = 7.5f;
        [Tooltip("Fıçıyı başının üstüne kaldırıp geriye esneme: oyuncunun tepki penceresi")]
        [SerializeField] float windupDuration = 0.35f;
        [SerializeField] float swingDuration = 0.08f;
        [SerializeField] float followDuration = 0.2f;
        [Tooltip("Gövdeye göre fıçının elden çıktığı nokta, sağa bakarken")]
        [SerializeField] Vector2 releaseOffset = new Vector2(0.7f, 1.45f);
        [Tooltip("Fıçının havada kalma süresi: mesafe / bu hız, sınırlar arasında")]
        [SerializeField] float throwSpeed = 13f;
        [SerializeField] float minFlightTime = 0.4f;
        [SerializeField] float maxFlightTime = 0.7f;
        [Tooltip("Fıçıyı Gila'nın gittiği yöne atar (0 = olduğu yere, 1 = tam önleme)")]
        [Range(0f, 1f)]
        [SerializeField] float throwLead = 0.7f;
        [SerializeField] Color chipColor = new Color32(0x8A, 0x5A, 0x34, 0xFF);

        [Header("Pompalı")]
        [SerializeField] EnemyBullet pelletPrefab;
        [SerializeField] float drawDuration = 0.28f;
        [Tooltip("Tüfekle Gila'nın peşinden yürüme hızı (oyuncu 5.5)")]
        [SerializeField] float gunChaseSpeed = 4.2f;
        [Tooltip("Gila bu kadar yakındaysa ateş eder")]
        [SerializeField] float gunRange = 7.5f;
        [Tooltip("Gövdeye göre namlu ucu, sağa bakarken")]
        [SerializeField] Vector2 muzzleOffset = new Vector2(0.9f, 1f);
        [Tooltip("Doğrultma: oyuncunun zıplayıp kaçma penceresi")]
        [SerializeField] float aimDuration = 0.3f;
        [SerializeField] float firePoseDuration = 0.1f;
        [SerializeField] float recoilPoseDuration = 0.15f;
        [Tooltip("Tüfeği kurma: bu sırada ateş edemez")]
        [SerializeField] float pumpDuration = 0.3f;
        [SerializeField] float shotCooldown = 0.4f;
        [Tooltip("Tüfeğe sığan fişek: hepsi atılınca durup yeniden doldurur")]
        [SerializeField] int shellsPerLoad = 2;
        [Tooltip("Fişekleri tek tek sürme süresi (toplam): olduğu yerde durur, Gila'nın saldırı penceresi")]
        [SerializeField] float reloadDuration = 0.9f;
        [SerializeField] int pelletCount = 4;
        [Tooltip("Saçmaların yelpaze açısı (derece, tek yön)")]
        [SerializeField] float spreadAngle = 12f;
        [SerializeField] float pelletSpeed = 17f;
        [Tooltip("Havadaki Gila'ya namluyu kaldırabileceği en büyük açı (derece)")]
        [SerializeField] float maxAimAngle = 60f;
        [Tooltip("Hedef nokta: Gila'nın ayağından gövde ortasına")]
        [SerializeField] float targetHeight = 0.45f;

        [Header("Hasar")]
        [Tooltip("Hasar kareleri bu süreye yayılır: sendeleme, denge kaybı, büzülme, doğrulma")]
        [SerializeField] float hurtDuration = 0.45f;

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip throwClip;
        [SerializeField] AudioClip shotClip;
        [SerializeField] AudioClip pumpClip;

        [Header("Kareler: fıçılı")]
        [SerializeField] Sprite[] idleFrames;
        [SerializeField] float idleFps = 2f;
        [SerializeField] Sprite[] walkFrames;
        [SerializeField] float walkFps = 12f;
        [Tooltip("Duruş, kaldırma, baş üstü, geriye esneme: uyarı süresine yayılır")]
        [SerializeField] Sprite[] windupFrames;
        [SerializeField] Sprite swingFrame;
        [Header("Kareler: tüfekli")]
        [SerializeField] Sprite followFrame;
        [SerializeField] Sprite unarmedFrame;
        [SerializeField] Sprite[] drawFrames;
        [SerializeField] Sprite aimFrame;
        [SerializeField] Sprite fireFrame;
        [SerializeField] Sprite recoilFrame;
        [Tooltip("Tepedeki Gila'ya: bacaklar yerde, belden yukarısı geriye yatık, namlu yukarıda")]
        [SerializeField] Sprite upAimFrame;
        [SerializeField] Sprite upFireFrame;
        [SerializeField] Sprite upRecoilFrame;
        [Tooltip("Tam tepedeki Gila'ya: namlu dimdik yukarı")]
        [SerializeField] Sprite steepAimFrame;
        [SerializeField] Sprite steepFireFrame;
        [Tooltip("Gila yataydan bu açıdan daha dikteyse çapraz yerine dik yukarı kareleri oynar (derece)")]
        [SerializeField] float steepAngle = 72f;
        [SerializeField] Sprite pumpFrame;
        [SerializeField] Sprite readyFrame;
        [Tooltip("Fişek sürme: el kemerden fişek alır, yuvaya getirir, içeri iter. Her fişek için bir kez oynar")]
        [SerializeField] Sprite[] reloadFrames;
        [Tooltip("Tüfek elde yürüyüş")]
        [SerializeField] Sprite[] gunWalkFrames;
        [SerializeField] Sprite[] hurtFrames;
        [SerializeField] Sprite[] victoryFrames;
        [SerializeField] float victoryFps = 2.5f;
        [Header("Kareler: yenilgi")]
        [SerializeField] Sprite[] fallFrames;
        [SerializeField] Sprite[] landFrames;
        [SerializeField] float defeatFps = 8f;
        [Tooltip("İri gövde: uçmaz, olduğu yerde geriye devrilir")]
        [SerializeField] Vector2 defeatKnock = new Vector2(2.2f, 3.5f);

        public BarmanState State { get; private set; }
        /// <summary>Fıçı hâlâ elinde: hiçbir darbe işlemez.</summary>
        public bool HasBarrel { get; private set; } = true;

        readonly RaycastHit2D[] sightHits = new RaycastHit2D[4];
        ContactFilter2D solidFilter;
        Coroutine routine;
        float stateStartedAt;
        float nextShotAt;
        int shellsLeft;
        float hurtUntil;
        float barrelChaseSpeed;
        Rigidbody2D playerBody;
        bool aimingUp;
        bool aimingSteep;
        int lifeId; // dirilince artar: önceki ölümün yenilgi animasyonu silah bırakmasın

        bool PlayerDown => Player != null && !Player.IsAlive;

        // İki fazda da Gila'nın üstüne yürür: fıçıyla ağır, tüfekle daha çevik.
        protected override bool HoldPosition => State != BarmanState.None || PlayerDown;
        protected override bool Deflects(in DamageInfo hit) => HasBarrel;
        protected override bool KeepsFooting => true;
        /// <summary>Ceset geriye devrilir: pompalı, ceset yere oturunca orada çıkar.</summary>
        protected override bool DropsWeaponAfterDefeat => true;

        protected override void Awake()
        {
            base.Awake();
            barrelChaseSpeed = chaseSpeed;
            shellsLeft = shellsPerLoad;
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
            if (!isActiveAndEnabled || State != BarmanState.None || Time.time < hurtUntil || !IsGrounded())
                return;
            if (HasBarrel)
            {
                // Kovalamasa da (Gila yüksekte, havada) menzildeyse fıçıyı ona savurur.
                if (PlayerWithin(throwRange))
                    routine = StartCoroutine(ThrowRoutine());
            }
            else if (Time.time >= nextShotAt && PlayerWithin(gunRange))
            {
                routine = StartCoroutine(ShootRoutine());
            }
        }

        Vector2 Eye => (Vector2)transform.position + Vector2.up * muzzleOffset.y;
        Vector2 Muzzle => (Vector2)transform.position + new Vector2(muzzleOffset.x * FacingDirection, muzzleOffset.y);
        Vector2 PlayerTarget => (Vector2)Player.transform.position + Vector2.up * targetHeight;
        int DirectionToPlayer => Player.transform.position.x >= transform.position.x ? 1 : -1;

        /// <summary>Gila bu mesafede ve arada katı bir şey yok. Tek yönlü platformlar engel sayılmaz.</summary>
        bool PlayerWithin(float range)
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 path = PlayerTarget - Eye;
            if (path.magnitude > range)
                return false;
            int count = Physics2D.Raycast(Eye, path.normalized, solidFilter, sightHits, path.magnitude);
            for (int i = 0; i < count; i++)
                if (!sightHits[i].collider.usedByEffector)
                    return false;
            return true;
        }

        void Enter(BarmanState state)
        {
            State = state;
            stateStartedAt = Time.time;
        }

        IEnumerator ThrowRoutine()
        {
            Enter(BarmanState.Windup);
            TurnTowards(DirectionToPlayer);
            yield return new WaitForSeconds(windupDuration);

            Enter(BarmanState.Swing);
            if (Player != null && Player.IsAlive)
                TurnTowards(DirectionToPlayer);
            PlaySound(throwClip);
            yield return new WaitForSeconds(swingDuration);

            ThrowBarrel();
            HasBarrel = false;
            chaseSpeed = gunChaseSpeed;
            Enter(BarmanState.FollowThrough);
            yield return new WaitForSeconds(followDuration);

            // Fıçı gitti: sırtındaki pompalıya uzanır.
            Enter(BarmanState.Draw);
            yield return new WaitForSeconds(drawDuration);

            State = BarmanState.None;
            routine = null;
            nextShotAt = Time.time + 0.2f;
        }

        /// <summary>
        /// Fıçı, Gila'nın gittiği yöne önleme yaparak düşeceği yere atılır (havada takip etmez: yön değiştiren kaçar).
        /// </summary>
        void ThrowBarrel()
        {
            if (barrelPrefab == null)
                return;
            Vector2 from = (Vector2)transform.position + new Vector2(releaseOffset.x * FacingDirection, releaseOffset.y);
            Vector2 target = Player != null && Player.IsAlive
                ? (Vector2)Player.transform.position + Vector2.up * 0.3f
                : (Vector2)transform.position + new Vector2(FacingDirection * throwRange * 0.7f, 0.3f);
            Vector2 delta = target - from;
            float time = Mathf.Clamp(Mathf.Abs(delta.x) / throwSpeed, minFlightTime, maxFlightTime);
            if (throwLead > 0f && Player != null && Player.IsAlive)
            {
                // Yatayda önleme: koşarak altından geçmeye çalışan Gila'nın önüne düşer.
                if (playerBody == null)
                    playerBody = Player.GetComponent<Rigidbody2D>();
                if (playerBody != null)
                    delta.x += playerBody.linearVelocity.x * time * throwLead;
            }
            float gravity = barrelPrefab.Gravity;
            var velocity = new Vector2(delta.x / time, (delta.y + 0.5f * gravity * time * time) / time);
            BarmanBarrel barrel = Instantiate(barrelPrefab, from, Quaternion.identity);
            barrel.Launch(velocity, FacingDirection);
        }

        /// <summary>
        /// Gila tepesinde: namlunun normal açısından (maxAimAngle) dik ve yukarıda. O zaman açı sınırı uygulanmaz,
        /// doğrudan Gila'ya nişan alır; yoksa saçma hep çapraza gider, tepedeki Gila'ya hiç isabet etmezdi.
        /// </summary>
        bool PlayerOverhead(out Vector2 direction)
        {
            direction = Vector2.up;
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 toTarget = PlayerTarget - Eye;
            direction = toTarget.normalized;
            return toTarget.y > 0.5f && Mathf.Atan2(toTarget.y, Mathf.Abs(toTarget.x)) * Mathf.Rad2Deg > maxAimAngle;
        }

        /// <summary>
        /// Tepeye nişan: yukarı nişan kareleri oynar (bacaklar yerde, gövde geriye yatık, tüfek yukarıda).
        /// Gila tam tepedeyse çapraz değil dik yukarı kareler: namlu saçmanın gittiği yöne baksın.
        /// </summary>
        void SetLean(bool lean)
        {
            aimingUp = lean;
            aimingSteep = false;
            if (!lean || Player == null)
                return;
            Vector2 toTarget = PlayerTarget - Eye;
            aimingSteep = Mathf.Atan2(toTarget.y, Mathf.Abs(toTarget.x)) * Mathf.Rad2Deg > steepAngle;
        }

        IEnumerator ShootRoutine()
        {
            Enter(BarmanState.Aim);
            TurnTowards(DirectionToPlayer);
            SetLean(PlayerOverhead(out _));
            yield return new WaitForSeconds(aimDuration);

            Enter(BarmanState.Fire);
            FireBlast();
            shellsLeft--;
            yield return new WaitForSeconds(firePoseDuration + recoilPoseDuration);
            SetLean(false);

            Enter(BarmanState.Pump);
            PlaySound(pumpClip);
            EjectShell();
            yield return new WaitForSeconds(pumpDuration);

            if (shellsLeft <= 0)
            {
                // Tüfek boş: olduğu yerde durup fişekleri tek tek sürer, sonunda pompalar.
                Enter(BarmanState.Reload);
                yield return new WaitForSeconds(reloadDuration);
                shellsLeft = Mathf.Max(1, shellsPerLoad);
                PlaySound(pumpClip);
            }

            State = BarmanState.None;
            routine = null;
            nextShotAt = Time.time + shotCooldown;
        }

        /// <summary>Saçma: tetik anındaki Gila'ya nişanlı, yelpaze açılan kısa menzilli taneler.</summary>
        void FireBlast()
        {
            PlaySound(shotClip);
            CameraShake.AddAt(transform.position, 0.2f);
            if (pelletPrefab == null)
                return;
            bool alive = Player != null && Player.IsAlive;
            Vector2 muzzle = Muzzle;
            Vector2 aim;
            if (PlayerOverhead(out Vector2 direct))
            {
                // Tepedeki Gila: açı sınırı yok, saçma dosdoğru ona gider; namlu ucu da o yöndedir.
                aim = direct;
                muzzle = Eye + direct * muzzleOffset.x;
                SetLean(true);
            }
            else
            {
                SetLean(false);
                // Doğrultma sırasında Gila arkaya geçtiyse dönmez: namlunun baktığı yöne düz atar.
                aim = alive && (PlayerTarget.x - transform.position.x) * FacingDirection > 0f
                    ? EnemyBullet.AimDirection(Eye, muzzle, PlayerTarget, FacingDirection, maxAimAngle)
                    : Vector2.right * FacingDirection;
            }
            float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < pelletCount; i++)
            {
                float t = pelletCount > 1 ? i / (pelletCount - 1f) * 2f - 1f : 0f;
                float angle = (aimAngle + t * spreadAngle) * Mathf.Deg2Rad;
                EnemyBullet pellet = Instantiate(pelletPrefab, muzzle, Quaternion.identity);
                pellet.Owner = this;
                pellet.Speed = pelletSpeed;
                pellet.Lifetime = (gunRange + 1.5f) / pelletSpeed; // kısa menzil: uzaktaki Gila'ya ulaşmaz
                pellet.Launch(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), Eye);
            }
            PixelBurst.Spawn(muzzle, new Color32(0xFF, 0xD8, 0x5A, 0xFF), 5, 2.5f, 0.15f);
        }

        void EjectShell()
        {
            ShellCasing.Spawn(Eye + Vector2.right * (0.3f * FacingDirection), new Vector2(-FacingDirection * 1.5f, 3.5f), new Color32(0xC0, 0x3A, 0x2E, 0xFF));
        }

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
            State = BarmanState.None;
            SetLean(false);
        }

        // Obje kapanınca coroutine de durur; durum takılı kalırsa barmen yerinde donardı.
        void OnDisable()
        {
            routine = null;
            State = BarmanState.None;
            SetLean(false);
        }

        protected override void OnBlocked()
        {
            // Fıçıdan seken darbe: kıymık saçılır, barmen saldırana döner.
            if (!HasBarrel)
                return;
            PixelBurst.Spawn(box.bounds.center + Vector3.right * (0.4f * FacingDirection), chipColor, 5, 3.5f, 0.25f);
            if (State == BarmanState.None && Player != null && Player.IsAlive)
                TurnTowards(DirectionToPlayer);
        }

        protected override void OnDamaged()
        {
            // Yalnız tüfekli fazda vurulur. İri gövde: doğrulturken ya da ateş ederken darbe onu durdurmaz.
            if (State != BarmanState.None)
                return;
            CancelRoutine();
            hurtUntil = Time.time + hurtDuration;
            nextShotAt = Time.time + hurtDuration + 0.15f;
            if (Player != null)
                TurnTowards(DirectionToPlayer);
        }

        protected override void OnDied()
        {
            CancelRoutine();
            int knock = Player != null ? -DirectionToPlayer : -FacingDirection;
            Sprite first = fallFrames != null && fallFrames.Length > 0 ? fallFrames[0] : null;
            JackalDefeat defeat = JackalDefeat.Spawn(transform.position, first, null, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, knock);
            defeat.Animate(fallFrames, landFrames, defeatFps, defeatKnock);
            int life = lifeId;
            defeat.Finished += position =>
            {
                if (life == lifeId) // bu arada oyuncu ölüp barmen dirildiyse eski ceset silah bırakmaz
                    DropWeapon(position);
            };
        }

        public override void ResetEnemy()
        {
            CancelRoutine();
            lifeId++;
            base.ResetEnemy();
            HasBarrel = true; // yeni bir fıçıyla döner
            chaseSpeed = barrelChaseSpeed;
            nextShotAt = 0f;
            shellsLeft = shellsPerLoad;
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
            float since = Time.time - stateStartedAt;
            switch (State)
            {
                case BarmanState.Windup: return Stretch(windupFrames, since / windupDuration);
                case BarmanState.Swing: return swingFrame;
                case BarmanState.FollowThrough: return since < followDuration * 0.4f ? followFrame : unarmedFrame;
                case BarmanState.Draw: return Stretch(drawFrames, since / drawDuration);
            }
            if (HasBarrel)
            {
                // Kenarda ya da duvarda EnemyBase onu durdurur; yerinde yürümesin.
                return !PlayerDown && Mathf.Abs(body.linearVelocity.x) > 0.1f ? Loop(walkFrames, walkFps) : Loop(idleFrames, idleFps);
            }
            if (PlayerDown)
                return Loop(victoryFrames, victoryFps);
            if (Time.time < hurtUntil)
                return Stretch(hurtFrames, 1f - (hurtUntil - Time.time) / hurtDuration);
            switch (State)
            {
                case BarmanState.Aim:
                    if (aimingUp && aimingSteep && steepAimFrame != null)
                        return steepAimFrame;
                    return aimingUp && upAimFrame != null ? upAimFrame : aimFrame;
                case BarmanState.Fire:
                    if (aimingUp && aimingSteep && steepFireFrame != null)
                        return since < firePoseDuration ? steepFireFrame : steepAimFrame;
                    if (aimingUp && upFireFrame != null)
                        return since < firePoseDuration ? upFireFrame : upRecoilFrame;
                    return since < firePoseDuration ? fireFrame : recoilFrame;
                case BarmanState.Pump: return since < pumpDuration * 0.6f ? pumpFrame : readyFrame;
                case BarmanState.Reload:
                {
                    // Son anda pompa kolu çekilir; öncesinde her fişek için sürme kareleri baştan oynar.
                    float pumpAt = reloadDuration - pumpDuration * 0.6f;
                    if (since >= pumpAt)
                        return pumpFrame;
                    if (reloadFrames == null || reloadFrames.Length == 0)
                        return readyFrame;
                    float perShell = pumpAt / Mathf.Max(1, shellsPerLoad);
                    return Stretch(reloadFrames, (since % perShell) / perShell);
                }
                default:
                    return Mathf.Abs(body.linearVelocity.x) > 0.1f ? Loop(gunWalkFrames, walkFps) : readyFrame;
            }
        }

        /// <summary>Kareleri sürenin tamamına yayar; son karede kalır.</summary>
        static Sprite Stretch(Sprite[] frames, float progress)
        {
            if (frames == null || frames.Length == 0)
                return null;
            return frames[Mathf.Clamp(Mathf.FloorToInt(progress * frames.Length), 0, frames.Length - 1)];
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
            Gizmos.color = HasBarrel ? new Color(0.7f, 0.45f, 0.2f) : new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireSphere(Eye, HasBarrel ? throwRange : gunRange);
        }
    }
}
