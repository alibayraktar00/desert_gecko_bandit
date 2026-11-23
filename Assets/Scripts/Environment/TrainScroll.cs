using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Film usulü tren yolculuğunun ortak durumu: tren yerinde durur, dünya geriye akar. Offset dünyanın şimdiye kadar
    /// ne kadar geriye kaydığıdır (birim). Arka plan katmanları (ParallaxLayer) uzaklıklarına göre bundan pay alır.
    /// TrainRide sürer; sahne her yüklendiğinde sıfırlanır.
    /// </summary>
    public static class TrainScroll
    {
        /// <summary>Dünyanın toplam geriye kayması (birim).</summary>
        public static float Offset { get; private set; }
        /// <summary>Trenin hızı (birim/sn): dünya bu hızla geriye akar.</summary>
        public static float Speed { get; set; }

        public static void Advance(float dt) => Offset += Speed * dt;

        public static void Reset()
        {
            Offset = 0f;
            Speed = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            Reset();
            SceneManager.sceneLoaded += (_, _) => Reset();
        }
    }
}
