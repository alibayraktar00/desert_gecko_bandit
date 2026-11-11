using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Diken Mutantı: sırtı yüzlerce dikenle kaplı, iri ve hantal bir çöl kirpisi.
    /// Ağır adımlarla devriye gezer. Gila'yı görünce çömelir, diken yığını dikilip titrer (uyarı + gerilme sesi),
    /// sonra düz bir hat üzerinde yaylım ateşi açar. Atış ilerledikçe sırtı dolu -> yarı -> kel olur:
    /// cephanesi bir bar olmadan karakterin kendisinden okunur. İlk yaylımdan sonra yarı dikenle kısa bir nefes alır.
    /// Dikenleri bitince tükenir: dili dışarıda soluklanır. Tek savunmasız anı budur; dikenliyken her darbe seker.
    /// Süre bitince "Fııışş" ile dikenler yeniden çıkar.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar, dikenler de o yöne uçar.
    /// </summary>
    public class SpikeMutant : EnemyBase, IPrickly
    {
        public enum SpikeState { Patrol, Crouch, Bristle, Volley, Reload, Exhausted, Regrow }
        public enum SpikePhase { Full, Half, Bald }

        [Header("Algılama")]
        [SerializeField] float sightRange = 8f;
        [SerializeField] float sightHeight = 2f;
        [Tooltip("Dikenler yeniden çıktıktan sonra yeni yaylıma kadar bekleme")]
        [SerializeField] float attackCooldown = 1.5f;

        [Header("Uyarı")]
        [SerializeField] float crouchDuration = 0.45f;
        [Tooltip("Diken yığını titrer: oyuncunun saklanacak yer arama süresi")]
        [SerializeField] float bristleDuration = 0.9f;
        [SerializeField] float shakeAmount = 1f / 32f;

        [Header("Yaylım ateşi")]
        [SerializeField] EnemyBullet spikePrefab;
        [Tooltip("İki yaylım: ilki dolu -> yarı, ikincisi yarı -> kel")]
        [SerializeField] int volleyCount = 2;
        [Tooltip("Her yaylımdaki salvo sayısı; salvolar arasında üstünden atlanacak boşluk var")]
        [SerializeField] int salvosPerVolley = 2;
        [SerializeField] int spikesPerSalvo = 3;
        [SerializeField] float spikeInterval = 0.12f;
        [SerializeField] float salvoGap = 0.5f;
        [Tooltip("Gövdeye göre diken çıkış noktası, sağa bakarken")]
        [SerializeField] Vector2 muzzleOffset = new Vector2(0.55f, 0.62f);
        [SerializeField] float muzzleJitter = 0.08f;
        [Tooltip("Her atışta geri tepme: kirpi azar azar geriye kayar")]
        [SerializeField] float recoilSpeed = 1.6f;
        [Tooltip("İki yaylım arasında yarı dikenle soluklanma")]
        [SerializeField] float reloadDuration = 0.6f;

        [Header("Dikenler")]
        [Tooltip("Dikenliyken dil atan Gila'nın kaybettiği can (yarım kalp = 1)")]
        [SerializeField] int prickDamage = 1;

        [Header("Tükenme")]
        [SerializeField] float exhaustedDuration = 3.5f;
        [SerializeField] float regrowDuration = 0.8f;
        [SerializeField] float hurtDuration = 0.35f;
        [SerializeField] Color sweatColor = new Color32(0xA8, 0xD8, 0xF0, 0xFF);
        [SerializeField] Color sparkColor = new Color32(0xD8, 0xDC, 0xE6, 0xFF);
        [SerializeField] Color dustColor = new Color32(0x8A, 0x62, 0x40, 0xFF);

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip bristleClip;
        [SerializeField] AudioClip shotClip;
        [SerializeField] AudioClip regrowClip;

        [Header("Kareler (fps)")]
        [SerializeField] float idleFps = 5f;
        [SerializeField] float walkFps = 7f;
        [SerializeField] float bristleFps = 18f;
        [SerializeField] float exhaustedFps = 8f;

        [Header("Kareler")]
        [SerializeField] Sprite[] idleFrames;
        [SerializeField] Sprite[] walkFrames;
        [SerializeField] Sprite[] crouchFrames;
        [SerializeField] Sprite[] bristleFrames;
        [Tooltip("Dolu -> yarı -> kel: kare, kalan diken oranına göre seçilir")]
        [SerializeField] Sprite[] volleyFrames;
        [SerializeField] Sprite[] halfIdleFrames;
        [SerializeField] Sprite[] exhaustedFrames;
        [SerializeField] Sprite[] regrowFrames;
        [SerializeField] Sprite[] hurtFrames;
        [SerializeField] Sprite[] fullDefeatFrames;
        [SerializeField] Sprite[] halfDefeatFrames;
        [SerializeField] Sprite[] baldDefeatFrames;

        public SpikeState State { get; private set; } = SpikeState.Patrol;
        public int SpikesLeft { get; private set; }
        int TotalSpikes => volleyCount * salvosPerVolley * spikesPerSalvo;
        int SpikesPerVolley => salvosPerVolley * spikesPerSalvo;

        public SpikePhase Phase
        {
            get
            {
                if (SpikesLeft <= 0)
                    return SpikePhase.Bald;
                return SpikesLeft * 2 > TotalSpikes ? SpikePhase.Full : SpikePhase.Half;
            }
        }

        /// <summary>Dikenleri varken dil atmak Gila'nın canını yakar: ondan kaçılır, kelleşince vurulur.</summary>
        public bool PricksTongue => IsAlive && State != SpikeState.Exhausted;
        public int PrickDamage => prickDamage;

        /// <summary>Dikenleri varken (ya da yeniden çıkarken) her darbe seker.</summary>
        protected override bool Invulnerable => State != SpikeState.Exhausted;
        /// <summary>
        /// Dikenler dili, mermiyi, saçmayı ve dikeni sektirir. Patlama ve kazma dikenlerin arasından geçer:
        /// hem vurur hem dikenleri kırıp mutantı kel bırakır.
        /// </summary>
        protected override bool Deflects(in DamageInfo hit) =>
            Invulnerable && hit.Kind != DamageKind.Explosion && hit.Kind != DamageKind.Melee;
        protected override bool CanChase => false;
        /// <summary>İri ve ağır: dil darbeleri onu uçurumdan itemez.</summary>
        protected override bool KeepsFooting => true;
        protected override bool HoldPosition => State != SpikeState.Patrol;

        readonly RaycastHit2D[] sightHits = new RaycastHit2D[4];
        ContactFilter2D solidFilter;
        Vector3 visualHome;
        int spikyContactDamage;
        int firedThisVolley;
        float stateStartedAt, stateEndsAt, nextAttackAt, nextShotAt, hurtUntil, nextSweatAt, recoilUntil;

        protected override void Awake()
        {
            base.Awake();
            spikyContactDamage = contactDamage;
            SpikesLeft = TotalSpikes;
            if (spriteRenderer != null)
                visualHome = spriteRenderer.transform.localPosition;
            solidFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | LayerMask.GetMask("Default"),
                useTriggers = false,
            };
        }

        void Enter(SpikeState state, float duration = 0f)
        {
            State = state;
            stateStartedAt = Time.time;
            stateEndsAt = Time.time + duration;
            // Kelken dikeni yok: çarpmak can yakmaz, yanına gidip vurulabilsin.
            contactDamage = state == SpikeState.Exhausted ? 0 : spikyContactDamage;
        }

        int DirectionToPlayer => Player != null && Player.transform.position.x >= transform.position.x ? 1 : -1;

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled)
                return;

            bool timeUp = Time.time >= stateEndsAt;
            switch (State)
            {
                case SpikeState.Patrol:
                    if (SeesPlayer())
                    {
                        TurnTowards(DirectionToPlayer);
                        Enter(SpikeState.Crouch, crouchDuration);
                    }
                    break;
                case SpikeState.Crouch:
                    if (timeUp)
                    {
                        Play(bristleClip);
                        Enter(SpikeState.Bristle, bristleDuration);
                    }
                    break;
                case SpikeState.Bristle:
                    if (timeUp)
                        BeginVolley();
                    break;
                case SpikeState.Volley:
                    Volley();
                    break;
                case SpikeState.Reload:
                    if (timeUp)
                        BeginVolley();
                    break;
                case SpikeState.Exhausted:
                    Sweat();
                    if (timeUp)
                    {
                        Play(regrowClip);
                        Enter(SpikeState.Regrow, regrowDuration);
                    }
                    break;
                case SpikeState.Regrow:
                    if (timeUp)
                    {
                        SpikesLeft = TotalSpikes;
                        nextAttackAt = Time.time + attackCooldown;
                        Enter(SpikeState.Patrol);
                    }
                    break;
            }
        }

        bool SeesPlayer()
        {
            if (Time.time < nextAttackAt || !IsGrounded() || Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = Player.transform.position - transform.position;
            if (Mathf.Abs(toPlayer.x) > sightRange || Mathf.Abs(toPlayer.y) > sightHeight)
                return false;
            Vector2 eye = (Vector2)transform.position + Vector2.up * 0.6f;
            int count = Physics2D.Raycast(eye, Vector2.right * Mathf.Sign(toPlayer.x), solidFilter, sightHits, Mathf.Abs(toPlayer.x));
            for (int i = 0; i < count; i++)
                if (!sightHits[i].collider.usedByEffector) // tek yönlü platformlar görüşü kesmez
                    return false;
            return true;
        }

        void BeginVolley()
        {
            // Yaylım arasında Gila arkasına geçtiyse döner.
            if (Player != null && Player.IsAlive)
                TurnTowards(DirectionToPlayer);
            firedThisVolley = 0;
            nextShotAt = Time.time;
            Enter(SpikeState.Volley);
        }

        void Volley()
        {
            if (Time.time < recoilUntil && !IsBlocked(-FacingDirection))
                body.linearVelocity = new Vector2(-FacingDirection * recoilSpeed, body.linearVelocity.y);

            if (Time.time < nextShotAt)
                return;
            Fire();
            firedThisVolley++;
            bool salvoDone = firedThisVolley % spikesPerSalvo == 0;
            nextShotAt = Time.time + (salvoDone ? salvoGap : spikeInterval);

            if (SpikesLeft <= 0)
            {
                Enter(SpikeState.Exhausted, exhaustedDuration);
                PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 1.1f, sweatColor, 6, 2f, 0.4f);
            }
            else if (firedThisVolley >= SpikesPerVolley)
                Enter(SpikeState.Reload, reloadDuration);
        }

        void Fire()
        {
            SpikesLeft--;
            Vector2 origin = transform.position;
            Vector2 muzzle = origin + new Vector2(muzzleOffset.x * FacingDirection, muzzleOffset.y + Random.Range(-muzzleJitter, muzzleJitter));
            if (spikePrefab != null)
            {
                EnemyBullet spike = Instantiate(spikePrefab, muzzle, Quaternion.identity);
                spike.Launch(FacingDirection, new Vector2(origin.x, muzzle.y));
            }
            PixelBurst.Spawn(muzzle, sparkColor, 2, 2f, 0.15f);
            recoilUntil = Time.time + spikeInterval * 0.6f;
            Play(shotClip);
        }

        void Sweat()
        {
            if (Time.time < nextSweatAt)
                return;
            nextSweatAt = Time.time + 0.45f;
            PixelBurst.Spawn((Vector2)transform.position + new Vector2(FacingDirection * 0.3f, 1.2f), sweatColor, 2, 1.2f, 0.35f);
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        protected override void OnBlocked()
        {
            PixelBurst.Spawn(box.bounds.center + Vector3.up * 0.2f, sparkColor, 5, 3.5f, 0.25f); // dikenlerden seken darbe
        }

        protected override void OnDamaged()
        {
            // Acıyla öne sendeler; soluklanma süresi uzamaz, Gila hızlı davranmalı.
            hurtUntil = Time.time + hurtDuration;
            if (State != SpikeState.Exhausted && (LastHit.Kind == DamageKind.Explosion || LastHit.Kind == DamageKind.Melee))
            {
                // Dikenler kırılıp saçılır: yaylım yarıda kalır, mutant kel ve savunmasız kalır.
                SpikesLeft = 0;
                PixelBurst.Spawn(box.bounds.center + Vector3.up * 0.4f, sparkColor, 14, 4.5f, 0.4f);
                PixelBurst.Spawn(box.bounds.center + Vector3.up * 0.4f, dustColor, 8, 3f, 0.4f);
                Enter(SpikeState.Exhausted, exhaustedDuration);
            }
        }

        /// <summary>Diken demeti, mutant kapaklanıp toza karışınca yattığı yerde kalır.</summary>
        protected override float WeaponDropDelay
        {
            get
            {
                Sprite[] frames = Phase == SpikePhase.Full ? fullDefeatFrames : Phase == SpikePhase.Half ? halfDefeatFrames : baldDefeatFrames;
                return (frames != null ? frames.Length : 0) * 0.09f + 1.0f;
            }
        }

        protected override void OnDied()
        {
            Sprite[] frames = Phase == SpikePhase.Full ? fullDefeatFrames
                : Phase == SpikePhase.Half ? halfDefeatFrames
                : baldDefeatFrames;
            ArmadilloDefeat.Spawn(transform.position, frames, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, dustColor);
        }

        public override void ResetEnemy()
        {
            base.ResetEnemy();
            SpikesLeft = TotalSpikes;
            nextAttackAt = 0f;
            hurtUntil = 0f;
            recoilUntil = 0f;
            Enter(SpikeState.Patrol);
        }

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;

            // Diken yığını titrer: gövde hızla bir sağa bir sola kayar.
            Vector3 offset = Vector3.zero;
            if (State == SpikeState.Bristle)
                offset.x = (Mathf.FloorToInt(Time.time * 30f) % 2 == 0 ? 1f : -1f) * shakeAmount;
            spriteRenderer.transform.localPosition = visualHome + offset;
        }

        Sprite CurrentFrame()
        {
            if (Time.time < hurtUntil)
                return Loop(hurtFrames, 10f);
            switch (State)
            {
                case SpikeState.Crouch: return Once(crouchFrames, crouchDuration);
                case SpikeState.Bristle: return Loop(bristleFrames, bristleFps);
                case SpikeState.Volley: return VolleyFrame();
                case SpikeState.Reload: return Loop(halfIdleFrames, idleFps);
                case SpikeState.Exhausted: return Loop(exhaustedFrames, exhaustedFps);
                case SpikeState.Regrow: return Once(regrowFrames, regrowDuration);
                default:
                    return Mathf.Abs(body.linearVelocity.x) > 0.1f ? Loop(walkFrames, walkFps) : Loop(idleFrames, idleFps);
            }
        }

        /// <summary>Yaylım karesi atılan diken oranından seçilir: sırt atış boyunca kelleşir.</summary>
        Sprite VolleyFrame()
        {
            if (volleyFrames == null || volleyFrames.Length == 0)
                return null;
            float spent = 1f - (float)SpikesLeft / Mathf.Max(1, TotalSpikes);
            int i = Mathf.RoundToInt(spent * (volleyFrames.Length - 1));
            return volleyFrames[Mathf.Clamp(i, 0, volleyFrames.Length - 1)];
        }

        Sprite Once(Sprite[] frames, float duration)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int i = Mathf.FloorToInt((Time.time - stateStartedAt) / duration * frames.Length);
            return frames[Mathf.Clamp(i, 0, frames.Length - 1)];
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
            Gizmos.color = new Color(0.3f, 0.3f, 0.35f, 0.7f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * sightHeight * 0.5f, new Vector3(sightRange * 2f, sightHeight));
        }
    }
}
