using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>
    /// Level'a yerleştirilmiş silah: oyun başında yerde durur, süresi dolmaz. Alınırsa Gila ölüp checkpoint'te
    /// doğunca yeniden konur (düşmanlar dirilince silahlarını yeniden düşürdüğü gibi). Örneğin diken mutantından
    /// önce bırakılmış madenci kazması: dikenleri kırmanın yolunu gösterir.
    /// </summary>
    public class WeaponSpawner : MonoBehaviour
    {
        [SerializeField] WeaponData weapon;

        WeaponPickup current;

        void Start() => Spawn();

        void OnEnable() => PlayerHealth.PlayerRespawned += Respawn;
        void OnDisable() => PlayerHealth.PlayerRespawned -= Respawn;

        void Respawn()
        {
            if (current == null)
                Spawn();
        }

        void Spawn()
        {
            if (weapon == null)
                return;
            current = WeaponPickup.Spawn(weapon, weapon.Ammo, (Vector2)transform.position + Vector2.up * 0.4f, Vector2.zero, 0f, 0.25f);
            if (current != null)
                current.MakePermanent();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.3f, new Vector3(0.8f, 0.5f, 0f));
        }
    }
}
