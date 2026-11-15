using System.Collections;
using System.Collections.Generic;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Bard: yarasa çağıran gitarist, iki fazlı. Kendisi dövüşmez, alanı yarasalarıyla tutar (cadı gibi).
    /// Gila'yı görünce çalmaya başlar ve bir daha susmaz: gitarın sesi yarasaları çağırır; yarasalar yukarıdaki
    /// karanlıktan uçarak gelir. Bir kısmı tepeden dinamit bırakır, bir kısmı hızla dalış yapar; Gila öldürene kadar
    /// saldırır. Sürü bitince Bard yenisini gönderir: insan fazında 3, canavar fazında 4 yarasa (maxBats).
    /// İnsan formu: Gila yaklaşınca geri çekilir, mesafeyi korur. Köşeye sıkışırsa ya da Gila dibine girerse
    /// gitarı savurup onu geri iter.
    /// Canı bitince ölmez, canavara dönüşür (dönüşürken darbe işlemez): dolu canla, daha kalabalık ve hızlı
    /// sürüyle, geri çekilmeden Gila'nın üstüne yürüyerek devam eder. Canavar ölünce yeni yarasa gelmez, sahadakiler
    /// öldürülene kadar saldırır.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar.
    /// </summary>
    public class Bard : EnemyBase
    {
        public enum BardState { None, BashWindup, BashStrike, BashRecover, Transform }

        [System.Serializable]
        public class Phase
        {
            [Tooltip("Bir sürüdeki dinamitçi yarasa sayısı")]
            public int bombers = 1;
            [Tooltip("Bir sürüdeki dalışçı yarasa sayısı")]
            public int divers = 1;
            [Tooltip("Her yeni sürüye eklenen yarasa")]
            public int growth = 1;
            [Tooltip("Bir sürüdeki en fazla yarasa")]
            public int maxBats = 4;
            [Tooltip("Sürü bittikten sonra yenisini çağırmadan önceki soluklanma")]
            public float resummonDelay = 2.2f;
            [Tooltip("Yarasaların hız çarpanı")]
            public float batSpeed = 1f;
            [Tooltip("Yarasanın kaç vuruşta öldüğü")]
            public int batHealth = 2;
            [Tooltip("Gitar savurmadan önceki gerilme: oyuncunun kaçma penceresi")]
            public float bashWindup = 0.35f;
            public float bashCooldown = 1.5f;
            public int bashDamage = 1;
            public float playFps = 9f;
        }

        [Header("Fazlar")]
        [SerializeField] Phase human = new Phase { bombers = 1, divers = 2, maxBats = 3 }; // insan: hep 3 yarasa
        [SerializeField] Phase monster = new Phase
        {
            bombers = 2, divers = 2, maxBats = 4, batHealth = 3, resummonDelay = 1f, batSpeed = 1.25f, bashWindup = 0.24f, bashCooldown = 0.9f, bashDamage = 2, playFps = 13f,
        };
        [Tooltip("Canavar formunun canı (insan formunun canı: maxHealth)")]
        [SerializeField] int monsterHealth = 6;
        [SerializeField] float transformDuration = 1.3f;
        [Tooltip("Canavar Gila'nın üstüne bu hızla yürür (oyuncu 5.5)")]
        [SerializeField] float monsterWalkSpeed = 2.8f;

        [Header("Yarasalar")]
        [SerializeField] BardBat batPrefab;
        [Tooltip("Gila'yı fark ettikten sonra ilk sürüye kadar geçen süre")]
        [SerializeField] float firstSummonDelay = 0.8f;
        [Tooltip("Yarasaların toplandığı yer: başının üstü")]
        [SerializeField] float summonHeight = 2.1f;
        [Tooltip("Yarasalar bu kadar yukarıdan (tavan alçaksa tavanın hemen altından) uçarak gelir")]
        [SerializeField] float entryHeight = 7f;
        [SerializeField] Color summonColor = new Color32(0x8E, 0x5A, 0xD6, 0xFF);

        [Header("Mesafe (insan formu)")]
        [Tooltip("Gila bundan yakınsa geri çekilir")]
        [SerializeField] float keepDistance = 4.5f;
        [Tooltip("Gila bundan uzaksa yaklaşır: yarasaları menzilde kalsın")]
        [SerializeField] float farDistance = 8.5f;
        [SerializeField] float retreatSpeed = 2.6f;

        [Header("Gitar savurma")]
        [Tooltip("Gila yatayda bu kadar yaklaşınca gitarı savurur")]
        [SerializeField] float bashRange = 1.7f;
        [Tooltip("Savurmanın uzandığı mesafe: gerilme sırasında bunun dışına çıkan kurtulur")]
        [SerializeField] float bashReach = 2.1f;
        [SerializeField] float bashHeight = 1.4f;
        [SerializeField] float bashStrikeDuration = 0.12f;
        [SerializeField] float bashRecoverDuration = 0.28f;

        [Header("Hasar")]
        [SerializeField] float hurtDuration = 0.4f;
        [Tooltip("Gövdeye göre göz hizası: görüş hattı buradan bakılır")]
        [SerializeField] float eyeHeight = 0.95f;
        [SerializeField] float targetHeight = 0.45f;

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip summonClip;
        [SerializeField] AudioClip bashClip;
        [SerializeField] AudioClip transformClip;

        [Header("Kareler: insan")]
        [SerializeField] Sprite[] idleFrames;
        [SerializeField] Sprite[] walkFrames;
        [SerializeField] Sprite[] playFrames;
        [SerializeField] Sprite[] hurtFrames;
        [SerializeField] Sprite[] victoryFrames;
        [Tooltip("Gerilme kareleri (bir ya da daha çok), sonra savurma ve toparlanma")]
        [SerializeField] Sprite[] bashFrames;
        [Header("Kareler: dönüşüm")]
        [SerializeField] Sprite[] transformFrames;
        [Header("Kareler: canavar")]
        [SerializeField] Sprite[] monsterIdleFrames;
        [SerializeField] Sprite[] monsterWalkFrames;
        [SerializeField] Sprite[] monsterPlayFrames;
        [SerializeField] Sprite[] monsterHurtFrames;
        [SerializeField] Sprite[] monsterVictoryFrames;
        [SerializeField] Sprite[] monsterBashFrames;
        [Header("Kareler: yenilgi")]
        [Tooltip("Ağır darbe: havadayken")]
        [SerializeField] Sprite[] fallFrames;
        [Tooltip("Dizlerin üstüne çöküş, yere yığılma, serilme")]
        [SerializeField] Sprite[] landFrames;
        [SerializeField] float idleFps = 2f;
        [SerializeField] float walkFps = 10f;
        [SerializeField] float victoryFps = 6f;
        [Tooltip("Yenilgi karelerinin süreleri (sn), fallFrames ve landFrames sırasıyla; ağır çekimde uzar")]
        [SerializeField] float[] defeatDurations = { 0.45f, 0.5f, 0.55f, 0.4f, 0.4f, 0.55f, 0.45f };

        public BardState State { get; private set; }
        public bool IsMonster { get; private set; }
        /// <summary>Gila'yı fark etti: çalıyor, yarasa çağırıyor.</summary>
        public bool IsPlaying { get; private set; }
        public int BatCount => bats.Count;

        readonly List<BardBat> bats = new List<BardBat>();
        readonly RaycastHit2D[] sightHits = new RaycastHit2D[4];
        ContactFilter2D solidFilter;
        Coroutine routine;
        float stateStartedAt, nextSummonAt, nextBashAt, hurtUntil, nextNoteAt;
        bool waveOut; // sahada sürü var: bitince yeniden çağırma süresi başlar
        int wavesSummoned;
        int lifeId; // dirilince artar: önceki ölümün yenilgi animasyonu gitar bırakmasın

        Phase Current => IsMonster ? monster : human;
        bool PlayerDown => Player != null && !Player.IsAlive;
        int DirectionToPlayer => Player.transform.position.x >= transform.position.x ? 1 : -1;
        Vector2 Eye => (Vector2)transform.position + Vector2.up * eyeHeight;
        Vector2 PlayerTarget => (Vector2)Player.transform.position + Vector2.up * targetHeight;

        // Devriyeyi EnemyBase yürütür; Gila'yı görünce kendi hareketini aşağıda yapar.
        protected override bool CanChase => false;
        protected override bool HoldPosition => IsPlaying || State != BardState.None || PlayerDown;
        protected override bool Invulnerable => State == BardState.Transform;
        protected override bool KeepsFooting => true;
        public override bool IgnoresEnemyFire => true;
        /// <summary>Gitar, Bard yere yığılınca yattığı yerde kalır.</summary>
        protected override bool DropsWeaponAfterDefeat => true;

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
            if (!isActiveAndEnabled)
                return;
            bats.RemoveAll(b => b == null || !b.IsAlive);
            UpdateEngagement();
            if (!IsPlaying || State != BardState.None)
                return;

            Summon();
            if (Time.time < hurtUntil || !IsGrounded())
                return;
            TurnTowards(DirectionToPlayer);
            if (Time.time >= nextBashAt && PlayerInBashRange(bashRange))
            {
                routine = StartCoroutine(BashRoutine());
                return;
            }
            Reposition();
        }

        void UpdateEngagement()
        {
            if (Player == null || !Player.IsAlive)
            {
                IsPlaying = false;
                return;
            }
            float distance = Vector2.Distance(PlayerTarget, Eye);
            if (!IsPlaying)
            {
                if (distance <= detectRange && SeesPlayer())
                    Engage();
            }
            else if (distance > loseRange)
            {
                IsPlaying = false;
            }
        }

        void Engage()
        {
            if (IsPlaying)
                return;
            IsPlaying = true;
            nextSummonAt = Mathf.Max(nextSummonAt, Time.time + firstSummonDelay);
        }

        /// <summary>Arada katı bir şey yok. Tek yönlü platformlar engel sayılmaz.</summary>
        bool SeesPlayer()
        {
            Vector2 path = PlayerTarget - Eye;
            int count = Physics2D.Raycast(Eye, path.normalized, solidFilter, sightHits, path.magnitude);
            for (int i = 0; i < count; i++)
                if (!sightHits[i].collider.usedByEffector)
                    return false;
            return true;
        }

        // --- Yarasalar ---

        void Summon()
        {
            if (bats.Count > 0)
            {
                waveOut = true;
                return;
            }
            if (waveOut)
            {
                // Sürü bitti: biraz soluklanıp yenisini çağırır.
                waveOut = false;
                nextSummonAt = Time.time + Current.resummonDelay;
            }
            if (batPrefab == null || Time.time < nextSummonAt)
                return;

            Phase phase = Current;
            // Her sürü bir öncekinden kalabalık: Gila yarasaları bitirdikçe Bard daha fazlasını gönderir.
            int baseCount = phase.bombers + phase.divers;
            int total = Mathf.Min(phase.maxBats, baseCount + phase.growth * wavesSummoned);
            int bombers = Mathf.Max(1, Mathf.RoundToInt(total * phase.bombers / (float)Mathf.Max(1, baseCount)));
            float ceiling = CeilingAbove();
            for (int i = 0; i < total; i++)
            {
                // Başının üstünde yelpaze: ortadan yanlara. Yukarıdaki karanlıktan, yelpazenin yanlarına açılarak gelir.
                float spread = total > 1 ? (i / (total - 1f) - 0.5f) * 2f : 0f;
                var gather = new Vector2(spread * 1.6f, summonHeight + (i % 2) * 0.5f);
                Vector2 entry = (Vector2)transform.position + new Vector2(spread * 3.5f, Mathf.Min(entryHeight, ceiling - 0.7f));
                BardBat bat = Instantiate(batPrefab, entry, Quaternion.identity);
                // Sırayla saldırırlar: hepsi aynı anda çullanmasın.
                bat.Summon(this, i < bombers ? BardBat.BatRole.Bomber : BardBat.BatRole.Diver, phase.batSpeed, 0.4f + i * 0.45f, spread, gather,
                    phase.batHealth);
                bats.Add(bat);
            }
            wavesSummoned++;
            waveOut = true;
            PlaySound(summonClip);
            PixelBurst.Spawn(GuitarPoint(), summonColor, 10, 4.5f, 0.4f);
        }

        /// <summary>Başının üstündeki tavana uzaklık (açık havada entryHeight).</summary>
        float CeilingAbove()
        {
            Vector2 head = (Vector2)transform.position + Vector2.up * (eyeHeight + 0.4f);
            RaycastHit2D hit = Physics2D.Raycast(head, Vector2.up, entryHeight + 1f, solidFilter.layerMask);
            return hit ? hit.point.y - transform.position.y : entryHeight + 1f;
        }

        void RemoveBats()
        {
            foreach (BardBat bat in bats)
                if (bat != null)
                    Destroy(bat.gameObject);
            bats.Clear();
            waveOut = false;
        }

        Vector2 GuitarPoint() => (Vector2)transform.position + new Vector2(0.4f * FacingDirection, 0.7f);

        // --- Hareket ---

        /// <summary>İnsan mesafeyi korur (yaklaşana geri çekilir, uzaklaşanı izler); canavar üstüne yürür.</summary>
        void Reposition()
        {
            int toPlayer = DirectionToPlayer;
            float dx = Mathf.Abs(Player.transform.position.x - transform.position.x);
            float velocity = 0f;
            if (IsMonster)
            {
                if (dx > bashRange * 0.8f && !IsBlocked(toPlayer))
                    velocity = toPlayer * monsterWalkSpeed;
            }
            else if (dx < keepDistance && !IsBlocked(-toPlayer))
            {
                velocity = -toPlayer * retreatSpeed; // yüzü Gila'ya dönük, geri geri
            }
            else if (dx > farDistance && !IsBlocked(toPlayer))
            {
                velocity = toPlayer * patrolSpeed;
            }
            body.linearVelocity = new Vector2(velocity, body.linearVelocity.y);
        }

        // --- Gitar savurma ---

        bool PlayerInBashRange(float reach)
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 to = Player.transform.position - transform.position;
            return Mathf.Abs(to.x) <= reach && to.y > -0.8f && to.y < bashHeight;
        }

        void Enter(BardState state)
        {
            State = state;
            stateStartedAt = Time.time;
        }

        IEnumerator BashRoutine()
        {
            Phase phase = Current;
            Enter(BardState.BashWindup);
            yield return new WaitForSeconds(phase.bashWindup);

            Enter(BardState.BashStrike);
            PlaySound(bashClip);
            // Gerilirken arkaya geçen ya da uzaklaşan Gila kurtulur.
            if (PlayerInBashRange(bashReach) && (Player.transform.position.x - transform.position.x) * FacingDirection > -0.2f)
            {
                Player.TakeDamage(phase.bashDamage, new Vector2(FacingDirection, 0f));
                CameraShake.AddAt(transform.position, 0.25f);
                PixelBurst.Spawn(GuitarPoint() + Vector2.right * (0.6f * FacingDirection), new Color32(0xF2, 0xE2, 0xB0, 0xFF), 7, 4f, 0.25f);
            }
            yield return new WaitForSeconds(bashStrikeDuration);

            Enter(BardState.BashRecover);
            yield return new WaitForSeconds(bashRecoverDuration);

            State = BardState.None;
            routine = null;
            nextBashAt = Time.time + phase.bashCooldown;
        }

        // --- Dönüşüm ---

        protected override int SecondWind()
        {
            if (IsMonster)
                return 0;
            CancelRoutine();
            routine = StartCoroutine(TransformRoutine());
            return monsterHealth;
        }

        IEnumerator TransformRoutine()
        {
            Enter(BardState.Transform);
            hurtUntil = 0f;
            PlaySound(transformClip);
            float burstAt = transformDuration * 0.55f; // dönüşümün doruk noktası
            yield return new WaitForSeconds(burstAt);
            CameraShake.AddAt(transform.position, 0.45f);
            PixelBurst.Spawn(box.bounds.center, summonColor, 22, 6f, 0.6f);
            PixelBurst.Spawn(box.bounds.center, new Color32(0xD8, 0x3A, 0x4A, 0xFF), 12, 4f, 0.5f);
            yield return new WaitForSeconds(transformDuration - burstAt);

            IsMonster = true;
            State = BardState.None;
            routine = null;
            IsPlaying = Player != null && Player.IsAlive;
            // Kükreyip hemen yeni sürüyü çağırır.
            waveOut = false;
            nextSummonAt = Time.time + (bats.Count == 0 ? 0.3f : monster.resummonDelay);
            nextBashAt = Time.time + 0.4f;
        }

        void CancelRoutine()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            State = BardState.None;
        }

        // Obje kapanınca coroutine de durur; durum takılı kalırsa Bard yerinde donardı.
        void OnDisable()
        {
            routine = null;
            State = BardState.None;
        }

        protected override void OnDamaged()
        {
            Engage(); // uzaktan vurulursa da çalmaya başlar
            // Gitarı savururken darbe onu durdurmaz; çalarken sendeler ama çalmayı bırakmaz.
            if (State == BardState.None)
                hurtUntil = Time.time + hurtDuration;
        }

        protected override void OnDied()
        {
            CancelRoutine();
            bats.Clear(); // sahadaki yarasalar öldürülene kadar saldırır; yenisi gelmez
            int knock = Player != null ? -DirectionToPlayer : -FacingDirection;
            // Epik yenilgi: yerinde ağır ağır çöker (BardDefeat). Kareler: ağır darbe, dizlerin üstüne çöküş (canavar),
            // dizlerin üstünde (insana döner), öne eğilme, yere yığılma, serilme, hareketsiz (ruh yükselir).
            var frames = new System.Collections.Generic.List<Sprite>();
            if (fallFrames != null)
                frames.AddRange(fallFrames);
            if (landFrames != null)
                frames.AddRange(landFrames);
            int fall = fallFrames != null ? fallFrames.Length : 0;
            BardDefeat defeat = BardDefeat.Spawn(transform.position, frames.ToArray(), defeatDurations, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, knock,
                formChange: IsMonster ? fall + 1 : -1, downFrame: frames.Count - 3, soulFrame: frames.Count - 2);
            int life = lifeId;
            defeat.Finished += position =>
            {
                if (life == lifeId) // bu arada oyuncu ölüp Bard dirildiyse eski ceset gitar bırakmaz
                    DropWeapon(position);
            };
        }

        public override void ResetEnemy()
        {
            CancelRoutine();
            RemoveBats();
            lifeId++;
            base.ResetEnemy();
            IsMonster = false;
            IsPlaying = false;
            wavesSummoned = 0;
            nextSummonAt = 0f;
            nextBashAt = 0f;
            hurtUntil = 0f;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RemoveBats();
        }

        void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        // --- Görsel ---

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;
            Notes();
        }

        /// <summary>Çalarken gitardan mor notalar saçılır: yarasaları çağıran ses görünür olsun.</summary>
        void Notes()
        {
            if (!IsPlaying || State != BardState.None || Time.time < nextNoteAt)
                return;
            nextNoteAt = Time.time + (IsMonster ? 0.16f : 0.28f);
            PixelBurst.Spawn(GuitarPoint(), summonColor, 1, 2.2f, 0.4f);
        }

        Sprite CurrentFrame()
        {
            float since = Time.time - stateStartedAt;
            Sprite[] bash = IsMonster ? monsterBashFrames : bashFrames;
            switch (State)
            {
                case BardState.Transform: return Stretch(transformFrames, since / transformDuration);
                // Son iki kare savurma ve toparlanma; öncekiler gerilme süresine yayılır (geriye çekme, yukarı kaldırma).
                case BardState.BashWindup: return WindupFrame(bash, since / Current.bashWindup);
                case BardState.BashStrike: return Pick(bash, bash != null ? bash.Length - 2 : 0);
                case BardState.BashRecover: return Pick(bash, bash != null ? bash.Length - 1 : 0);
            }
            if (PlayerDown)
                return Loop(IsMonster ? monsterVictoryFrames : victoryFrames, victoryFps);
            if (Time.time < hurtUntil)
                return Stretch(IsMonster ? monsterHurtFrames : hurtFrames, 1f - (hurtUntil - Time.time) / hurtDuration);
            bool moving = Mathf.Abs(body.linearVelocity.x) > 0.1f;
            if (moving)
                return Loop(IsMonster ? monsterWalkFrames : walkFrames, walkFps);
            if (IsPlaying)
                return Loop(IsMonster ? monsterPlayFrames : playFrames, Current.playFps);
            return Loop(IsMonster ? monsterIdleFrames : idleFrames, idleFps);
        }

        static Sprite Pick(Sprite[] frames, int index) =>
            frames == null || frames.Length == 0 ? null : frames[Mathf.Clamp(index, 0, frames.Length - 1)];

        static Sprite WindupFrame(Sprite[] frames, float progress)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int count = Mathf.Max(1, frames.Length - 2);
            return frames[Mathf.Clamp(Mathf.FloorToInt(progress * count), 0, count - 1)];
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
            Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeight, detectRange);
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireCube(transform.position + new Vector3(0f, bashHeight * 0.5f), new Vector3(bashReach * 2f, bashHeight));
        }
    }
}
