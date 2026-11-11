namespace DesertGecko.Core
{
    /// <summary>Dikenli düşman: dikenleri varken dili ona değen Gila'nın canı yanar.</summary>
    public interface IPrickly
    {
        /// <summary>Şu an dikenli mi (dil değerse can yakar).</summary>
        bool PricksTongue { get; }

        /// <summary>Dile batan diken hasarı. Yarım kalp = 1 birim.</summary>
        int PrickDamage { get; }
    }
}
