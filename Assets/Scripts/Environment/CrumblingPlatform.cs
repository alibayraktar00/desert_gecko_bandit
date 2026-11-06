using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>Çürük tahta: üstüne basılınca titrer, kısa süre sonra düşer, bir süre sonra yerine gelir.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public class CrumblingPlatform : MonoBehaviour
    {
        enum Phase { Idle, Shaking, Fallen }

        [SerializeField] float shakeTime = 0.45f;
        [SerializeField] float returnTime = 3f;
        [SerializeField] float shakeAmount = 0.04f;
        [SerializeField] float fallSpeed = 7f;
        [SerializeField] Color burstColor = new Color32(0xA8, 0x3A, 0x32, 0xFF);

        public bool IsSolid => col.enabled;

        BoxCollider2D col;
        SpriteRenderer sr;
        Vector3 home;
        Phase phase;
        float phaseEnds;
        readonly Collider2D[] hits = new Collider2D[1];

        void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            sr = GetComponent<SpriteRenderer>();
            home = transform.position;
            PlayerHealth.PlayerRespawned += Restore;
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Restore;
        }

        void FixedUpdate()
        {
            if (phase != Phase.Idle)
                return;
            // Tahtanın hemen üstü: alttan geçerken değil, basınca tetiklensin.
            Bounds b = col.bounds;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.PlayerMask, useTriggers = false };
            if (Physics2D.OverlapBox(new Vector2(b.center.x, b.max.y + 0.05f), new Vector2(b.size.x, 0.1f), 0f, filter, hits) > 0)
            {
                phase = Phase.Shaking;
                phaseEnds = Time.time + shakeTime;
            }
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.Shaking:
                    transform.position = home + Vector3.right * (Mathf.Sin(Time.time * 70f) * shakeAmount);
                    if (Time.time >= phaseEnds)
                        Fall();
                    break;
                case Phase.Fallen:
                    transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
                    Color c = sr.color;
                    c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 2f);
                    sr.color = c;
                    if (Time.time >= phaseEnds)
                        Restore();
                    break;
            }
        }

        void Fall()
        {
            phase = Phase.Fallen;
            phaseEnds = Time.time + returnTime;
            col.enabled = false;
            transform.position = home;
            PixelBurst.Spawn(col.bounds.center, burstColor, 8, 3f, 0.4f);
        }

        void Restore()
        {
            phase = Phase.Idle;
            col.enabled = true;
            transform.position = home;
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }
}
