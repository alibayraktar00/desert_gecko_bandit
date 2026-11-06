using System.Collections.Generic;
using DesertGecko.Effects;
using DesertGecko.Enemies;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Dövüş odası: oyuncu içeri girince arkadaki kapı iner, bölgedeki düşmanlar ölünce kapılar açılır.
    /// Oyuncu ölürse her şey başa döner (düşmanlar zaten kendini sıfırlar).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ArenaGate : MonoBehaviour
    {
        [SerializeField] Transform entrance;
        [SerializeField] Transform exit;
        [Tooltip("Açılınca kapının yukarı kalktığı mesafe")]
        [SerializeField] float openHeight = 6f;
        [SerializeField] float moveSpeed = 9f;
        [Tooltip("Bu dünyadaki bölgede başlayan düşmanlar sayılır: x en az, x en çok")]
        [SerializeField] Vector2 enemyRangeX;
        [SerializeField] Vector2 enemyRangeY;
        [SerializeField] Color burstColor = new Color32(0xF4, 0xD3, 0x5E, 0xFF);

        public bool IsLocked => locked && !cleared;
        public bool IsCleared => cleared;

        readonly List<EnemyBase> enemies = new List<EnemyBase>();
        Vector3 entranceDown, exitDown;
        bool locked, cleared;

        public void Configure(Transform entranceGate, Transform exitGate, float height, Vector2 rangeX, Vector2 rangeY)
        {
            entrance = entranceGate;
            exit = exitGate;
            openHeight = height;
            enemyRangeX = rangeX;
            enemyRangeY = rangeY;
        }

        void Awake()
        {
            entranceDown = entrance.position;
            exitDown = exit.position;
            entrance.position = entranceDown + Vector3.up * openHeight; // giriş başta açık
            PlayerHealth.PlayerRespawned += ResetRoom;
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= ResetRoom;
        }

        void Start()
        {
            foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsInactive.Include))
            {
                Vector2 p = e.transform.position;
                if (p.x >= enemyRangeX.x && p.x <= enemyRangeX.y && p.y >= enemyRangeY.x && p.y <= enemyRangeY.y)
                    enemies.Add(e);
            }
        }

        void Update()
        {
            if (locked && !cleared && enemies.TrueForAll(e => !e.IsAlive))
            {
                cleared = true;
                PixelBurst.Spawn(exitDown + Vector3.up * 1.5f, burstColor, 14, 5f, 0.6f);
            }

            bool entranceOpen = !locked || cleared;
            Slide(entrance, entranceDown, entranceOpen);
            Slide(exit, exitDown, cleared);
        }

        void Slide(Transform gate, Vector3 down, bool open)
        {
            Vector3 target = open ? down + Vector3.up * openHeight : down;
            gate.position = Vector3.MoveTowards(gate.position, target, moveSpeed * Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!locked && other.GetComponentInParent<PlayerHealth>() != null)
                locked = true;
        }

        void ResetRoom()
        {
            locked = false;
            cleared = false;
        }
    }
}
