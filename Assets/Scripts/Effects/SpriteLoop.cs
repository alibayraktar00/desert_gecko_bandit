using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.Effects
{
    /// <summary>Kareleri döngüyle oynatır; SpriteRenderer'da ya da arayüz Image'ında çalışır (ör. menüdeki Gila).</summary>
    public class SpriteLoop : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;
        [SerializeField] float fps = 6f;
        [Tooltip("Aynı kareleri oynatan nesneler birlikte oynamasın diye saniye cinsinden kayma")]
        [SerializeField] float offset;
        [Tooltip("Bir tur bitince bu kadar saniye görünmez bekler (fışkırma gibi ara ara olanlar için)")]
        [SerializeField] float pause;

        public void Configure(Sprite[] loopFrames, float framesPerSecond, float timeOffset = 0f, float pauseAfter = 0f)
        {
            frames = loopFrames;
            fps = framesPerSecond;
            offset = timeOffset;
            pause = pauseAfter;
        }

        SpriteRenderer spriteRenderer;
        Image image;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            image = GetComponent<Image>();
        }

        void Update()
        {
            if (frames == null || frames.Length == 0)
                return;
            float time = Time.unscaledTime + offset;
            if (pause > 0f)
            {
                float play = frames.Length / fps;
                time %= play + pause;
                bool visible = time < play;
                if (spriteRenderer != null) spriteRenderer.enabled = visible;
                if (!visible)
                    return;
            }
            Sprite frame = frames[Mathf.FloorToInt(time * fps) % frames.Length];
            if (spriteRenderer != null) spriteRenderer.sprite = frame;
            if (image != null) image.sprite = frame;
        }
    }
}
