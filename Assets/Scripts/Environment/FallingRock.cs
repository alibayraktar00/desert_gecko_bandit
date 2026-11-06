using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Tavandan sarkan kaya: oyuncu altına gelince titrer ve düşer, zemine çarpınca parçalanır,
    /// değdiği oyuncunun canını yakar. Bir süre sonra aynı yerde yeniden belirir.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FallingRock : MonoBehaviour
    {
        enum Phase { Hanging, Shaking, Falling, Gone }

        [SerializeField] float triggerHalfWidth = 1.2f;
        [SerializeField] float triggerDepth = 12f;
        [SerializeField] float shakeTime = 0.4f;
        [SerializeField] float shakeAmount = 0.05f;
        [SerializeField] float gravity = 26f;
        [SerializeField] float regrowTime = 3f;
        [SerializeField] int damage = 1;
        [SerializeField] float killY = -14f;
        [SerializeField] Vector2 hitSize = new Vector2(0.6f, 0.9f);
        [SerializeField] Color burstColor = new Color32(0x6E, 0x44, 0x30, 0xFF);

        public bool IsFalling => phase == Phase.Falling;

        SpriteRenderer sr;
        PlayerHealth player;
        Vector3 home;
        Phase phase;
        float phaseEnds, speed;
        bool hitPlayer;
        readonly Collider2D[] hits = new Collider2D[1];

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            home = transform.position;
            PlayerHealth.PlayerRespawned += Rehang;
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Rehang;
        }

        void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.Hanging:
                    if (player != null && player.IsAlive && PlayerBelow())
                    {
                        phase = Phase.Shaking;
                        phaseEnds = Time.time + shakeTime;
                    }
                    break;
                case Phase.Shaking:
                    transform.position = home + Vector3.right * (Mathf.Sin(Time.time * 60f) * shakeAmount);
                    if (Time.time >= phaseEnds)
                    {
                        transform.position = home;
                        phase = Phase.Falling;
                        speed = 0f;
                        hitPlayer = false;
                    }
                    break;
                case Phase.Falling:
                    Fall();
                    break;
                case Phase.Gone:
                    if (Time.time >= phaseEnds)
                        Rehang();
                    break;
            }
        }

        bool PlayerBelow()
        {
            Vector2 delta = player.transform.position - home;
            return Mathf.Abs(delta.x) <= triggerHalfWidth && delta.y < 0f && delta.y >= -triggerDepth;
        }

        void Fall()
        {
            speed += gravity * Time.deltaTime;
            float step = speed * Time.deltaTime;
            Vector2 tip = transform.position;
            float tipOffset = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
            RaycastHit2D ground = Physics2D.Raycast(tip + Vector2.down * tipOffset, Vector2.down, step, GameLayers.GroundMask);
            transform.position += Vector3.down * step;

            if (!hitPlayer)
            {
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.PlayerMask, useTriggers = false };
                Vector2 center = (Vector2)transform.position + Vector2.down * (tipOffset / 2f);
                if (Physics2D.OverlapBox(center, hitSize, 0f, filter, hits) > 0)
                {
                    hitPlayer = true;
                    var health = hits[0].GetComponentInParent<PlayerHealth>();
                    if (health != null)
                        health.TakeDamage(damage, new Vector2(health.transform.position.x >= transform.position.x ? 1f : -1f, 0f));
                }
            }

            if (ground || transform.position.y < killY)
                Shatter();
        }

        void Shatter()
        {
            PixelBurst.Spawn((Vector2)transform.position + Vector2.down * 0.6f, burstColor, 10, 4f, 0.45f);
            sr.enabled = false;
            phase = Phase.Gone;
            phaseEnds = Time.time + regrowTime;
        }

        void Rehang()
        {
            phase = Phase.Hanging;
            transform.position = home;
            sr.enabled = true;
        }
    }
}
