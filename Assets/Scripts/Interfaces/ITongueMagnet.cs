using System.Collections.Generic;
using UnityEngine;

namespace DesertGecko.Core
{
    /// <summary>
    /// Dilin böcek gibi kıvrılıp yapıştığı düşman (havada süzülen, dilin düz yoluna denk gelmeyen hedefler).
    /// Dilin yolu bu düşmanın yakalama çemberine değerse dil ona doğru uzanır ve vurur; tam isabet gerekmez.
    /// </summary>
    public interface ITongueMagnet : IDamageable
    {
        /// <summary>Dilin ucunun yapıştığı nokta (gövdenin ortası).</summary>
        Vector2 TongueAnchor { get; }
        /// <summary>Dilin yolu bu yarıçapa değerse dil kıvrılır.</summary>
        float TongueCatchRadius { get; }
    }

    /// <summary>Sahnedeki etkin dil mıknatısları (düşman OnEnable/OnDisable ile kaydolur).</summary>
    public static class TongueMagnets
    {
        static readonly List<ITongueMagnet> active = new List<ITongueMagnet>();
        public static IReadOnlyList<ITongueMagnet> Active => active;

        public static void Register(ITongueMagnet magnet)
        {
            if (!active.Contains(magnet))
                active.Add(magnet);
        }

        public static void Unregister(ITongueMagnet magnet) => active.Remove(magnet);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear() => active.Clear(); // domain reload kapalıyken eski sahneden kalmasın
    }
}
