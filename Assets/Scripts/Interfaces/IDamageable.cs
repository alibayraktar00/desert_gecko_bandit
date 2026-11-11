using UnityEngine;

namespace DesertGecko.Core
{
    /// <summary>Hasar alabilen her şey: oyuncu ve düşmanlar.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Darbenin miktarı, yönü ve türü. Düşmanlar zırh, savuşturma ve darbe korumasını türe göre işler.</summary>
        /// <returns>Darbe işlediyse true; zırhtan/dikenden sektiyse, savuşturulduysa ya da korumaya takıldıysa false
        /// (mermi o zaman seker).</returns>
        bool TakeDamage(DamageInfo info);
    }
}
