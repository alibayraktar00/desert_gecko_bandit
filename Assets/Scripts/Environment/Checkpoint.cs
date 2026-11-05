using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Fenerli kaktüs: oyuncu dokununca fener tutuşur, dumanı tüter, ölünce burada yeniden doğar.
    /// Aynı anda tek bir checkpoint aktiftir; yenisi yanınca eskisi söner.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        const float PopTime = 0.28f, PopAmount = 0.14f;

        [SerializeField] SpriteRenderer totem;
        [SerializeField] Sprite offFrame;
        [SerializeField] Sprite[] igniteFrames;
        [SerializeField] Sprite[] litFrames;
        [SerializeField] float igniteFps = 12f;
        [SerializeField] float litFps = 10f;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip igniteClip;
        [SerializeField] Vector2 lanternOffset = new Vector2(0.05f, 2.45f);
        [SerializeField] Vector2 respawnOffset = new Vector2(0f, 0.1f);
        [SerializeField] Color burstColor = new Color32(0xFF, 0xB0, 0x48, 0xFF);
        [SerializeField] Color smokeColor = new Color32(0xB4, 0xA8, 0xA2, 0xFF);

        static Checkpoint current;

        float litAt = float.NegativeInfinity;
        Vector3 baseScale = Vector3.one;

        public bool IsActive => current == this;
        public Sprite CurrentFrame => totem != null ? totem.sprite : null;

        void Awake()
        {
            if (totem == null)
                totem = GetComponentInChildren<SpriteRenderer>();
            if (totem != null)
                baseScale = totem.transform.localScale;
            ShowOff();
        }

        void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (IsActive)
                return;
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health != null && health.IsAlive)
                Activate(health);
        }

        public void Activate(PlayerHealth health)
        {
            if (current != null)
                current.Extinguish();
            current = this;
            litAt = Time.time;
            health.SetCheckpoint((Vector2)transform.position + respawnOffset);

            PixelBurst.Spawn(LanternPosition, burstColor, 14, 3.5f, 0.6f);
            if (audioSource != null && igniteClip != null)
                audioSource.PlayOneShot(igniteClip);
        }

        Vector2 LanternPosition => (Vector2)transform.position + lanternOffset;

        void Extinguish()
        {
            litAt = float.NegativeInfinity;
            ShowOff();
            PixelBurst.Spawn(LanternPosition, smokeColor, 8, 1.5f, 0.7f); // sönen fenerden son duman
        }

        void ShowOff()
        {
            if (totem == null)
                return;
            if (offFrame != null)
                totem.sprite = offFrame;
            totem.transform.localScale = baseScale;
        }

        void Update()
        {
            if (!IsActive || totem == null)
                return;

            float age = Time.time - litAt;
            int ignite = igniteFrames != null ? igniteFrames.Length : 0;
            int frame = Mathf.FloorToInt(age * igniteFps);
            if (frame < ignite)
                totem.sprite = igniteFrames[frame];
            else if (litFrames != null && litFrames.Length > 0)
                totem.sprite = litFrames[Mathf.FloorToInt((age - ignite / igniteFps) * litFps) % litFrames.Length];

            // Tutuşurken ufak bir sıçrama: alttan pivotlu olduğu için yerde kalır.
            float t = Mathf.Clamp01(age / PopTime);
            float pop = Mathf.Sin(t * Mathf.PI) * PopAmount;
            totem.transform.localScale = new Vector3(baseScale.x * (1f - pop * 0.5f), baseScale.y * (1f + pop), baseScale.z);
        }
    }
}
