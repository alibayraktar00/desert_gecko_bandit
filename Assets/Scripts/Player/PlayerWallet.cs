using System;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>Oyuncunun topladığı kristaller. Şimdilik sadece skor; ileride para birimi olacak.</summary>
    public class PlayerWallet : MonoBehaviour
    {
        public int Crystals { get; private set; }

        public event Action<int> Changed;

        public void Add(int amount)
        {
            if (amount <= 0)
                return;
            Crystals += amount;
            Changed?.Invoke(Crystals);
        }
    }
}
