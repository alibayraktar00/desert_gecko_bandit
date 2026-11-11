using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Kamuflajlı Çalı Faresi: sıradan, hareketsiz bir çalı topu gibi durur. Gila üstüne gelince onu geri
    /// fırlatır, çalıyı patlatıp çıkar ve peşine düşer; bir daha çalıya giremez. Yetişince bıçaklar, sonra
    /// kısa süre soluklanır. Dil darbelerini iki kez bıçağıyla savuşturup karşı saldırır, üçüncüde ölür.
    /// Kuyruk savuşturulamaz. Atılırken oyuncunun önünde durur, içinden geçmez.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar.
    /// </summary>
    public class TumbleweedMouse : EnemyBase
    {
        public enum MouseState { Disguised, Reveal, Chase, Stab, Fatigue }

        [Header("Gizlenme")]
        [Tooltip("Oyuncu çalının bu kadar içine girince tetiklenir")]
        [SerializeField] float triggerHalfWidth = 0.7f;
        [SerializeField] float triggerHeight = 1.1f;
        [Tooltip("Zemindeki sıradan çalılar da toprağa bu kadar gömülü")]
        [SerializeField] float disguiseSink = 0.1f;

        [Header("Ortaya çıkış")]
        [SerializeField] Vector2 throwVelocity = new Vector2(6.5f, 6f);
        [SerializeField] float throwControlLock = 0.25f;
        [SerializeField] float revealDuration = 0.45f;
        [SerializeField] float revealHop = 6f;
        [SerializeField] Color twigColor = new Color32(0xB8, 0x94, 0x56, 0xFF);

        [Header("Kovalama")]
        [SerializeField] float chaseRunSpeed = 6f;
        [Tooltip("Oyuncuya bu kadar kala durur: üst üste binmesin")]
        [SerializeField] float stopDistance = 1f;

        [Header("Bıçak")]
        [SerializeField] float stabDuration = 0.65f;
        [SerializeField] float stabInterval = 0.2f;
        [SerializeField] float stabReach = 1.5f;
        [SerializeField] float stabHeight = 1.2f;
        [SerializeField] int stabDamage = 1;
        [SerializeField] float stabFps = 14f;
        [SerializeField] float runFps = 16f;
        [SerializeField] Color stabColor = new Color32(0xE8, 0xE8, 0xF0, 0xFF);

        [Header("Savuşturma")]
        [Tooltip("Bu kadar dil darbesi bıçakla savuşturulur, sonrakinde vurulur")]
        [SerializeField] int parryCount = 2;
        [Tooltip("Bundan güçlü darbeler (kuyruk) savuşturulamaz")]
        [SerializeField] int maxParriedDamage = 1;
        [SerializeField] float parryPoseTime = 0.2f;

        [Header("Yorgunluk")]
        [SerializeField] float fatigueDuration = 1f;
        [SerializeField] float fatigueFps = 4f;

        [Header("Kareler")]
        [SerializeField] Sprite disguiseFrame;
        [SerializeField] Sprite[] revealFrames;
        [SerializeField] Sprite jumpFrame;
        [SerializeField] Sprite[] runFrames;
        [SerializeField] Sprite[] stabFrames;
        [SerializeField] Sprite[] fatigueFrames;
        [SerializeField] Sprite parryFrame;
        [SerializeField] Sprite[] defeatFrames;

        public MouseState State { get; private set; } = MouseState.Disguised;
        public bool IsDisguised => State == MouseState.Disguised;
        public int ParriesLeft { get; private set; }

        protected override bool CanChase => false;
        protected override bool HoldPosition => true; // kendi hareketini aşağıda yapar

        float stateStartedAt, stateEndsAt, nextStabAt, parryPoseUntil;
        float visualBaseY;
        bool isRevealed;

        protected override void Awake()
        {
            base.Awake();
            contactDamage = 0; // sadece bıçak can yakar
            ParriesLeft = parryCount;
            if (spriteRenderer != null)
                visualBaseY = spriteRenderer.transform.localPosition.y;
        }

        void Enter(MouseState state, float duration)
        {
            State = state;
            stateStartedAt = Time.time;
            stateEndsAt = Time.time + duration;
        }

        int DirectionToPlayer => Player != null && Player.transform.position.x >= transform.position.x ? 1 : -1;

        bool PlayerWithin(float halfWidth, float height)
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = Player.transform.position - transform.position;
            return Mathf.Abs(toPlayer.x) <= halfWidth && Mathf.Abs(toPlayer.y) <= height;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled)
                return;

            bool timeUp = Time.time >= stateEndsAt;
            switch (State)
            {
                case MouseState.Disguised:
                    if (PlayerWithin(triggerHalfWidth, triggerHeight))
                    {
                        ThrowPlayer();
                        BeginReveal();
                    }
                    break;
                case MouseState.Reveal:
                    if (timeUp && IsGrounded())
                        Enter(MouseState.Chase, 0f);
                    break;
                case MouseState.Chase:
                    Chase();
                    break;
                case MouseState.Stab:
                    Stab(timeUp);
                    break;
                case MouseState.Fatigue:
                    if (timeUp)
                        Enter(MouseState.Chase, 0f);
                    break;
            }
        }

        void ThrowPlayer()
        {
            var controller = Player.GetComponent<PlayerController>();
            if (controller == null)
                return;
            float dx = Player.transform.position.x - transform.position.x;
            // Tam ortadaysa geldiği yöne, yani baktığının tersine fırlar.
            float side = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : -controller.FacingDirection;
            controller.Knockback(new Vector2(side * throwVelocity.x, throwVelocity.y), throwControlLock);
        }

        void BeginReveal()
        {
            isRevealed = true;
            TurnTowards(DirectionToPlayer);
            Enter(MouseState.Reveal, revealDuration);
            body.linearVelocity = new Vector2(0f, revealHop);
            Vector2 center = box.bounds.center;
            PixelBurst.Spawn(center, twigColor, 22, 6f, 0.6f); // çalı patlar
            PixelBurst.Spawn(center, new Color(twigColor.r * 0.6f, twigColor.g * 0.6f, twigColor.b * 0.5f), 10, 4f, 0.5f);
        }

        void Chase()
        {
            if (Player == null || !Player.IsAlive)
                return;
            int direction = DirectionToPlayer;
            TurnTowards(direction);
            if (PlayerWithin(stopDistance, stabHeight))
            {
                BeginStab();
                return;
            }
            // Kenarda ya da duvarda bekler: uçuruma atlamaz.
            bool canRun = IsGrounded() && !IsBlocked(direction) && !PlayerStandsAbove
                && Mathf.Abs(Player.transform.position.x - transform.position.x) > stopDistance;
            body.linearVelocity = new Vector2(canRun ? direction * chaseRunSpeed : 0f, body.linearVelocity.y);
        }

        void BeginStab()
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            nextStabAt = Time.time;
            Enter(MouseState.Stab, stabDuration);
        }

        void Stab(bool timeUp)
        {
            if (Time.time >= nextStabAt)
            {
                nextStabAt = Time.time + stabInterval;
                Vector2 tip = (Vector2)transform.position + new Vector2(FacingDirection * 0.8f, 0.5f);
                PixelBurst.Spawn(tip, stabColor, 3, 2.5f, 0.15f);
                if (PlayerWithin(stabReach, stabHeight) && (Player.transform.position.x - transform.position.x) * FacingDirection >= -0.3f)
                    Player.TakeDamage(stabDamage, new Vector2(FacingDirection, 0f));
            }
            if (timeUp)
                Enter(MouseState.Fatigue, fatigueDuration);
        }

        // Gizliyken darbe işlemez (patlama hariç: çalıyı da fareyi de vurur). Açıkken bıçağıyla yalnız yakın darbeleri,
        // dili ve kazmayı, sayısı bitene kadar savuşturur. Kuyruk, mermi, saçma ve patlama delip geçer.
        protected override bool Deflects(in DamageInfo hit)
        {
            if (hit.Kind == DamageKind.Explosion)
                return false;
            if (IsDisguised)
                return true;
            bool parryable = (hit.Kind == DamageKind.Tongue && hit.Amount <= maxParriedDamage) || hit.Kind == DamageKind.Melee;
            return parryable && ParriesLeft > 0;
        }

        protected override void OnBlocked()
        {
            if (IsDisguised)
            {
                PixelBurst.Spawn(box.bounds.center, twigColor, 5, 3f, 0.3f);
                BeginReveal(); // çalıya vuran onu ortaya çıkarır, ama fırlatılmaz
                return;
            }

            // Savuşturma: kıvılcım, kısa bıçak duruşu ve hemen karşı saldırı.
            ParriesLeft--;
            parryPoseUntil = Time.time + parryPoseTime;
            TurnTowards(DirectionToPlayer);
            Vector2 knife = (Vector2)transform.position + new Vector2(FacingDirection * 0.7f, 0.6f);
            PixelBurst.Spawn(knife, stabColor, 10, 4.5f, 0.3f);
            if (PlayerWithin(stabReach, stabHeight))
                BeginStab();
            else if (State != MouseState.Reveal)
                Enter(MouseState.Chase, 0f);
        }

        protected override void OnDied()
        {
            PixelBurst.Spawn(box.bounds.center, twigColor, 12, 4f, 0.7f); // kalan çalı parçaları uçuşur
            ArmadilloDefeat.Spawn(transform.position, defeatFrames, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, twigColor);
        }

        // Oyuncu ölüp checkpoint'te doğunca her düşman gibi baştan başlar.
        public override void ResetEnemy()
        {
            base.ResetEnemy();
            contactDamage = 0;
            isRevealed = false;
            ParriesLeft = parryCount;
            parryPoseUntil = 0f;
            Enter(MouseState.Disguised, 0f);
        }

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;

            // Gizliyken zemindeki sıradan çalılarla birebir aynı dursun.
            if (!isRevealed)
                spriteRenderer.flipX = false;
            Transform visual = spriteRenderer.transform;
            Vector3 p = visual.localPosition;
            p.y = visualBaseY - (isRevealed ? 0f : disguiseSink);
            visual.localPosition = p;
        }

        Sprite CurrentFrame()
        {
            if (Time.time < parryPoseUntil && parryFrame != null)
                return parryFrame;
            switch (State)
            {
                case MouseState.Disguised: return disguiseFrame;
                case MouseState.Reveal:
                    float t = (Time.time - stateStartedAt) / revealDuration;
                    return t >= 0.75f ? jumpFrame : Once(revealFrames, revealDuration * 0.75f);
                case MouseState.Chase:
                    return Mathf.Abs(body.linearVelocity.x) > 0.1f ? Loop(runFrames, runFps) : parryFrame;
                case MouseState.Stab: return Loop(stabFrames, stabFps);
                case MouseState.Fatigue: return Loop(fatigueFrames, fatigueFps);
                default: return null;
            }
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
    }
}
