using DesertGecko.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>Sol üstteki kalpler: her kalp 2 can birimi (dolu / yarım / boş).</summary>
    public class HealthUI : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;
        [SerializeField] Image[] hearts;
        [SerializeField] Sprite fullHeart;
        [SerializeField] Sprite halfHeart;
        [SerializeField] Sprite emptyHeart;

        void OnEnable()
        {
            health.Damaged += OnHealthChanged;
            health.Healed += OnHealthChanged;
            health.Respawned += Refresh;
        }

        void OnDisable()
        {
            health.Damaged -= OnHealthChanged;
            health.Healed -= OnHealthChanged;
            health.Respawned -= Refresh;
        }

        // PlayerHealth canını Awake'te dolduruyor; OnEnable'da henüz hazır olmayabilir.
        void Start()
        {
            EnsureHeartCount();
            Refresh();
        }

        /// <summary>
        /// Kalp sayısı canla belirlenir (her kalp 2 birim). Eski kurulmuş sahnelerde daha az kalp varsa
        /// sonuncusu aynı aralıkla kopyalanır: can artınca sahneleri yeniden kurmak gerekmez.
        /// </summary>
        void EnsureHeartCount()
        {
            int needed = (health.MaxHealth + 1) / 2;
            if (hearts == null || hearts.Length == 0 || hearts.Length >= needed)
                return;
            var list = new System.Collections.Generic.List<Image>(hearts);
            RectTransform last = hearts[hearts.Length - 1].rectTransform;
            float step = hearts.Length > 1
                ? last.anchoredPosition.x - hearts[hearts.Length - 2].rectTransform.anchoredPosition.x
                : last.sizeDelta.x + 2f;
            for (int i = hearts.Length; i < needed; i++)
            {
                Image copy = Instantiate(hearts[hearts.Length - 1], last.parent);
                copy.name = $"Kalp_{i + 1}";
                copy.rectTransform.anchoredPosition = last.anchoredPosition + new Vector2(step * (i - hearts.Length + 1), 0f);
                list.Add(copy);
            }
            hearts = list.ToArray();
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(Mathf.Max(root.sizeDelta.x, step * needed), root.sizeDelta.y);
        }

        void OnHealthChanged(int _) => Refresh();

        public void Refresh()
        {
            int hp = health.CurrentHealth;
            for (int i = 0; i < hearts.Length; i++)
            {
                int inThisHeart = Mathf.Clamp(hp - i * 2, 0, 2);
                hearts[i].sprite = inThisHeart == 2 ? fullHeart : inThisHeart == 1 ? halfHeart : emptyHeart;
            }
        }
    }
}
