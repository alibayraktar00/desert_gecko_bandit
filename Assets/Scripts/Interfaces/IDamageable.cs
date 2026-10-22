using UnityEngine;

namespace DesertGecko.Core
{
    /// <summary>Hasar alabilen her şey: oyuncu ve düşmanlar.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <param name="amount">Yarım kalp = 1 birim.</param>
        /// <param name="knockbackDirection">Hasarın geldiği yönün tersi; sadece işareti/yönü kullanılır.</param>
        void TakeDamage(int amount, Vector2 knockbackDirection);
    }
}
