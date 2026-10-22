using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Kaktüs totemi: oyuncu dokununca aktif olur, ölünce burada yeniden doğar.
    /// Aynı anda tek bir checkpoint aktiftir.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] SpriteRenderer totem;
        [SerializeField] Color inactiveColor = new Color(0.55f, 0.55f, 0.6f);
        [SerializeField] Color activeColor = Color.white;
        [SerializeField] Vector2 respawnOffset = new Vector2(0f, 0.1f);
        [SerializeField] Color burstColor = new Color32(0x7C, 0xC4, 0x4A, 0xFF);

        static Checkpoint current;

        public bool IsActive => current == this;

        void Awake()
        {
            if (totem == null)
                totem = GetComponentInChildren<SpriteRenderer>();
            SetVisual(false);
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
                current.SetVisual(false);
            current = this;
            SetVisual(true);
            health.SetCheckpoint((Vector2)transform.position + respawnOffset);
            PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 1.2f, burstColor, 12, 3.5f, 0.6f);
        }

        void SetVisual(bool active)
        {
            if (totem != null)
                totem.color = active ? activeColor : inactiveColor;
        }
    }
}
