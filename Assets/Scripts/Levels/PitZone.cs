using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Levels
{
    /// <summary>
    /// Uçurumun içindeki görünmez bölge: oyuncu kenarın biraz altına inince düşmüş sayılır.
    /// Böylece level ne kadar yüksekte olursa olsun uzun uzun boşluğa düşülmez.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PitZone : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerHealth>() != null && LevelManager.Current != null)
                LevelManager.Current.PlayerFell();
        }
    }
}
