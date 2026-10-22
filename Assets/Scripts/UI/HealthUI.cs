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
        void Start() => Refresh();

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
