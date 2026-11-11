using System;
using System.Collections.Generic;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.UI;
using DesertGecko.Weapons;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Düşmandan alınan silah. Silah varken K ateş eder (W ile yukarı, W + yön ile çapraz); mermi bitince K boş
    /// silahı fırlatır. Yerdeyken S + K silahı mermisiyle birlikte bırakır. Silah yokken K kuyruğu fırlatır (TailAttack).
    /// Ölünce silah kaybolur. Silah, Gila'nın öndeki yumruğuna oturan ayrı bir sprite'tır: kabza yumruğun içinde
    /// kalır, namlu önden çıkar. Gila'nın kendi koşusu ve kolları değişmez; kolu göğsünde duran karelerde (duruş)
    /// koşudaki öne uzanmış ön kol da bu sprite'ta çizilidir, silah dururken de elde görünür.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader), typeof(PlayerHealth))]
    public class PlayerWeapon : MonoBehaviour
    {
        const float FireFrameTime = 0.06f, RecoilControlLock = 0.12f;

        [Tooltip("Boş silah fırlatma hızı (baktığı yöne, hafif yukarı)")]
        [SerializeField] Vector2 throwVelocity = new Vector2(12f, 4f);
        [Tooltip("Bırakılan silah bu süre geri alınmaz")]
        [SerializeField] float dropPickupDelay = 0.6f;
        [SerializeField] int sortingOrder = 12;

        public WeaponData Current { get; private set; }
        public int Ammo { get; private set; }
        public bool IsArmed => Current != null;
        public WeaponData.Aim CurrentAim { get; private set; }
        /// <summary>Yerdeki silahı alabilir mi (ölüyken alamaz).</summary>
        public bool CanPickUp => health.IsAlive;

        /// <summary>Silah ya da mermi sayısı değişti (arayüz).</summary>
        public event Action Changed;
        public event Action Fired;

        PlayerController controller;
        PlayerInputReader input;
        PlayerHealth health;
        Transform visualRoot;
        PlayerVisual visual;
        SpriteRenderer bodyRenderer;
        SpriteRenderer overlay;
        float nextFireAt;
        float firedAt = -10f;
        static int nextShotId; // saçma taneleri aynı atış numarasını taşır
        float cycleAt = -1f;         // pompa gibi mekanizmanın çalışacağı an
        float swingStartedAt = -10f; // yakın dövüş: sallamanın başladığı an
        bool swingStruck;            // bu sallamada vuruş yapıldı mı
        readonly Collider2D[] meleeHits = new Collider2D[16];
        readonly HashSet<IDamageable> meleeDamaged = new HashSet<IDamageable>();
        static readonly Color SparkColor = new Color32(0xFF, 0xD6, 0x5A, 0xFF);
        static readonly Color DustColor = new Color32(0xC8, 0x9A, 0x60, 0xFF);
        static readonly Color SplinterColor = new Color32(0x87, 0x5E, 0x47, 0xFF);

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<PlayerHealth>();
            visual = GetComponentInChildren<PlayerVisual>();
            if (visual != null)
            {
                visualRoot = visual.transform;
                bodyRenderer = visual.GetComponent<SpriteRenderer>();
            }
            CreateOverlay();
        }

        void Start() => WeaponHUD.Attach(this);

        void OnEnable()
        {
            health.Died += LoseWeapon;
            health.Respawned += LoseWeapon;
        }

        void OnDisable()
        {
            health.Died -= LoseWeapon;
            health.Respawned -= LoseWeapon;
        }

        void CreateOverlay()
        {
            if (visualRoot == null)
                return;
            // Görselin çocuğu: yön çevirme ve esneme ölçeğini gövdeyle birlikte alır.
            var go = new GameObject("ElSilahi");
            go.transform.SetParent(visualRoot, false);
            overlay = go.AddComponent<SpriteRenderer>();
            if (bodyRenderer != null)
            {
                overlay.sortingLayerID = bodyRenderer.sortingLayerID;
                overlay.sharedMaterial = bodyRenderer.sharedMaterial;
            }
            // Gövdenin önünde: kareler gövdeyle çakışan kabzayı zaten kırpılmış çizer (tools/gila_silah_ciz.py),
            // böylece yumruk kabzayı kavrar, namlu ve dururken öne uzanan kol görünür.
            overlay.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + 1 : sortingOrder;
            overlay.enabled = false;
        }

        void Update()
        {
            if (!IsArmed)
                return;
            CurrentAim = ReadAim();
            // Kuyruk tuşu silah varken ateş tuşudur; TailAttack silah varken bu tuşa dokunmaz.
            if (input.ConsumeTail())
                Use();
            if (IsArmed && Current.Mode == WeaponData.FireMode.Melee)
                UpdateSwing();
            if (IsArmed && cycleAt >= 0f && Time.time >= cycleAt)
                Cycle();
        }

        /// <summary>Atıştan sonra mekanizma: pompa sesi, boş kovan arkaya fırlar.</summary>
        void Cycle()
        {
            cycleAt = -1f;
            if (Current.CycleSound != null)
                OneShotSound.Play(Current.CycleSound, 0.6f);
            if (Current.EjectsShell)
            {
                int side = controller.FacingDirection;
                ShellCasing.Spawn(Chest + new Vector2(side * 0.3f, 0.1f),
                    new Vector2(-side * UnityEngine.Random.Range(1.5f, 3f), UnityEngine.Random.Range(3.5f, 5f)), Current.ShellColor);
            }
        }

        void LateUpdate() => UpdateOverlay();

        WeaponData.Aim ReadAim()
        {
            Vector2 move = input.Move;
            if (move.y < 0.5f)
                return WeaponData.Aim.Forward;
            return Mathf.Abs(move.x) > 0.3f ? WeaponData.Aim.Diagonal : WeaponData.Aim.Up;
        }

        /// <summary>Duvara yapışıkken yüz duvara dönük: silah duvardan uzağa ateş eder.</summary>
        int ShootSide => controller.State == PlayerController.MoveState.WallCling ? -controller.FacingDirection : controller.FacingDirection;

        Vector2 AimDirection(WeaponData.Aim aim, int side) => aim switch
        {
            WeaponData.Aim.Up => Vector2.up,
            WeaponData.Aim.Diagonal => new Vector2(side, 1f).normalized,
            _ => new Vector2(side, 0f),
        };

        Vector2 Chest => (Vector2)transform.position + Vector2.up * 0.5f;

        Vector2 MuzzlePosition(WeaponData.Aim aim, int side)
        {
            Vector2 local = Current.MuzzleFor(aim, BodyFrame);
            bool armShown = overlay != null && overlay.enabled && side == controller.FacingDirection;
            if (armShown && visualRoot != null)
                return visualRoot.TransformPoint(local); // gövdeyle birlikte çevrilir ve esner
            return Chest + AimDirection(aim, side) * 0.6f;
        }

        void Use()
        {
            if (!health.IsAlive || controller.MovementLocked || Time.time < nextFireAt)
                return;

            if (input.Move.y < -0.5f && controller.IsGrounded)
            {
                Drop();
                return;
            }
            if (Current.Mode == WeaponData.FireMode.Melee)
            {
                StartSwing();
                return;
            }
            if (Ammo <= 0)
            {
                ThrowEmpty();
                return;
            }
            Fire();
        }

        // --- Yakın dövüş (kazma) ---

        bool Swinging => Time.time - swingStartedAt < Current.WindupTime + Current.StrikeTime + Current.FollowTime;

        void StartSwing()
        {
            if (Ammo <= 0)
                return;
            swingStartedAt = Time.time;
            swingStruck = false;
            if (Current.SwingSound != null)
                OneShotSound.Play(Current.SwingSound, 0.5f);
            nextFireAt = Time.time + Current.FireInterval;
        }

        /// <summary>Kaldırma bitince vurur; sallama bitince dayanıklılığı biten kazma kırılır.</summary>
        void UpdateSwing()
        {
            float t = Time.time - swingStartedAt;
            if (!swingStruck && t >= Current.WindupTime && Swinging)
            {
                swingStruck = true;
                Strike();
            }
            if (Ammo <= 0 && swingStruck && !Swinging)
                Break();
        }

        /// <summary>
        /// Vuruş: Gila'nın önündeki kutuda kalan her düşmana bir kez yakın dövüş hasarı. Bir şeye değerse kıvılcım,
        /// boşa inerse önündeki yerden toz kalkar. Her sallama bir dayanıklılık harcar.
        /// </summary>
        void Strike()
        {
            int side = ShootSide;
            Vector2 area = Current.MeleeArea;
            Vector2 center = Chest + new Vector2(side * (area.x * 0.5f + 0.15f), -0.05f);
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask("Default", GameLayers.Enemy),
                useTriggers = false,
            };
            int count = Physics2D.OverlapBox(center, area, 0f, filter, meleeHits);
            meleeDamaged.Clear();
            int strike = ++nextShotId;
            for (int i = 0; i < count; i++)
            {
                var target = meleeHits[i].GetComponentInParent<IDamageable>();
                if (target == null || target is PlayerHealth || !target.IsAlive || !meleeDamaged.Add(target))
                    continue;
                target.TakeDamage(new DamageInfo(Current.Damage, new Vector2(side, 0f), DamageKind.Melee, strike));
                PixelBurst.Spawn(meleeHits[i].ClosestPoint(center), SparkColor, 7, 3.5f, 0.25f);
            }

            bool hit = meleeDamaged.Count > 0;
            if (!hit)
            {
                // Boşa inen kazma önündeki yere çarpar: toz ve taş kıvılcımı.
                Vector2 tip = center + new Vector2(side * area.x * 0.35f, 0f);
                RaycastHit2D ground = Physics2D.Raycast(tip, Vector2.down, 1.2f, GameLayers.GroundMask);
                if (ground.collider != null)
                {
                    PixelBurst.Spawn(ground.point, DustColor, 8, 2.5f, 0.4f);
                    PixelBurst.Spawn(ground.point, SparkColor, 3, 3f, 0.2f);
                }
            }
            if (Current.FireSound != null)
                OneShotSound.Play(Current.FireSound, hit ? Current.FireVolume : Current.FireVolume * 0.6f);
            if (hit)
                HitStop.Request(Current.HitStop); // kazma ete ve zırha oturur
            CameraShake.Add(hit ? Current.Shake : Current.Shake * 0.6f); // kazma bir şeye ya da yere iner

            Ammo--;
            firedAt = Time.time;
            Fired?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>Dayanıklılığı biten kazma kırılır: sap kıymıkları saçılır, el boşalır.</summary>
        void Break()
        {
            PixelBurst.Spawn(Chest + new Vector2(ShootSide * 0.6f, 0f), SplinterColor, 10, 3f, 0.5f);
            Clear();
        }

        /// <summary>Sallamanın karesi: 0 tutuş, 1 kaldırma, 2 vuruş, 3 takip.</summary>
        int SwingFrame()
        {
            float t = Time.time - swingStartedAt;
            if (t < Current.WindupTime)
                return 1;
            if (t < Current.WindupTime + Current.StrikeTime)
                return 2;
            if (t < Current.WindupTime + Current.StrikeTime + Current.FollowTime)
                return 3;
            return 0;
        }

        void Fire()
        {
            int side = ShootSide;
            Vector2 direction = AimDirection(CurrentAim, side);
            Vector2 muzzle = MuzzlePosition(CurrentAim, side);
            if (Current.Mode == WeaponData.FireMode.Thrown)
            {
                PlayerDynamite.Spawn(Current, Chest, muzzle, ThrowVelocityFor(CurrentAim, side), sortingOrder);
                if (Current.SwingSound != null)
                    OneShotSound.Play(Current.SwingSound, 0.5f);
            }
            else
                FireProjectiles(muzzle, direction);
            MuzzleFlash.Spawn(Current.MuzzleFlashFrames, muzzle, direction, sortingOrder + 1);
            if (Current.FireSound != null)
                OneShotSound.Play(Current.FireSound, Current.FireVolume);

            if (Current.Recoil > 0f)
                Kick(direction);
            if (Current.Shake > 0f)
                CameraShake.Add(Current.Shake);

            Ammo--;
            nextFireAt = Time.time + Current.FireInterval;
            firedAt = Time.time;
            if (Current.CycleSound != null || Current.EjectsShell)
                cycleAt = Time.time + Current.CycleDelay;
            Fired?.Invoke();
            if (Current.Consumable && Ammo <= 0)
            {
                Clear(); // son çubuk da atıldı: elde bir şey kalmaz
                return;
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Tek mermi ya da saçma: taneler nişan çizgisinin çevresine rastgele açılarla dağılır, hızları ve
        /// menzilleri biraz farklıdır. Yakında bulut sıkıdır (hepsi isabet eder), uzakta seyrekleşip söner.
        /// </summary>
        void FireProjectiles(Vector2 muzzle, Vector2 direction)
        {
            int count = Current.Pellets;
            if (count == 1 && Current.SpreadAngle <= 0f)
            {
                PlayerBullet.Spawn(Current, Chest, muzzle, direction, sortingOrder);
                return;
            }
            int shot = ++nextShotId;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float jitter = Mathf.Clamp01(Current.PelletJitter);
            for (int i = 0; i < count; i++)
            {
                // Taneleri koniye eşit yay, sonra biraz sars: ortası boş kalmasın, kenarlar düzgün çizgi olmasın.
                float t = count > 1 ? (i / (float)(count - 1)) * 2f - 1f : 0f;
                float angle = baseAngle + (t + UnityEngine.Random.Range(-0.35f, 0.35f)) * Current.SpreadAngle;
                Vector2 pelletDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                float speed = Current.BulletSpeed * UnityEngine.Random.Range(1f - jitter * 0.3f, 1f + jitter * 0.1f);
                float range = Current.BulletRange * UnityEngine.Random.Range(1f - jitter * 0.35f, 1f);
                PlayerBullet.Spawn(Current, Chest, muzzle, pelletDirection, sortingOrder, shot, speed, range);
            }
        }

        /// <summary>Atılan silahın hızı: ileri yay, çapraz daha dik, yukarı neredeyse dik (biraz öne).</summary>
        Vector2 ThrowVelocityFor(WeaponData.Aim aim, int side)
        {
            Vector2 v = Current.ThrowVelocity;
            float speed = v.magnitude;
            return aim switch
            {
                WeaponData.Aim.Up => new Vector2(side * speed * 0.15f, speed * 1.05f),
                WeaponData.Aim.Diagonal => new Vector2(side * speed * 0.62f, speed * 0.85f),
                _ => new Vector2(side * v.x, v.y),
            };
        }

        /// <summary>Geri tepme: Gila atışın tersine itilir. Havada küçük bir hız kazancı olarak da kullanılabilir.</summary>
        void Kick(Vector2 direction)
        {
            if (controller.State == PlayerController.MoveState.WallCling || controller.IsSwinging)
                return;
            Vector2 push = -direction * Current.Recoil;
            Vector2 velocity = new Vector2(push.x, controller.IsGrounded ? Mathf.Max(push.y, 1.5f) : push.y + 1f);
            controller.Knockback(velocity, RecoilControlLock);
        }

        void ThrowEmpty()
        {
            int side = ShootSide;
            Vector2 velocity = CurrentAim == WeaponData.Aim.Up
                ? new Vector2(side * 2f, throwVelocity.x)
                : CurrentAim == WeaponData.Aim.Diagonal
                    ? new Vector2(side * throwVelocity.x * 0.75f, throwVelocity.x * 0.75f)
                    : new Vector2(side * throwVelocity.x, throwVelocity.y);
            ThrownWeapon.Spawn(Current.Icon, Chest + new Vector2(side * 0.4f, 0.1f), velocity, Current.ThrownDamage, sortingOrder);
            nextFireAt = Time.time + 0.3f; // aynı basış kuyruğu da fırlatmasın
            Clear();
        }

        void Drop()
        {
            int side = controller.FacingDirection;
            WeaponPickup.Spawn(Current, Ammo, Chest + new Vector2(side * 0.5f, 0f), new Vector2(side * 2f, 3f), 0f, dropPickupDelay);
            nextFireAt = Time.time + 0.3f;
            Clear();
        }

        /// <summary>
        /// Yerdeki silahı almayı dener. Eli boşsa alır; aynı silahsa mermisini tamamlar (dolu ise almaz);
        /// başka silahsa ancak <paramref name="swap"/> ile elindekini bırakıp değiştirir.
        /// </summary>
        /// <returns>Yerdeki silah alındıysa (ya da mermisi aktarıldıysa) true.</returns>
        public bool TryTake(WeaponData weapon, int ammo, bool swap)
        {
            if (weapon == null || !health.IsAlive)
                return false;
            if (!IsArmed)
            {
                Equip(weapon, ammo);
                PlayPickup(weapon);
                return true;
            }
            if (weapon == Current)
            {
                if (Ammo >= Current.Ammo)
                    return false;
                Ammo = Mathf.Min(Current.Ammo, Ammo + ammo);
                Changed?.Invoke();
                PlayPickup(weapon);
                return true;
            }
            if (!swap)
                return false;
            int side = controller.FacingDirection;
            WeaponPickup.Spawn(Current, Ammo, Chest, new Vector2(-side * 2f, 3f), 0f, dropPickupDelay);
            Equip(weapon, ammo);
            PlayPickup(weapon);
            return true;
        }

        /// <summary>Silah ele geçti: kavrama sesi, arayüz ikonu sıçrar.</summary>
        public event Action PickedUp;

        void PlayPickup(WeaponData weapon)
        {
            if (weapon.PickupSound != null)
                OneShotSound.Play(weapon.PickupSound, 0.6f);
            PickedUp?.Invoke();
        }

        /// <summary>Yerdeki bu silahı almak için S gerekiyor mu (elde başka bir silah var).</summary>
        public bool NeedsSwapFor(WeaponData weapon) => IsArmed && weapon != Current && health.IsAlive;

        public void Equip(WeaponData weapon, int ammo)
        {
            Current = weapon;
            Ammo = Mathf.Clamp(ammo, 0, weapon != null ? weapon.Ammo : 0);
            nextFireAt = Time.time + 0.15f;
            firedAt = -10f;
            Changed?.Invoke();
        }

        void LoseWeapon() => Clear();

        void Clear()
        {
            cycleAt = -1f;
            if (Current == null)
                return;
            Current = null;
            Ammo = 0;
            Changed?.Invoke();
        }

        void UpdateOverlay()
        {
            if (overlay == null)
                return;

            Sprite sprite = null;
            if (IsArmed && ShowsArm())
            {
                Sprite[] frames = Current.FramesFor(CurrentAim, BodyFrame);
                if (frames != null && frames.Length > 0)
                {
                    float since = Time.time - firedAt;
                    int index = Current.Mode == WeaponData.FireMode.Melee
                        ? SwingFrame()
                        : since < FireFrameTime ? 1 : since < FireFrameTime + Current.RecoverTime ? 2 : 0;
                    sprite = frames[Mathf.Min(index, frames.Length - 1)];
                }
            }

            overlay.sprite = sprite;
            // Hasar sonrası yanıp sönme ve kamuflaj gövdeyle birlikte işlesin.
            bool bodyVisible = bodyRenderer == null || bodyRenderer.enabled;
            overlay.enabled = sprite != null && bodyVisible && health.IsAlive;
            if (bodyRenderer != null)
                overlay.color = new Color(1f, 1f, 1f, bodyRenderer.color.a);
        }

        /// <summary>Gövdenin şu anki karesi: silah bu karedeki yumruğun yerine oturur.</summary>
        Sprite BodyFrame => visual != null ? visual.BaseFrame : null;

        /// <summary>Duvarda ve dilde sallanırken silah çizilmez: o karelerde yumruk başka yerde.</summary>
        bool ShowsArm() =>
            controller.State != PlayerController.MoveState.WallCling && controller.State != PlayerController.MoveState.Swinging;
    }
}
