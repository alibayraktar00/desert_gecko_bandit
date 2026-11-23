using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Kare dizisini bir kez oynatıp kendini silen efekt (pixelfx'ten aktarılan duman, patlama, toz). İstenirse
    /// oynarken bir hızla kayar: trenin bacasından çıkan duman, dünya geriye akarken onunla birlikte geriye gider.
    /// Kareler alt orta pivotlu: nesnenin konumu efektin orijinidir (patlamanın zemini, bacanın ağzı).
    /// </summary>
    public class FrameFx : MonoBehaviour
    {
        Sprite[] frames;
        float fps;
        float startedAt;
        bool unscaled;
        SpriteRenderer spriteRenderer;

        /// <summary>Dünya birimi/sn cinsinden kayma; dışarıdan her kare değiştirilebilir.</summary>
        public Vector2 Velocity { get; set; }

        public static FrameFx Spawn(Sprite[] frames, float fps, Vector2 position, int sortingOrder, bool flip = false, float scale = 1f, bool unscaledTime = false)
        {
            if (frames == null || frames.Length == 0)
                return null;
            var go = new GameObject("FrameFx");
            go.transform.position = position;
            go.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1f);
            var fx = go.AddComponent<FrameFx>();
            fx.frames = frames;
            fx.fps = fps;
            fx.unscaled = unscaledTime;
            fx.startedAt = unscaledTime ? Time.unscaledTime : Time.time;
            fx.spriteRenderer = go.AddComponent<SpriteRenderer>();
            fx.spriteRenderer.sortingOrder = sortingOrder;
            fx.spriteRenderer.sprite = frames[0];
            return fx;
        }

        void Update()
        {
            float now = unscaled ? Time.unscaledTime : Time.time;
            int frame = Mathf.FloorToInt((now - startedAt) * fps);
            if (frame >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            spriteRenderer.sprite = frames[frame];
            transform.position += (Vector3)(Velocity * (unscaled ? Time.unscaledDeltaTime : Time.deltaTime));
        }
    }
}
