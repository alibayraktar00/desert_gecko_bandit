using System.Collections.Generic;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Yıkım: bir noktanın çevresindeki binalardan ve eşyalardan (DestructibleSprite) çentikli bir parça koparır;
    /// kopan pikseller küçük taş/tahta parçaları olarak fizikle saçılır, yerde seker, yuvarlanır, sonra söner.
    /// Mavi Dev yere çarpınca ve duvarı delip geçerken kullanır. Kırılmaz kaya (BreakableRock) da patlamış sayılır.
    /// Kırık görseller oyuncu yeniden doğunca onarılır (dev de baştan başlar).
    /// </summary>
    public static class Shatter
    {
        const int MaxLiveDebris = 260;
        static readonly Queue<GameObject> live = new Queue<GameObject>();
        static readonly List<DestructibleSprite> hits = new List<DestructibleSprite>();
        static PhysicsMaterial2D debrisMaterial;
        static Sprite square;
        static int debrisLayer = -2;

        /// <summary>Merkezin çevresinde yarıçap kadar yıkım; güç parçaların ne kadar uzağa savrulacağı.</summary>
        public static void At(Vector2 center, float radius, float strength = 1f)
        {
            hits.Clear();
            DestructibleSprite.Overlapping(center, radius, hits);
            foreach (DestructibleSprite target in hits)
                target.Carve(center, radius, strength);

            foreach (Collider2D c in Physics2D.OverlapCircleAll(center, radius))
                if (c.GetComponent<Environment.BreakableRock>() is Environment.BreakableRock rock && rock.IsAlive)
                    rock.TakeDamage(new Core.DamageInfo(1, (Vector2)rock.transform.position - center, Core.DamageKind.Explosion));
        }

        /// <summary>Düz renkli parçalar (toprak, taş): görseli olmayan çarpışmalardan (duvar, blok) kopanlar için.</summary>
        public static void Chunks(Vector2 at, Color color, int count, Vector2 push, float strength = 1f)
        {
            if (square == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 32f);
            }
            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(2, 5);
                Color c = color * Random.Range(0.75f, 1.1f);
                c.a = 1f;
                Vector2 v = push * Random.Range(1.5f, 4.5f) * strength + new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(2.5f, 6f) * strength);
                Spawn(square, at + Random.insideUnitCircle * 0.25f, v, c, size, null, 0);
            }
        }

        /// <summary>Kopan bir parça: kendi görseli, fizik gövdesi; zemine çarpar, seker, ömrünün sonunda yanıp söner.</summary>
        internal static void Spawn(Sprite sprite, Vector2 at, Vector2 velocity, Color color, float scale, Material material, int order)
        {
            if (debrisLayer == -2)
            {
                debrisLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (debrisLayer >= 0)
                {
                    // Parçalar Gila'yı ve düşmanları itmesin, yalnız zemine çarpsın.
                    foreach (string other in new[] { Core.GameLayers.Player, Core.GameLayers.Enemy, Core.GameLayers.PlayerAttack, "Ignore Raycast" })
                    {
                        int layer = LayerMask.NameToLayer(other);
                        if (layer >= 0)
                            Physics2D.IgnoreLayerCollision(debrisLayer, layer);
                    }
                }
            }
            if (debrisMaterial == null)
                debrisMaterial = new PhysicsMaterial2D("Enkaz") { bounciness = 0.25f, friction = 0.7f };

            while (live.Count >= MaxLiveDebris)
            {
                GameObject old = live.Dequeue();
                if (old != null)
                    Object.Destroy(old);
            }

            var go = new GameObject("Enkaz");
            if (debrisLayer >= 0)
                go.layer = debrisLayer;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (material != null)
                sr.sharedMaterial = material;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 2.5f;
            body.sharedMaterial = debrisMaterial;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.Range(-720f, 720f);
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = sprite.bounds.size;
            go.AddComponent<DebrisLife>().Begin(Random.Range(2.2f, 3.6f), sprite != square ? sprite : null);
            live.Enqueue(go);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            live.Clear();
            debrisLayer = -2;
        }
    }

    /// <summary>Enkaz parçasının ömrü: süre dolmadan yanıp söner, sonra silinir. Oyuncu doğunca hemen silinir.</summary>
    public class DebrisLife : MonoBehaviour
    {
        float dieAt;
        SpriteRenderer sr;
        Sprite owned; // binadan kopan parçanın kendi görseli: parçayla birlikte silinir

        public void Begin(float lifetime, Sprite ownedSprite)
        {
            dieAt = Time.time + lifetime;
            owned = ownedSprite;
            sr = GetComponent<SpriteRenderer>();
            PlayerHealth.PlayerRespawned += Remove;
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Remove;
            if (owned != null)
                Destroy(owned);
        }
        void Remove() => Destroy(gameObject);

        void Update()
        {
            float left = dieAt - Time.time;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            if (left < 0.6f && sr != null)
                sr.enabled = Mathf.FloorToInt(Time.time * 14f) % 2 == 0;
        }
    }

    /// <summary>
    /// Kırılabilen görsel (bina, eşya). İlk darbede görselin piksellerinin yazılabilir bir kopyası çıkarılır; Carve
    /// çentikli bir daire içindeki pikselleri siler ve 3x3 piksellik parçalar halinde fizikle saçar.
    /// Görselin çarpışma yüzeyleri (veranda, çatı) yerinde kalır. Oyuncu yeniden doğunca görsel onarılır.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class DestructibleSprite : MonoBehaviour
    {
        const int Block = 3;               // parça boyu (piksel)
        const int MaxChunksPerCarve = 70;  // fazlası yalnız silinir (toz olur)

        static readonly List<DestructibleSprite> all = new List<DestructibleSprite>();

        SpriteRenderer sr;
        Sprite original;
        Texture2D work;     // görünen, delikli kopya
        Texture2D source;   // kopmadan önceki pikseller: parçaların görseli
        Sprite carved;
        Color32[] pixels;
        int width, height;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            original = sr.sprite;
        }

        void OnEnable()
        {
            all.Add(this);
            PlayerHealth.PlayerRespawned += Restore;
        }

        void OnDisable()
        {
            all.Remove(this);
            PlayerHealth.PlayerRespawned -= Restore;
        }

        void OnDestroy()
        {
            if (work != null) Destroy(work);
            if (source != null) Destroy(source);
            if (carved != null) Destroy(carved);
        }

        internal static void Overlapping(Vector2 center, float radius, List<DestructibleSprite> result)
        {
            foreach (DestructibleSprite d in all)
            {
                if (d.sr == null || !d.sr.enabled || d.original == null)
                    continue;
                Bounds b = d.sr.bounds;
                Vector2 closest = b.ClosestPoint(center);
                if ((closest - center).sqrMagnitude <= radius * radius)
                    result.Add(d);
            }
        }

        void Restore()
        {
            if (carved == null)
                return;
            sr.sprite = original;
            Destroy(carved);
            Destroy(work);
            Destroy(source);
            carved = null;
            work = source = null;
            pixels = null;
        }

        bool Prepare()
        {
            if (work != null)
                return true;
            if (original == null)
                return false;
            Rect r = original.textureRect;
            width = Mathf.RoundToInt(r.width);
            height = Mathf.RoundToInt(r.height);
            if (width <= 0 || height <= 0)
                return false;

            // İçe aktarılan doku okunamaz: GPU'dan kopyalanır.
            Texture tex = original.texture;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            source = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            source.ReadPixels(new Rect(r.x, r.y, width, height), 0, 0);
            source.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            pixels = source.GetPixels32();
            work = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            work.SetPixels32(pixels);
            work.Apply();
            Vector2 pivot = new Vector2(original.pivot.x / width, original.pivot.y / height);
            carved = Sprite.Create(work, new Rect(0, 0, width, height), pivot, original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sr.sprite = carved;
            return true;
        }

        Vector2 ToPixel(Vector2 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            float ppu = original.pixelsPerUnit;
            float x = (sr.flipX ? -local.x : local.x) * ppu + original.pivot.x;
            return new Vector2(x, local.y * ppu + original.pivot.y);
        }

        Vector2 ToWorld(float px, float py)
        {
            float ppu = original.pixelsPerUnit;
            float lx = (px - original.pivot.x) / ppu;
            if (sr.flipX)
                lx = -lx;
            return transform.TransformPoint(new Vector3(lx, (py - original.pivot.y) / ppu, 0f));
        }

        /// <summary>Çentikli daire içindeki pikselleri koparır, parçalarını merkezden dışa ve yukarı savurur.</summary>
        public void Carve(Vector2 center, float radius, float strength)
        {
            if (!Prepare())
                return;
            float ppu = original.pixelsPerUnit;
            float scale = Mathf.Abs(transform.lossyScale.x);
            Vector2 c = ToPixel(center);
            float rPix = radius * ppu / Mathf.Max(0.01f, scale);
            float seed = Random.value * 100f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - rPix * 1.2f)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(c.x + rPix * 1.2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - rPix * 1.2f)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(c.y + rPix * 1.2f));
            if (x0 > x1 || y0 > y1)
                return;

            // Bloklar halinde dolaş: her blok ya tamamen kalır ya da kopar (kenar çentikli, piksel temiz).
            int chunks = 0, removed = 0;
            for (int by = y0 - y0 % Block; by <= y1; by += Block)
            for (int bx = x0 - x0 % Block; bx <= x1; bx += Block)
            {
                float cx = bx + Block / 2f - c.x, cy = by + Block / 2f - c.y;
                float angle = Mathf.Atan2(cy, cx);
                float jag = 0.7f + 0.5f * Mathf.PerlinNoise(angle * 2.3f + seed, seed);
                if (cx * cx + cy * cy > rPix * jag * rPix * jag)
                    continue;
                int opaque = 0;
                for (int y = by; y < by + Block && y < height; y++)
                for (int x = bx; x < bx + Block && x < width; x++)
                {
                    int i = y * width + x;
                    if (pixels[i].a > 0)
                    {
                        opaque++;
                        pixels[i] = new Color32(0, 0, 0, 0);
                    }
                }
                if (opaque == 0)
                    continue;
                removed++;
                if (opaque < 3 || chunks >= MaxChunksPerCarve)
                    continue;
                chunks++;
                int bw = Mathf.Min(Block, width - bx), bh = Mathf.Min(Block, height - by);
                Sprite piece = Sprite.Create(source, new Rect(bx, by, bw, bh), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
                Vector2 at = ToWorld(bx + bw / 2f, by + bh / 2f);
                Vector2 away = at - center;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized;
                Vector2 v = away * Random.Range(2f, 6f) * strength + Vector2.up * Random.Range(2.5f, 6.5f) * strength;
                Shatter.Spawn(piece, at, v, sr.color, scale, sr.sharedMaterial, sr.sortingOrder + 1);
            }
            if (removed == 0)
                return;
            work.SetPixels32(pixels);
            work.Apply();
            PixelBurst.Spawn(center, new Color32(0xB8, 0x9A, 0x74, 0xFF), 10, 3f, 0.5f); // toz
        }
    }
}
