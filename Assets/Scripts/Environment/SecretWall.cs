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

        SpriteRenderer[] renderers;
        int inside;
        float alpha = 1f;

        void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        void Update()
        {
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
