using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Etrafa saçılıp sönen küçük kare pikseller. Ölüm, isabet ve toplama anları için
    /// hazır efekt; prefab gerektirmez.
    /// </summary>
    public class PixelBurst : MonoBehaviour
    {
        const float Gravity = 14f;
        static Sprite pixelSprite;

        Transform[] parts;
        SpriteRenderer[] renderers;
        Vector2[] velocities;
        Color color;
        float lifetime;
        float age;

        public static void Spawn(Vector2 position, Color color, int count = 8, float speed = 4f, float lifetime = 0.45f)
        {
            var go = new GameObject("PixelBurst");
            go.transform.position = position;
            go.AddComponent<PixelBurst>().Init(color, count, speed, lifetime);
        }

        void Init(Color burstColor, int count, float speed, float life)
        {
            if (pixelSprite == null)
                pixelSprite = CreatePixelSprite();

            color = burstColor;
            lifetime = life;
            parts = new Transform[count];
            renderers = new SpriteRenderer[count];
            velocities = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var part = new GameObject("p");
                part.transform.SetParent(transform, false);
                var sr = part.AddComponent<SpriteRenderer>();
                sr.sprite = pixelSprite;
                sr.color = color;
                sr.sortingOrder = 20;
                parts[i] = part.transform;
                renderers[i] = sr;

                float angle = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * Random.Range(0.5f, 1f) + Vector2.up * 1.5f;
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            Color faded = color;
            faded.a = color.a * (1f - age / lifetime);
            for (int i = 0; i < parts.Length; i++)
            {
                velocities[i].y -= Gravity * Time.deltaTime;
                parts[i].localPosition += (Vector3)(velocities[i] * Time.deltaTime);
                renderers[i].color = faded;
            }
        }

        // 2x2 piksel = oyun ölçeğinde 1/16 birim
        static Sprite CreatePixelSprite()
        {
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
            var white = new Color32(255, 255, 255, 255);
            tex.SetPixels32(new[] { white, white, white, white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
