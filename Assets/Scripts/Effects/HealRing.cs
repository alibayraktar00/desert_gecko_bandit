using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Şifa dalgası: Gila'nın gövdesinden hızla genişleyip sönen ince, parlak yeşil halka.
    /// Gila'yı izler; can kazandığını oyuncuya doğrudan hissettirir.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HealRing : MonoBehaviour
    {
        const float Duration = 0.45f, StartSize = 0.4f, EndSize = 2.6f;
        static readonly Vector3 BodyOffset = new Vector3(0f, 0.5f, 0f);

        Transform follow;
        SpriteRenderer sr;
        Color color;
        float spriteSize = 1f;
        float age;

        public static void Spawn(Transform follow, Sprite sprite, Color color, Material material = null, int order = 12)
        {
            if (sprite == null || follow == null)
                return;
            var ring = new GameObject("SifaHalkasi").AddComponent<HealRing>();
            ring.follow = follow;
            ring.color = color;
            ring.sr = ring.GetComponent<SpriteRenderer>();
            ring.sr.sprite = sprite;
            if (material != null)
                ring.sr.sharedMaterial = material;
            ring.sr.sortingOrder = order;
            ring.sr.color = color;
            ring.spriteSize = Mathf.Max(0.01f, sprite.bounds.size.x);
            ring.Apply(0f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Duration);
            Apply(t);
            if (t >= 1f)
                Destroy(gameObject);
        }

        void Apply(float t)
        {
            if (follow != null)
                transform.position = follow.position + BodyOffset;
            float eased = 1f - (1f - t) * (1f - t); // hızlı başlar, yavaşlayarak açılır
            transform.localScale = Vector3.one * (Mathf.Lerp(StartSize, EndSize, eased) / spriteSize);
            Color c = color;
            c.a = color.a * (1f - t) * (1f - t);
            sr.color = c;
        }
    }
}
