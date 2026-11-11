using DesertGecko.Core;
using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Silahtan fırlayan boş kovan: dönerek düşer, yerde bir iki kez seker, kısa süre yatıp söner.
    /// Pompalı her pompada bir fişek kovanı atar. Prefab gerektirmez.
    /// </summary>
    public class ShellCasing : MonoBehaviour
    {
        const float Gravity = 24f, Bounce = 0.4f, LieTime = 1.6f, FadeTime = 0.5f;
        static Sprite sprite;

        SpriteRenderer sr;
        Vector2 velocity;
        float spin;
        float age;
        float landedAt = -1f;
        int bounces;

        public static void Spawn(Vector2 position, Vector2 velocity, Color color)
        {
            if (sprite == null)
                sprite = CreateSprite();
            var go = new GameObject("BosKovan");
            go.transform.position = position;
            var casing = go.AddComponent<ShellCasing>();
            casing.sr = go.AddComponent<SpriteRenderer>();
            casing.sr.sprite = sprite;
            casing.sr.color = color;
            casing.sr.sortingOrder = 13;
            casing.velocity = velocity;
            casing.spin = Random.Range(540f, 900f) * (velocity.x >= 0f ? -1f : 1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (landedAt >= 0f)
            {
                float t = age - landedAt - LieTime;
                if (t > 0f)
                {
                    Color c = sr.color;
                    c.a = 1f - t / FadeTime;
                    sr.color = c;
                    if (t >= FadeTime)
                        Destroy(gameObject);
                }
                return;
            }

            velocity.y -= Gravity * dt;
            Vector2 from = transform.position;
            Vector2 step = velocity * dt;
            RaycastHit2D hit = Physics2D.Raycast(from, step.normalized, step.magnitude + 0.02f, GameLayers.GroundMask);
            if (hit.collider != null)
            {
                transform.position = hit.point + hit.normal * 0.03f;
                if (bounces < 2 && velocity.magnitude > 2f)
                {
                    bounces++;
                    velocity = Vector2.Reflect(velocity, hit.normal) * Bounce;
                    spin *= 0.5f;
                    return;
                }
                landedAt = age;
                transform.rotation = Quaternion.Euler(0f, 0f, Random.value < 0.5f ? 0f : 180f);
                return;
            }
            transform.position = from + step;
            transform.Rotate(0f, 0f, spin * dt);
            if (age > 4f)
                Destroy(gameObject); // uçuruma düştü
        }

        // 3x1 piksel kovan, 32 PPU
        static Sprite CreateSprite()
        {
            var tex = new Texture2D(3, 1) { filterMode = FilterMode.Point };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(200, 200, 200, 255) });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 3, 1), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
