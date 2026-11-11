using DesertGecko.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Patlamanın görsel tarafı (hasar ve itme Weapons.Blast'ta): bir anlık beyaz parlama, genişleyip sönen şok halkası,
    /// sahneyi kısa süre turuncuya boyayan ışık, havaya fırlayıp yere düşen, seken ve bir süre yatıp sönen toprak/taş
    /// parçaları, yukarı süzülüp büyüyerek dağılan duman. Prefab gerektirmez; sprite'lar çalışırken üretilir.
    /// </summary>
    public class ExplosionFx : MonoBehaviour
    {
        const float FlashTime = 0.07f, RingTime = 0.28f, LightTime = 0.35f;
        const float DebrisGravity = 26f, DebrisBounce = 0.3f, DebrisLie = 1.4f, DebrisFade = 0.6f;
        const float SmokeTime = 1.3f, SmokeRise = 0.7f;
        const int PixelsPerUnit = 32;

        static readonly Color[] DirtColors =
        {
            new Color32(0xC8, 0x9A, 0x60, 0xFF), new Color32(0x8A, 0x5A, 0x3A, 0xFF),
            new Color32(0x5E, 0x3B, 0x26, 0xFF), new Color32(0x7A, 0x6E, 0x62, 0xFF),
        };
        static readonly Color SmokeColor = new Color32(0x6E, 0x5E, 0x52, 0xFF);
        static readonly Color FlashColor = new Color32(0xFF, 0xF4, 0xC8, 0xFF);
        static readonly Color RingColor = new Color32(0xFF, 0xE0, 0x90, 0xFF);
        static readonly Color LightColor = new Color32(0xFF, 0xA0, 0x40, 0xFF);
        static Sprite disc, ring, chunkSmall, chunkBig;

        struct Debris
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public Vector2 velocity;
            public float spin;
            public float landedAt;
            public bool bounced;
        }

        float radius;
        float age;
        SpriteRenderer flash;
        SpriteRenderer shock;
        Light2D glow;
        Debris[] debris;
        SpriteRenderer[] smoke;
        Vector2[] smokeDrift;

        /// <param name="radius">Patlamanın hasar yarıçapı: halka, ışık ve parçaların savrulması buna göre ölçeklenir.</param>
        public static void Spawn(Vector2 at, float radius, int debrisCount = 16, int smokeCount = 7)
        {
            EnsureSprites();
            var go = new GameObject("Patlama");
            go.transform.position = at;
            go.AddComponent<ExplosionFx>().Init(radius, debrisCount, smokeCount);
        }

        void Init(float blastRadius, int debrisCount, int smokeCount)
        {
            radius = blastRadius;
            flash = Child("Parlama", disc, FlashColor, 25);
            flash.transform.localScale = Vector3.one * radius * 1.4f;
            shock = Child("SokHalkasi", ring, RingColor, 24);
            shock.transform.localScale = Vector3.one * 0.3f;

            glow = new GameObject("Isik").AddComponent<Light2D>();
            glow.transform.SetParent(transform, false);
            glow.lightType = Light2D.LightType.Point;
            glow.pointLightOuterRadius = radius * 3.5f;
            glow.pointLightInnerRadius = radius * 0.5f;
            glow.color = LightColor;
            glow.intensity = 2.2f;

            debris = new Debris[debrisCount];
            for (int i = 0; i < debrisCount; i++)
            {
                SpriteRenderer sr = Child("Parca", Random.value < 0.35f ? chunkBig : chunkSmall, DirtColors[Random.Range(0, DirtColors.Length)], 21);
                // Patlama çoğu zaman yerin yüzeyinde olur: parçalar zeminin içinden başlamasın.
                sr.transform.localPosition = new Vector2(Random.Range(-0.15f, 0.15f), 0.18f);
                // Çoğu yukarı ve yanlara: yerden kalkan toprak. Birkaçı neredeyse dik fırlar.
                float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;
                float speed = Random.Range(5f, 11f) * Mathf.Lerp(0.8f, 1.2f, radius / 2f);
                debris[i] = new Debris
                {
                    transform = sr.transform,
                    renderer = sr,
                    velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    spin = Random.Range(-720f, 720f),
                    landedAt = -1f,
                };
            }

            smoke = new SpriteRenderer[smokeCount];
            smokeDrift = new Vector2[smokeCount];
            for (int i = 0; i < smokeCount; i++)
            {
                smoke[i] = Child("Duman", disc, SmokeColor, 19);
                smoke[i].transform.localPosition = Random.insideUnitCircle * radius * 0.4f;
                smokeDrift[i] = new Vector2(Random.Range(-0.5f, 0.5f), SmokeRise * Random.Range(0.7f, 1.3f));
            }
        }

        SpriteRenderer Child(string name, Sprite sprite, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            flash.enabled = age < FlashTime;

            float ringT = Mathf.Clamp01(age / RingTime);
            shock.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2.4f, 1f - (1f - ringT) * (1f - ringT));
            shock.color = new Color(RingColor.r, RingColor.g, RingColor.b, 0.85f * (1f - ringT));
            shock.enabled = ringT < 1f;

            if (glow != null)
            {
                glow.intensity = 2.2f * Mathf.Clamp01(1f - age / LightTime);
                if (age >= LightTime)
                    Destroy(glow.gameObject);
            }

            bool alive = ringT < 1f;
            for (int i = 0; i < debris.Length; i++)
                alive |= UpdateDebris(ref debris[i], dt);
            for (int i = 0; i < smoke.Length; i++)
                alive |= UpdateSmoke(i, dt);
            if (!alive)
                Destroy(gameObject);
        }

        /// <summary>Parça yerçekimiyle düşer, zemine değince bir kez seker, sonra yatar ve söner. Yaşıyorsa true.</summary>
        bool UpdateDebris(ref Debris d, float dt)
        {
            if (d.transform == null)
                return false;
            if (d.landedAt >= 0f)
            {
                float lie = age - d.landedAt - DebrisLie;
                if (lie >= DebrisFade)
                {
                    Destroy(d.transform.gameObject);
                    d.transform = null;
                    return false;
                }
                if (lie > 0f)
                {
                    Color c = d.renderer.color;
                    c.a = 1f - lie / DebrisFade;
                    d.renderer.color = c;
                }
                return true;
            }

            d.velocity.y -= DebrisGravity * dt;
            Vector2 from = d.transform.position;
            Vector2 step = d.velocity * dt;
            RaycastHit2D hit = step.sqrMagnitude > 0f
                ? Physics2D.Raycast(from, step.normalized, step.magnitude, GameLayers.GroundMask)
                : default;
            if (hit.collider != null)
            {
                d.transform.position = hit.point + hit.normal * 0.03f;
                if (!d.bounced && d.velocity.magnitude > 3f)
                {
                    d.bounced = true;
                    d.velocity = Vector2.Reflect(d.velocity, hit.normal) * DebrisBounce;
                }
                else if (hit.normal.y > 0.5f)
                {
                    d.landedAt = age; // yere yattı
                }
                else
                {
                    d.velocity = new Vector2(-d.velocity.x * 0.2f, Mathf.Min(d.velocity.y, 0f)); // duvardan düşer
                }
                return true;
            }
            d.transform.position = from + step;
            d.transform.Rotate(0f, 0f, d.spin * dt);
            if (d.transform.position.y < transform.position.y - 30f)
            {
                Destroy(d.transform.gameObject); // uçuruma düştü
                d.transform = null;
                return false;
            }
            return true;
        }

        bool UpdateSmoke(int i, float dt)
        {
            if (smoke[i] == null)
                return false;
            float t = age / SmokeTime;
            if (t >= 1f)
            {
                Destroy(smoke[i].gameObject);
                smoke[i] = null;
                return false;
            }
            smoke[i].transform.localPosition += (Vector3)(smokeDrift[i] * dt);
            smoke[i].transform.localScale = Vector3.one * Mathf.Lerp(radius * 0.5f, radius * 1.1f, t);
            smoke[i].color = new Color(SmokeColor.r, SmokeColor.g, SmokeColor.b, 0.55f * (1f - t) * Mathf.Clamp01(age / 0.08f));
            return true;
        }

        // --- Çalışırken üretilen sprite'lar (piksel ölçeği oyunla aynı: 32 PPU) ---

        static void EnsureSprites()
        {
            if (disc != null)
                return;
            disc = Circle(32, filled: true);    // 1 birim çap
            ring = Circle(32, filled: false);
            chunkSmall = Square(2);
            chunkBig = Square(3);
        }

        static Sprite Circle(int size, bool filled)
        {
            var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    bool on = filled ? d <= r : d <= r && d >= r - 2f;
                    pixels[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite Square(int size)
        {
            var tex = new Texture2D(size, size) { filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }
    }
}
