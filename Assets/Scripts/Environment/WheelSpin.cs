using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>Tren tekerleği: dönme karelerini trenin hızına göre oynatır (yarıçap büyüdükçe yavaş döner).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WheelSpin : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;
        [Tooltip("Tekerleğin yarıçapı (birim)")]
        [SerializeField] float radius = 0.33f;

        SpriteRenderer spriteRenderer;
        float turns;

        /// <summary>Kaza: tekerlek boşta döner, gittikçe yavaşlar (trenin hızından bağımsız).</summary>
        public float FreeSpin { get; set; } = -1f;

        void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        void Update()
        {
            if (frames == null || frames.Length == 0)
                return;
            float speed = FreeSpin >= 0f ? FreeSpin : TrainScroll.Speed;
            turns += speed / (2f * Mathf.PI * radius) * Time.deltaTime;
            spriteRenderer.sprite = frames[(int)Mathf.Repeat(Mathf.Floor(turns * frames.Length), frames.Length)];
        }
    }
}
