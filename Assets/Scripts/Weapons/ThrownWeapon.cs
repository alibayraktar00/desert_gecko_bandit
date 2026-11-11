using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>
    /// Mermisi biten, fırlatılan silah: dönerek kavis çizer. İlk çarptığı düşmana küçük bir hasar verip
    /// geri seker; yere düşünce biraz yatar ve söner. Bir daha alınamaz.
    /// </summary>
    public class ThrownWeapon : MonoBehaviour
    {
        const float Gravity = 28f, SpinSpeed = 1080f, Lifetime = 3f, LieTime = 0.5f, FadeTime = 0.35f;
        static readonly Color DustColor = new Color32(0xC8, 0x9A, 0x60, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[6];
        ContactFilter2D filter;
        SpriteRenderer sr;
        Vector2 velocity;
        float age, landedAt = -1f;
        int damage;
        bool spent; // düşmana çarptı: artık hasar vermez

        public static ThrownWeapon Spawn(Sprite sprite, Vector2 position, Vector2 velocity, int damage, int order)
        {
            var go = new GameObject("FirlatilanSilah");
            go.transform.position = position;
            var thrown = go.AddComponent<ThrownWeapon>();
            thrown.sr = go.AddComponent<SpriteRenderer>();
            thrown.sr.sprite = sprite;
            thrown.sr.sortingOrder = order;
            thrown.velocity = velocity;
            thrown.damage = damage;
            return thrown;
        }

        void Awake()
        {
            filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | LayerMask.GetMask("Default", GameLayers.Enemy),
                useTriggers = false,
            };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (landedAt >= 0f)
            {
                Fade();
                return;
            }
            if (age > Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            velocity.y -= Gravity * dt;
            Vector2 from = transform.position;
            Vector2 to = from + velocity * dt;
            transform.Rotate(0f, 0f, -SpinSpeed * Mathf.Sign(velocity.x + 0.0001f) * dt);

            int count = Physics2D.Linecast(from, to, filter, hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = hits[i].collider;
                if (((1 << other.gameObject.layer) & GameLayers.GroundMask) != 0)
                {
                    transform.position = hits[i].point + hits[i].normal * 0.15f;
                    if (hits[i].normal.y > 0.5f)
                    {
                        landedAt = age;
                        transform.rotation = Quaternion.identity;
                        PixelBurst.Spawn(hits[i].point, DustColor, 6, 2f, 0.35f);
                    }
                    else
                    {
                        velocity = new Vector2(-velocity.x * 0.3f, Mathf.Max(velocity.y, 0f)); // duvardan seker
                    }
                    return;
                }
                if (spent)
                    continue;
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || target is PlayerHealth || !target.IsAlive)
                    continue;
                target.TakeDamage(new DamageInfo(damage, velocity.normalized, DamageKind.Thrown));
                spent = true;
                velocity = new Vector2(-velocity.x * 0.25f, 5f); // kafasından seker
                PixelBurst.Spawn(hits[i].point, Color.white, 6, 3f, 0.25f);
                break;
            }
            transform.position = to;
        }

        void Fade()
        {
            float t = age - landedAt - LieTime;
            if (t < 0f)
                return;
            Color c = sr.color;
            c.a = 1f - t / FadeTime;
            sr.color = c;
            if (t >= FadeTime)
                Destroy(gameObject);
        }
    }
}
