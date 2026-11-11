using System.Collections.Generic;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Enemies;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>
    /// Patlamanın fiziği: hasar yarıçapındaki düşmanlara patlama hasarı, daha geniş itme yarıçapındaki her şeye
    /// merkezden dışarı bir şok dalgası. İtme uzaklıkla azalır ve hep biraz yukarı kaldırır (yerden kopararak savurur):
    /// Gila savrulur (canı yanmaz; dinamitle zıplamak mümkün), düşmanlar havaya uçup sersemler (ağırlar az kıpırdar),
    /// yerdeki silahlar sıçrar, yerdeki dinamitler zincirleme patlar, fiziksel cisimler itilir. Ekran sarsılır.
    /// </summary>
    public static class Blast
    {
        const float PushRadiusScale = 1.9f;  // itme, hasar yarıçapının bu katına kadar uzanır
        const float PlayerForce = 16f, PlayerLock = 0.28f;
        const float EnemyForce = 13f, EnemyStun = 0.6f;
        const float PickupForce = 8f;
        const float BodyImpulse = 6f;
        const float ChainFuse = 0.12f;       // yakındaki dinamit bu kadar sonra patlar
        static int nextBlastId = 100000;      // atış numaralarıyla çakışmasın

        static readonly Collider2D[] hits = new Collider2D[32];
        static readonly HashSet<Object> touched = new HashSet<Object>();

        /// <summary>Patlat: hasar, şok dalgası, sarsıntı ve görsel.</summary>
        /// <param name="source">Patlayan dinamit (zincirde kendini yeniden tetiklemesin); yoksa null.</param>
        public static void Explode(Vector2 at, float radius, int damage, float shake, Object source = null)
        {
            ExplosionFx.Spawn(at, radius);
            CameraShake.AddAt(at, shake);

            float pushRadius = radius * PushRadiusScale;
            int blast = ++nextBlastId;
            touched.Clear();
            int count = Physics2D.OverlapCircle(at, pushRadius, new ContactFilter2D { useTriggers = false, useLayerMask = false }, hits);
            for (int i = 0; i < Mathf.Min(count, hits.Length); i++)
            {
                Collider2D other = hits[i];
                Vector2 point = other.bounds.center;
                Vector2 away = point - at;
                float distance = Mathf.Max(0f, away.magnitude - other.bounds.extents.magnitude * 0.5f);
                float falloff = 1f - Mathf.Clamp01(distance / pushRadius);
                Vector2 direction = Outward(away);

                var player = other.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    if (touched.Add(player))
                        PushPlayer(player, direction, falloff);
                    continue;
                }
                var enemy = other.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                {
                    if (!touched.Add(enemy))
                        continue;
                    if (damage > 0 && distance <= radius && enemy.IsAlive)
                        enemy.TakeDamage(new DamageInfo(damage, new Vector2(Mathf.Sign(away.x + 0.0001f), 0f), DamageKind.Explosion, blast));
                    if (enemy.IsAlive)
                        enemy.ApplyBlast(direction * (EnemyForce * falloff), EnemyStun * falloff);
                    continue;
                }
                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable != null && damage > 0 && distance <= radius && touched.Add((Object)damageable) && damageable.IsAlive)
                    damageable.TakeDamage(new DamageInfo(damage, new Vector2(Mathf.Sign(away.x + 0.0001f), 0f), DamageKind.Explosion, blast));

                Rigidbody2D rb = other.attachedRigidbody;
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic && touched.Add(rb))
                    rb.AddForce(direction * (BodyImpulse * falloff * rb.mass), ForceMode2D.Impulse);
            }

            // Çarpıştırıcısı olmayanlar: yerdeki silahlar ve yerdeki dinamitler.
            foreach (WeaponPickup pickup in Object.FindObjectsByType<WeaponPickup>(FindObjectsInactive.Exclude))
            {
                Vector2 away = (Vector2)pickup.transform.position - at;
                if (away.magnitude < pushRadius)
                    pickup.Push(Outward(away) * (PickupForce * (1f - away.magnitude / pushRadius)));
            }
            foreach (PlayerDynamite dynamite in Object.FindObjectsByType<PlayerDynamite>(FindObjectsInactive.Exclude))
            {
                if (dynamite == source)
                    continue;
                Vector2 away = (Vector2)dynamite.transform.position - at;
                if (away.magnitude < pushRadius)
                    dynamite.Kick(Outward(away) * (PickupForce * (1f - away.magnitude / pushRadius)), away.magnitude < radius ? ChainFuse : -1f);
            }
        }

        /// <summary>Merkezden dışarı, ama hep biraz yukarı: savrulan şey yerden kopsun, yere gömülmesin.</summary>
        static Vector2 Outward(Vector2 away)
        {
            Vector2 direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
            direction.y = Mathf.Max(direction.y, 0.45f);
            return direction.normalized;
        }

        static void PushPlayer(PlayerHealth player, Vector2 direction, float falloff)
        {
            if (falloff <= 0.05f || !player.IsAlive)
                return;
            var controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.Knockback(direction * (PlayerForce * falloff), PlayerLock * falloff + 0.08f);
        }
    }
}
