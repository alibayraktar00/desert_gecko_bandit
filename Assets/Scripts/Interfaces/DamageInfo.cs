using UnityEngine;

namespace DesertGecko.Core
{
    /// <summary>Darbenin kaynağı. Zırh, savuşturma ve darbe koruması buna göre karar verir.</summary>
    public enum DamageKind
    {
        /// <summary>Düşman gövdesine çarpma, düşman saldırıları.</summary>
        Contact,
        /// <summary>Ateş, düşen kaya, uçurum gibi çevre tehlikeleri.</summary>
        Hazard,
        /// <summary>Gila'nın dili.</summary>
        Tongue,
        /// <summary>Fırlatılan kuyruk.</summary>
        Tail,
        /// <summary>Silah mermisi, saçma, diken.</summary>
        Bullet,
        /// <summary>Dinamit gibi alan hasarı.</summary>
        Explosion,
        /// <summary>Bıçak, kazma gibi elde tutulan silahlar.</summary>
        Melee,
        /// <summary>Mermisi biten silahın fırlatılması.</summary>
        Thrown,
    }

    /// <summary>Tek bir darbe: miktar (yarım kalp = 1), geri tepme yönü ve tür.</summary>
    public readonly struct DamageInfo
    {
        public readonly int Amount;
        /// <summary>Hasarın geldiği yönün tersi; sadece işareti/yönü kullanılır.</summary>
        public readonly Vector2 Direction;
        public readonly DamageKind Kind;
        /// <summary>
        /// Aynı atışın parçaları (saçma taneleri) aynı numarayı taşır: hepsi ayrı ayrı vurur, kısa darbe korumasına
        /// takılmaz. Yakından bütün taneler isabet eder, uzaktan dağılıp birkaçı. 0 = tek parça.
        /// </summary>
        public readonly int ShotId;

        public DamageInfo(int amount, Vector2 direction, DamageKind kind, int shotId = 0)
        {
            Amount = amount;
            Direction = direction;
            Kind = kind;
            ShotId = shotId;
        }

        /// <summary>
        /// Silah darbeleri (mermi, patlama, fırlatma, kazma gibi yakın dövüş silahı). Uzun darbe korumasına takılmazlar:
        /// hızlarını silahın kendi atış/sallama aralığı sınırlar, yoksa ikinci vuruş boşa gider. Dil ve kuyruk yavaş sayılır.
        /// </summary>
        public bool IsRapid => Kind == DamageKind.Bullet || Kind == DamageKind.Explosion || Kind == DamageKind.Thrown
            || Kind == DamageKind.Melee;
    }
}
