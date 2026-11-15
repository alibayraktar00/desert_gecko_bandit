using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>Sahte kaya: içinden geçilir, oyuncu içindeyken saydamlaşıp arkadaki gizli odayı gösterir.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class SecretWall : MonoBehaviour
    {
        [SerializeField] float hiddenAlpha = 0.12f;
        [SerializeField] float fadeSpeed = 4f;
        [Tooltip("Odayı belli eden ipuçları: altından sızan toz ve çatlaktan çakan altın pırıltısı (sn aralık)")]
        [SerializeField] Vector2 dustEvery = new Vector2(0.7f, 1.6f);
        [SerializeField] Vector2 glintEvery = new Vector2(2.5f, 5f);
        [SerializeField] Color dustColor = new Color32(0x8A, 0x6E, 0x4E, 0xFF);
        [SerializeField] Color glintColor = new Color32(0xFF, 0xD8, 0x5A, 0xFF);

        float nextDustAt, nextGlintAt;
        BoxCollider2D box;

        SpriteRenderer[] renderers;
        int inside;
        float alpha = 1f;

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            renderers = GetComponentsInChildren<SpriteRenderer>();
            nextDustAt = Time.time + Random.Range(dustEvery.x, dustEvery.y);
            nextGlintAt = Time.time + Random.Range(glintEvery.x, glintEvery.y);
        }

        /// <summary>Kaya sahte: altından ara ara toz ve çakıl sızar, çatlaklarından altın pırıltısı çakar.</summary>
        void Hints()
        {
            if (inside > 0)
                return;
            Bounds b = box.bounds;
            if (Time.time >= nextDustAt)
            {
                nextDustAt = Time.time + Random.Range(dustEvery.x, dustEvery.y);
                PixelBurst.Spawn(new Vector2(Random.Range(b.min.x + 0.4f, b.max.x - 0.4f), b.min.y - 0.15f), dustColor, 2, 0.8f, 0.6f);
            }
            if (Time.time >= nextGlintAt)
            {
                nextGlintAt = Time.time + Random.Range(glintEvery.x, glintEvery.y);
                PixelBurst.Spawn(new Vector2(Random.Range(b.min.x + 0.5f, b.max.x - 0.5f), b.min.y + Mathf.Min(b.size.y * 0.6f, 2.2f)),
                    glintColor, 3, 0.9f, 0.35f);
            }
        }

        void Update()
        {
            Hints();
            float target = inside > 0 ? hiddenAlpha : 1f;
            if (Mathf.Approximately(alpha, target))
                return;
            alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.deltaTime);
            foreach (SpriteRenderer sr in renderers)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerHealth>() != null)
                inside++;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerHealth>() != null)
                inside = Mathf.Max(0, inside - 1);
        }
    }
}
