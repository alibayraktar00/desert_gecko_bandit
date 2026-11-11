using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>
    /// Gila'nın attığı dinamit: dönerek kavis çizer, zemine çarpınca seker ve yuvarlanıp durur. Fitili bitince
    /// ya da bir düşmana değince patlar (hasar, şok dalgası, sarsıntı: Blast). Başka bir patlama onu savurur,
    /// yakındaysa zincirleme patlatır. Fitil bitmeye yakın çubuk hızla yanıp söner.
    /// </summary>
    public class PlayerDynamite : MonoBehaviour
    {
        const float Gravity = 22f, Bounce = 0.35f, GroundFriction = 0.55f, RestSpeed = 1.2f, SpinSpeed = 720f;
        static readonly Color FlashColor = new Color32(0xFF, 0xE8, 0x7A, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[6];
        ContactFilter2D filter;
        SpriteRenderer sr;
        WeaponData weapon;
        Vector2 velocity;
        Vector2 previous;
        float age;
        bool resting;
        bool exploded;

        /// <param name="from">Atışın çıktığı göğüs: elden çıkış noktasına kadarki yol da denetlenir (duvardan geçmesin).</param>
        public static PlayerDynamite Spawn(WeaponData weapon, Vector2 from, Vector2 position, Vector2 velocity, int order)
        {
            var go = new GameObject("GilaDinamit");
            go.transform.position = position;
            var dynamite = go.AddComponent<PlayerDynamite>();
            dynamite.weapon = weapon;
            dynamite.velocity = velocity;
            dynamite.previous = from;
            dynamite.sr = go.AddComponent<SpriteRenderer>();
            dynamite.sr.sprite = weapon.BulletSprite;
            dynamite.sr.sortingOrder = order;
            if (weapon.FuseSound != null)
            {
                var fuse = go.AddComponent<AudioSource>();
                fuse.clip = weapon.FuseSound;
                fuse.loop = true;
                fuse.volume = 0.35f;
                fuse.spatialBlend = 0f;
                fuse.Play();
            }
            dynamite.filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | LayerMask.GetMask("Default", GameLayers.Enemy),
                useTriggers = false,
            };
            return dynamite;
        }

        void Awake() => PlayerHealth.PlayerRespawned += Remove;
        void OnDestroy() => PlayerHealth.PlayerRespawned -= Remove;

        void Remove()
        {
            if (this != null)
                Destroy(gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= weapon.FuseTime)
            {
                Explode(transform.position);
                return;
            }

            Blink();
            if (Mathf.Repeat(age, 0.06f) < dt)
                PixelBurst.Spawn(transform.position + transform.right * 0.2f, FlashColor, 1, 1.5f, 0.15f);

            if (resting)
                return;
            velocity.y -= Gravity * dt;
            Move(velocity * dt);
            transform.Rotate(0f, 0f, -SpinSpeed * Mathf.Sign(velocity.x + 0.0001f) * dt * Mathf.Clamp01(velocity.magnitude / 4f));
        }

        void Move(Vector2 step)
        {
            Vector2 from = previous;
            Vector2 to = (Vector2)transform.position + step;
            int count = Physics2D.Linecast(from, to, filter, hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = hits[i].collider;
                if (((1 << other.gameObject.layer) & GameLayers.GroundMask) != 0)
                {
                    BounceOff(hits[i]);
                    return;
                }
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || target is PlayerHealth || !target.IsAlive)
                    continue;
                Explode(hits[i].point); // düşmana değince beklemez
                return;
            }
            transform.position = to;
            previous = to;
        }

        /// <summary>Zeminden seker; yerde yavaşlayınca durur ve fitilin bitmesini bekler.</summary>
        void BounceOff(RaycastHit2D hit)
        {
            Vector2 at = hit.point + hit.normal * 0.12f;
            transform.position = at;
            previous = at;
            velocity = Vector2.Reflect(velocity, hit.normal) * Bounce;
            if (hit.normal.y > 0.5f)
            {
                velocity.x *= GroundFriction;
                if (velocity.magnitude < RestSpeed)
                {
                    velocity = Vector2.zero;
                    resting = true;
                    transform.rotation = Quaternion.identity;
                }
            }
        }

        void Blink()
        {
            float left = weapon.FuseTime - age;
            float rate = left < 0.4f ? 24f : left < 0.8f ? 12f : 0f;
            bool lit = rate > 0f && Mathf.Repeat(age * rate, 1f) < 0.5f;
            sr.color = lit ? new Color(1f, 0.75f, 0.55f) : Color.white;
        }

        /// <summary>
        /// Başka bir patlamanın şok dalgası: dinamit savrulur; <paramref name="fuse"/> 0 ya da büyükse fitili o kadar
        /// sonra biter (zincirleme patlama).
        /// </summary>
        public void Kick(Vector2 push, float fuse)
        {
            velocity = push;
            resting = false;
            if (fuse >= 0f)
                age = Mathf.Max(age, weapon.FuseTime - fuse);
        }

        void Explode(Vector2 at)
        {
            if (exploded)
                return;
            exploded = true;
            if (weapon.ImpactSound != null)
                OneShotSound.Play(weapon.ImpactSound, weapon.ImpactVolume);
            Blast.Explode(at, weapon.BlastRadius, weapon.Damage, 0.75f, this);
            Remove();
        }
    }
}
