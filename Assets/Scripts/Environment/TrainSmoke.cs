using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Lokomotifin bacası: duman öbekleri çıkarır. Dururken ağır ağır, yolda hızla "çuf çuf"; öbekler dünyayla
    /// birlikte geriye kayar, uzun bir kuyruk olur. Kareler pixelfx'in fire/train-smoke efektinden.
    /// </summary>
    public class TrainSmoke : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;
        [SerializeField] float fps = 15f;
        [SerializeField] float idleInterval = 1.1f;
        [SerializeField] float fastInterval = 0.16f;
        [SerializeField] int sortingOrder = -4;

        float nextAt;
        int count;

        /// <summary>Kaza sonrası: baca artık tütmez.</summary>
        public bool Stopped { get; set; }

        void Update()
        {
            if (Stopped || frames == null || frames.Length == 0 || Time.time < nextAt)
                return;
            float speed = TrainScroll.Speed;
            float k = Mathf.Clamp01(speed / 14f);
            nextAt = Time.time + Mathf.Lerp(idleInterval, fastInterval, k);
            count++;
            // Pixelfx efekti rüzgârı sola çizer; hızlanınca dünya da geriye aktığından öbek trenden kopar gider.
            FrameFx puff = FrameFx.Spawn(frames, fps * Mathf.Lerp(0.7f, 1.2f, k), transform.position, sortingOrder + (count % 2), count % 3 == 0,
                Mathf.Lerp(0.8f, 1.15f, (count * 0.37f) % 1f));
            if (puff != null)
                puff.Velocity = new Vector2(-speed * 0.85f, Mathf.Lerp(0.8f, 0.2f, k));
        }
    }
}
