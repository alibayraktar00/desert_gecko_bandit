using UnityEngine;

namespace DesertGecko.Core
{
    /// <summary>
    /// Fizik katmanlarının adları ve aralarındaki çarpışma kuralları.
    /// Oyuncu ile düşmanlar birbirinin içinden geçer; temas hasarı sorgu ile verilir, itişme olmaz.
    /// </summary>
    public static class GameLayers
    {
        public const string Ground = "Ground";
        public const string Player = "Player";
        public const string Enemy = "Enemy";
        public const string PlayerAttack = "PlayerAttack";

        // LayerMask.GetMask her çağrıda dizi oluşturur; düşmanlar bunu her fizik adımında kullanıyor.
        static int groundMask = -1, playerMask = -1;
        public static int GroundMask => groundMask >= 0 ? groundMask : groundMask = LayerMask.GetMask(Ground);
        public static int PlayerMask => playerMask >= 0 ? playerMask : playerMask = LayerMask.GetMask(Player);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureCollisions()
        {
            groundMask = playerMask = -1; // domain reload kapalıyken eski değer kalmasın

            Ignore(Player, Enemy);
            Ignore(Enemy, Enemy);
            Ignore(PlayerAttack, Player);
            Ignore(PlayerAttack, PlayerAttack);
        }

        static void Ignore(string a, string b)
        {
            int layerA = LayerMask.NameToLayer(a), layerB = LayerMask.NameToLayer(b);
            if (layerA < 0 || layerB < 0)
            {
                Debug.LogWarning($"[DesertGecko] '{(layerA < 0 ? a : b)}' katmanı yok. Desert Gecko → Test Sahnesini Kur katmanları ekler.");
                return;
            }
            Physics2D.IgnoreLayerCollision(layerA, layerB);
        }
    }
}
