using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.Effects
{
    /// <summary>Kareleri döngüyle oynatır; SpriteRenderer'da ya da arayüz Image'ında çalışır (ör. menüdeki Gila).</summary>
    public class SpriteLoop : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;
        [SerializeField] float fps = 6f;

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
            Sprite frame = frames[Mathf.FloorToInt(Time.unscaledTime * fps) % frames.Length];
            if (spriteRenderer != null) spriteRenderer.sprite = frame;
            if (image != null) image.sprite = frame;
        }
    }
}
