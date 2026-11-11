using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Oyuncunun görsel tarafı: duruma göre kare animasyonu, yöne göre çevirme,
    /// zıplama/iniş esneme efekti. Bir animasyonun kareleri yoksa en yakın olanı kullanılır.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        /// <summary>Bir karenin kuyruksuzdan büyümeye doğru halleri (tools/gila_kuyruk_isle.py).</summary>
        [System.Serializable]
        public struct TailVariant
        {
            public Sprite full;
            public Sprite[] stages;
        }

        [SerializeField] PlayerController controller;
        [SerializeField] SpriteRenderer spriteRenderer;

        [Header("Animasyon kareleri")]
        [SerializeField] Sprite[] idleFrames;
        [SerializeField] float idleFps = 6f;
        [SerializeField] Sprite[] runFrames;
        [SerializeField] float runFps = 12f;
        [SerializeField] Sprite riseFrame;
        [SerializeField] Sprite apexFrame;
        [SerializeField] Sprite fallFrame;
        [Tooltip("Boşsa düşme karesi kullanılır")]
        [SerializeField] Sprite[] glideFrames;
        [Tooltip("Süzülme: kuyruk ucu helikopter pervanesi gibi döner (8 kare bir tur)")]
        [SerializeField] float glideFps = 60f;
        [Tooltip("Boşsa idle kareleri kullanılır (yan görünümde duvara bakan dik duruş)")]
        [SerializeField] Sprite[] wallFrames;
        [SerializeField] float wallFps = 8f;
        [Tooltip("Dikey hız bu aralıktayken zirve karesi gösterilir")]
        [SerializeField] float apexVelocityRange = 2.5f;

        [Header("Kuyruk")]
        [SerializeField] TailVariant[] tailVariants;

        [Header("Esneme")]
        [SerializeField] Vector2 jumpStretch = new Vector2(0.75f, 1.3f);
        [SerializeField] Vector2 landSquash = new Vector2(1.3f, 0.7f);
        [SerializeField] float squashRecovery = 12f;

        [Header("Placeholder renkleri")]
        [SerializeField] bool tintByState = true;
        [SerializeField] Color groundedColor = Color.white;
        [SerializeField] Color airborneColor = new Color(1f, 0.85f, 0.6f);
        [SerializeField] Color wallColor = new Color(0.6f, 1f, 0.6f);
        [SerializeField] Color glideColor = new Color(0.6f, 0.85f, 1f);

        /// <summary>Şu an gösterilen gövde karesi (kuyruk haline çevrilmeden önce). Silah kolu buna göre seçilir.</summary>
        public Sprite BaseFrame { get; private set; }

        Vector2 squash = Vector2.one;
        Sprite[] currentFrames;
        float animTime;
        readonly Sprite[] single = new Sprite[1];
        readonly System.Collections.Generic.Dictionary<Sprite, Sprite[]> variantsByFrame =
            new System.Collections.Generic.Dictionary<Sprite, Sprite[]>();
        TailAttack tail;

        void Awake()
        {
            tail = GetComponentInParent<TailAttack>();
            if (tailVariants == null)
                return;
            foreach (TailVariant v in tailVariants)
                if (v.full != null)
                    variantsByFrame[v.full] = v.stages;
        }

        // Kuyruk gittiyse karenin kuyruksuz ya da büyümekte olan hali.
        Sprite WithTailState(Sprite frame)
        {
            float growth = tail != null ? tail.TailGrowth : 1f;
            if (growth >= 1f || frame == null || !variantsByFrame.TryGetValue(frame, out Sprite[] stages) || stages == null || stages.Length == 0)
                return frame;
            return stages[Mathf.Clamp(Mathf.FloorToInt(growth * stages.Length), 0, stages.Length - 1)];
        }

        void Reset()
        {
            controller = GetComponentInParent<PlayerController>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            controller.Jumped += Stretch;
            controller.DoubleJumped += Stretch;
            controller.WallJumped += Stretch;
            controller.Landed += Squash;
        }

        void OnDisable()
        {
            controller.Jumped -= Stretch;
            controller.DoubleJumped -= Stretch;
            controller.WallJumped -= Stretch;
            controller.Landed -= Squash;
        }

        void Stretch() => squash = jumpStretch;
        void Squash() => squash = landSquash;

        void LateUpdate()
        {
            squash = Vector2.Lerp(squash, Vector2.one, squashRecovery * Time.deltaTime);
            transform.localScale = new Vector3(squash.x * controller.FacingDirection, squash.y, 1f);

            Animate();

            if (tintByState)
            {
                spriteRenderer.color = controller.State switch
                {
                    PlayerController.MoveState.WallCling => wallColor,
                    PlayerController.MoveState.Gliding => glideColor,
                    PlayerController.MoveState.Airborne => airborneColor,
                    _ => groundedColor,
                };
            }
        }

        void Animate()
        {
            float fps = 0f;
            Sprite[] frames;
            Vector2 v = controller.LocalVelocity;

            switch (controller.State)
            {
                case PlayerController.MoveState.Grounded:
                    if (Mathf.Abs(v.x) > 0.5f && HasFrames(runFrames))
                    {
                        frames = runFrames;
                        fps = runFps * Mathf.Clamp(Mathf.Abs(v.x) / 8f, 0.6f, 1.5f);
                    }
                    else
                    {
                        frames = idleFrames;
                        fps = idleFps;
                    }
                    break;

                case PlayerController.MoveState.WallCling:
                    frames = HasFrames(wallFrames) ? wallFrames : idleFrames;
                    // Sadece tırmanırken oynat (inerken geriye doğru), dururken o karede kal.
                    fps = Mathf.Abs(v.y) > 0.1f ? wallFps * Mathf.Sign(v.y) : 0f;
                    break;

                case PlayerController.MoveState.Gliding:
                    frames = HasFrames(glideFrames) ? glideFrames : Single(fallFrame);
                    fps = glideFps;
                    break;

                default:
                    if (v.y > apexVelocityRange) frames = Single(riseFrame);
                    else if (v.y < -apexVelocityRange) frames = Single(fallFrame);
                    else frames = Single(apexFrame);
                    break;
            }

            if (!HasFrames(frames))
                return; // kare yoksa mevcut sprite'ta kal

            if (frames != currentFrames)
            {
                currentFrames = frames;
                animTime = 0f;
            }

            animTime += Time.deltaTime * fps;
            int index = (int)Mathf.Repeat(Mathf.Floor(animTime), frames.Length);
            Sprite shown = frames[index];
            // Süzülme kareleri düşüş karesinin pervaneli hali: el aynı yerde, silah düşüş pozunu kullanır.
            BaseFrame = frames == glideFrames && fallFrame != null ? fallFrame : shown;
            spriteRenderer.sprite = WithTailState(shown);
        }

        Sprite[] Single(Sprite sprite)
        {
            single[0] = sprite;
            return single;
        }

        static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;
    }
}
