using System;
using System.Collections;
using System.Collections.Generic;
using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Environment;
using DesertGecko.Weapons;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Yakın mesafe dil saldırısı (J). Önündeki alanı tarar: düşmana hasar verir, böceği yakalayıp
    /// can yeniler. Baktığı yöne uzanır; W/yukarı basılıysa yukarı, W + yön ile çapraz yukarı uzanır.
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

        [Header("Silah çekme")]
        [Tooltip("Dil yerdeki silahı bu uzaklıktan yakalayıp ağza çeker")]
        [SerializeField] float pullRange = 5f;
        [Tooltip("Dilin yolu silaha bu kadar yaklaşırsa yakalar")]
        [SerializeField] float pullRadius = 0.7f;

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
        PlayerVisual visual;
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
            visual = GetComponentInChildren<PlayerVisual>();
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
            Vector2 direction = AimDirection();
            Vector2 mouth = MouthPosition();
            if (FindBugTarget(mouth, direction) != null || FindMagnetTarget(mouth, direction) != null)
                return true;
            int count = OverlapTongue(mouth, direction);
            for (int i = 0; i < count; i++)
            {
                if (WallBetween(mouth, hits[i].ClosestPoint(mouth)))
                    continue;
                if (hits[i].GetComponentInParent<Bug>() != null)
                    return true;
                var target = hits[i].GetComponentInParent<IDamageable>();
                if (target != null && !(target is PlayerHealth) && target.IsAlive)
                    return true;
            }
            return false;
        }

        // --- Duvar ve zemin: dil içinden geçmez ---

        readonly RaycastHit2D[] solidHits = new RaycastHit2D[8];

        ContactFilter2D SolidFilter => new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = GameLayers.GroundMask | LayerMask.GetMask("Default"), // kapılar ve kayalar da katı
            useTriggers = false,
        };

        /// <summary>Dilin bu yönde uzanabildiği boy: ilk duvara/zemine kadar, en fazla menzil.</summary>
        float Reach(Vector2 mouth, Vector2 direction)
        {
            int count = Physics2D.Raycast(mouth, direction, SolidFilter, solidHits, range);
            float reach = range;
            for (int i = 0; i < count; i++)
                if (!solidHits[i].collider.usedByEffector) // tek yönlü platformlar dili kesmez
                    reach = Mathf.Min(reach, solidHits[i].distance);
            return reach;
        }

        /// <summary>Ağızla hedef arasında duvar/zemin var mı (alt kattan üst kattaki düşmana vurulmasın).</summary>
        bool WallBetween(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f)
                return false;
            int count = Physics2D.Raycast(from, delta / distance, SolidFilter, solidHits, distance);
            for (int i = 0; i < count; i++)
                if (!solidHits[i].collider.usedByEffector)
                    return true;
            return false;
        }

        /// <summary>Dilin süpürdüğü kutudaki dil hedefleri (hits dizisine); kutu duvarda biter.</summary>
        int OverlapTongue(Vector2 mouth, Vector2 direction)
        {
            float reach = Reach(mouth, direction);
            if (reach < 0.05f)
                return 0;
            Vector2 center = mouth + direction * (reach * 0.5f);
            // Böcekler trigger; global "queriesHitTriggers" ayarından bağımsız olarak bulunsun.
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitMask, useTriggers = true };
            // Kutu dilin yönüne döner: çapraz/yukarı vuruşta da dil boyunca tarar.
            return Physics2D.OverlapBox(center, new Vector2(reach, width), AngleOf(direction), filter, hits);
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
            Vector2 aim = AimDirection();
            Vector2 mouth = MouthPosition();
            Bug bug = FindBugTarget(mouth, aim);
            if (bug != null)
            {
                StartCoroutine(CatchRoutine(bug, aim));
                return true;
            }
            ITongueMagnet magnet = FindMagnetTarget(mouth, aim);
            if (magnet != null)
            {
                StartCoroutine(MagnetRoutine(magnet));
                return true;
            }
            // Dilin önünde düşman yoksa yerdeki silaha uzanır: uzaktaki silahı almak için.
            WeaponPickup pickup = EnemyInTongueReach(mouth, aim) ? null : FindPickupTarget(mouth, aim);
            StartCoroutine(pickup != null ? PullRoutine(pickup) : AttackRoutine(aim));
            return true;
        }

        /// <summary>
        /// Dilin yolu bir böceğin görünmez yakalama çemberine değiyorsa o böcek (birden çoksa en yakını).
        /// Tam isabet gerekmez: dil çembere değince böceğe doğru kıvrılıp yapışır.
        /// </summary>
        Bug FindBugTarget(Vector2 mouth, Vector2 direction)
        {
            Bug best = null;
            float bestDistance = float.MaxValue;
            Vector2 tip = mouth + direction * Reach(mouth, direction);
            IReadOnlyList<Bug> bugs = Bug.Active;
            for (int i = 0; i < bugs.Count; i++)
            {
                Bug bug = bugs[i];
                Vector2 center = bug.transform.position;
                if (DistanceToSegment(center, mouth, tip) > bug.CatchRadius || WallBetween(mouth, center))
                    continue;
                float distance = Vector2.Distance(mouth, center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = bug;
                }
            }
            return best;
        }

        /// <summary>
        /// Havada süzülen düşman (ITongueMagnet): dilin yolu yakalama çemberine değiyorsa, böcekteki gibi dil ona
        /// kıvrılır. Birden çoksa en yakını.
        /// </summary>
        ITongueMagnet FindMagnetTarget(Vector2 mouth, Vector2 direction)
        {
            ITongueMagnet best = null;
            float bestDistance = float.MaxValue;
            Vector2 tip = mouth + direction * Reach(mouth, direction);
            IReadOnlyList<ITongueMagnet> magnets = TongueMagnets.Active;
            for (int i = 0; i < magnets.Count; i++)
            {
                ITongueMagnet magnet = magnets[i];
                if (magnet == null || !magnet.IsAlive)
                    continue;
                Vector2 center = magnet.TongueAnchor;
                if (DistanceToSegment(center, mouth, tip) > magnet.TongueCatchRadius || WallBetween(mouth, center))
                    continue;
                float distance = Vector2.Distance(mouth, center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = magnet;
                }
            }
            return best;
        }

        bool EnemyInTongueReach(Vector2 mouth, Vector2 direction)
        {
            if (FindMagnetTarget(mouth, direction) != null)
                return true;
            int count = OverlapTongue(mouth, direction);
            for (int i = 0; i < count; i++)
            {
                var target = hits[i].GetComponentInParent<IDamageable>();
                if (target != null && !(target is PlayerHealth) && target.IsAlive && !WallBetween(mouth, hits[i].ClosestPoint(mouth)))
                    return true;
            }
            return false;
        }

        /// <summary>Dilin uzun yolunun (pullRange) yakınındaki, arada duvar olmayan en yakın yerdeki silah.</summary>
        WeaponPickup FindPickupTarget(Vector2 mouth, Vector2 direction)
        {
            WeaponPickup best = null;
            float bestDistance = float.MaxValue;
            Vector2 tip = mouth + direction * pullRange;
            IReadOnlyList<WeaponPickup> pickups = WeaponPickup.Active;
            for (int i = 0; i < pickups.Count; i++)
            {
                WeaponPickup pickup = pickups[i];
                if (!pickup.CanBePulled)
                    continue;
                Vector2 center = pickup.transform.position;
                if (DistanceToSegment(center, mouth, tip) > pullRadius || WallBetween(mouth, center))
                    continue;
                float distance = Vector2.Distance(mouth, center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pickup;
                }
            }
            return best;
        }

        /// <summary>
        /// Silah çekme: dil yerdeki silaha uzanıp yapışır, silahla birlikte ağza sarılır; silah ele geçer
        /// (elde başka silah varsa o bırakılır). Böcek yakalamayla aynı ritim.
        /// </summary>
        IEnumerator PullRoutine(WeaponPickup pickup)
        {
            IsAttacking = true;
            readyAt = Time.time + cooldown;
            controller.MovementLocked = true;
            Attacked?.Invoke();
            pickup.BeginPull();

            Vector2 grab = pickup.transform.position;
            float extend = extendTime * Mathf.Max(1f, Vector2.Distance(MouthPosition(), grab) / range);
            float t = 0f;
            while (t < extend && pickup != null)
            {
                t += Time.deltaTime;
                Vector2 mouth = MouthPosition();
                ShowTongue(mouth, (grab - mouth).normalized, Vector2.Distance(mouth, grab) * Mathf.Clamp01(t / extend));
                yield return null;
            }

            t = 0f;
            while (t < BugSwallow.PullTime && pickup != null)
            {
                t += Time.deltaTime;
                Vector2 mouth = MouthPosition();
                Vector2 tip = Vector2.Lerp(grab, mouth, BugSwallow.PullCurve(Mathf.Clamp01(t / BugSwallow.PullTime)));
                pickup.transform.position = tip;
                ShowTongue(mouth, (tip - mouth).normalized, Vector2.Distance(mouth, tip));
                yield return null;
            }
            if (pickup != null)
                pickup.FinishPull(GetComponent<PlayerWeapon>());

            if (tongueRenderer != null)
                tongueRenderer.enabled = false;
            if (health.IsAlive) // ölüm sırasında kilidi PlayerHealth yönetir
                controller.MovementLocked = false;
            IsAttacking = false;
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return Vector2.Distance(point, a + ab * t);
        }

        /// <summary>
        /// Yukarı basılıysa dil havadaki düşmanlara uzanır: yukarı + yön çapraz (45°), sadece yukarı dimdik.
        /// Duvara yapışıkken yukarı tırmanmak demek; orada hep yatay.
        /// </summary>
        Vector2 AimDirection()
        {
            int side = AttackSide();
            Vector2 move = input.Move;
            if (controller.State == PlayerController.MoveState.WallCling || move.y < AimUpThreshold)
                return new Vector2(side, 0f);
            return Mathf.Abs(move.x) >= AimSideThreshold ? new Vector2(side, 1f).normalized : Vector2.up;
        }

        const float AimUpThreshold = 0.5f, AimSideThreshold = 0.3f;

        static float AngleOf(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

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
            if (latchedChest != null)
            {
                // Sandığın kilidi: dil yapışır, sandık açılana kadar orada kalıp çekiştirir.
                TreasureChest chest = latchedChest;
                latchedChest = null;
                yield return HoldChest(chest);
                FinishAttack();
                yield break;
            }
            float reach = Reach(MouthPosition(), direction);

            // Görsel: hızlıca uzan, kısa dur, geri çekil. Duvara değerse orada durur.
            float t = 0f;
            float retractStart = extendTime * 2f;
            while (t < lockDuration)
            {
                t += Time.deltaTime;
                float extension = t < extendTime ? t / extendTime
                    : t < retractStart ? 1f
                    : 1f - (t - retractStart) / Mathf.Max(0.01f, lockDuration - retractStart);
                ShowTongue(MouthPosition(), direction, reach * Mathf.Clamp01(extension));
                yield return null;
            }

            if (tongueRenderer != null)
                tongueRenderer.enabled = false;
            if (health.IsAlive) // ölüm sırasında kilidi PlayerHealth yönetir
                controller.MovementLocked = false;
            IsAttacking = false;
        }

        /// <summary>
        /// Böcek yakalama: dil böceğe doğru uzanıp yapışır, sonra böcekle birlikte ağza sarılır
        /// (BugSwallow ile aynı süre ve eğri: dilin ucu hep böceğin üstünde kalır).
        /// Dilin asıl yönündeki düşmanlar yine vurulur.
        /// </summary>
        IEnumerator CatchRoutine(Bug bug, Vector2 aim)
        {
            IsAttacking = true;
            readyAt = Time.time + cooldown;
            controller.MovementLocked = true;
            Attacked?.Invoke();

            HitTargets(MouthPosition(), aim, catchBugs: false);

            // Uzan: uç, süzülmeye devam eden böceği izler.
            Vector2 grab = bug.transform.position;
            float t = 0f;
            while (t < extendTime && bug.isActiveAndEnabled)
            {
                t += Time.deltaTime;
                grab = bug.transform.position;
                Vector2 mouth = MouthPosition();
                ShowTongue(mouth, (grab - mouth).normalized, Vector2.Distance(mouth, grab) * Mathf.Clamp01(t / extendTime));
                yield return null;
            }

            // Yapış ve sar: böcek ağza çekilirken dilin ucu onunla gelir.
            if (bug.isActiveAndEnabled)
                bug.Catch(health, MouthPosition());
            t = 0f;
            while (t < BugSwallow.PullTime)
            {
                t += Time.deltaTime;
                Vector2 mouth = MouthPosition();
                Vector2 tip = Vector2.Lerp(grab, mouth, BugSwallow.PullCurve(Mathf.Clamp01(t / BugSwallow.PullTime)));
                ShowTongue(mouth, (tip - mouth).normalized, Vector2.Distance(mouth, tip));
                yield return null;
            }

            if (tongueRenderer != null)
                tongueRenderer.enabled = false;
            if (health.IsAlive) // ölüm sırasında kilidi PlayerHealth yönetir
                controller.MovementLocked = false;
            IsAttacking = false;
        }

        /// <summary>
        /// Havadaki düşmana vuruş: dil gövdesine doğru kıvrılıp uzanır (süzülürken ucu onu izler), değince vurur,
        /// kısa tutup geri sarılır. Böcek yakalamayla aynı his; düz yoldaki öbür hedefler bu vuruşta vurulmaz.
        /// </summary>
        IEnumerator MagnetRoutine(ITongueMagnet target)
        {
            IsAttacking = true;
            readyAt = Time.time + cooldown;
            controller.MovementLocked = true;
            Attacked?.Invoke();

            float reachTime = extendTime * 2f;
            Vector2 grab = target.TongueAnchor;
            float t = 0f;
            while (t < reachTime)
            {
                t += Time.deltaTime;
                if (target.IsAlive)
                    grab = target.TongueAnchor;
                Vector2 mouth = MouthPosition();
                ShowTongue(mouth, (grab - mouth).normalized, Vector2.Distance(mouth, grab) * Mathf.Clamp01(t / reachTime));
                yield return null;
            }

            if (target.IsAlive)
            {
                Vector2 mouth = MouthPosition();
                target.TakeDamage(new DamageInfo(damage, new Vector2(grab.x >= mouth.x ? 1f : -1f, 0f), DamageKind.Tongue));
            }

            float retractTime = extendTime * 3f;
            t = 0f;
            while (t < retractTime)
            {
                t += Time.deltaTime;
                Vector2 mouth = MouthPosition();
                Vector2 tip = Vector2.Lerp(grab, mouth, Mathf.Clamp01(t / retractTime));
                ShowTongue(mouth, (tip - mouth).normalized, Vector2.Distance(mouth, tip));
                yield return null;
            }
            FinishAttack();
        }

        TreasureChest latchedChest; // bu saldırıda dilin açmaya başladığı sandık

        /// <summary>
        /// Dil kilide uzanıp yapışır; sandık açılana kadar gerilip gevşeyerek çekiştirir, Gila da asılır (gövdesi
        /// geriye yaslanıp öne gelir). Vurulursa bırakır, sandık yine de açılır. Sonunda dil hızla geri sarılır.
        /// </summary>
        IEnumerator HoldChest(TreasureChest chest)
        {
            float until = Time.time + chest.TeaseDuration;
            bool guarded = health.IsInvincible;
            Transform body = visual != null ? visual.transform : null;
            Vector3 bodyHome = body != null ? body.localPosition : Vector3.zero;

            float extend = extendTime * 1.5f;
            for (float t = 0f; t < extend; t += Time.deltaTime)
            {
                Vector2 mouth = MouthPosition();
                Vector2 to = chest.LockPoint - mouth;
                ShowTongue(mouth, to.normalized, to.magnitude * Mathf.Clamp01(t / extend));
                yield return null;
            }

            while (Time.time < until && chest != null && health.IsAlive && (guarded || !health.IsInvincible))
            {
                // Çekiştirme: hızlı küçük titreşim ve arada bir sert asılma (dil gerilir, Gila geriye yaslanır).
                bool yank = Mathf.Sin(Time.time * 5.5f) > 0.55f;
                float wiggle = Mathf.Sin(Time.time * 26f) * 0.05f;
                Vector2 mouth = MouthPosition();
                Vector2 to = chest.LockPoint - mouth;
                ShowTongue(mouth, to.normalized, to.magnitude + wiggle - (yank ? 0.06f : 0f));
                if (body != null)
                    body.localPosition = bodyHome + Vector3.left * (AttackSide() * (yank ? 1f : 0f) / 32f);
                yield return null;
            }
            if (body != null)
                body.localPosition = bodyHome;

            // Geri sarıl.
            Vector2 end = chest != null ? chest.LockPoint : MouthPosition();
            for (float t = 0f; t < 0.08f; t += Time.deltaTime)
            {
                Vector2 mouth = MouthPosition();
                Vector2 to = end - mouth;
                ShowTongue(mouth, to.normalized, to.magnitude * (1f - t / 0.08f));
                yield return null;
            }
        }

        void FinishAttack()
        {
            if (tongueRenderer != null)
                tongueRenderer.enabled = false;
            if (health.IsAlive) // ölüm sırasında kilidi PlayerHealth yönetir
                controller.MovementLocked = false;
            IsAttacking = false;
        }

        void HitTargets(Vector2 origin, Vector2 direction, bool catchBugs = true)
        {
            alreadyHit.Clear();
            latchedChest = null;
            int count = OverlapTongue(origin, direction);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                if (WallBetween(origin, hit.ClosestPoint(origin)))
                    continue; // zeminin/duvarın öbür yanında: dil oraya ulaşmaz
                var bug = hit.GetComponentInParent<Bug>();
                if (bug != null)
                {
                    if (catchBugs && alreadyHit.Add(bug))
                        bug.Catch(health, origin); // dilin çıktığı yere, ağza çekilir
                    continue;
                }

                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || target is PlayerHealth || !target.IsAlive || !alreadyHit.Add((Component)target))
                    continue;

                // Dikenli düşmana dil değerse diken Gila'nın canını yakar; darbe dikenlerden seker.
                if (target is IPrickly prickly && prickly.PricksTongue)
                {
                    float away = Mathf.Sign(transform.position.x - ((Component)target).transform.position.x);
                    health.TakeDamage(prickly.PrickDamage, new Vector2(away == 0f ? -AttackSide() : away, 0f));
                }
                bool landed = target.TakeDamage(new DamageInfo(damage, direction, DamageKind.Tongue));
                if (landed && target is TreasureChest chest)
                    latchedChest = chest;
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
