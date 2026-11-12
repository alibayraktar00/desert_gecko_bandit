using System.Collections.Generic;
using DesertGecko.Effects;
using DesertGecko.Enemies;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Dövüş odası: oyuncu içeri girince arkadaki tavan kapısı çakılır, bölgedeki düşmanlar ölünce kapılar kalkar.
    /// Oyuncu ölürse her şey başa döner (düşmanlar zaten kendini sıfırlar). Kapıların hareketi ve efektleri CeilingGate'te.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ArenaGate : MonoBehaviour
    {
        [SerializeField] Transform entrance;
        [SerializeField] Transform exit;
        [Tooltip("Açılınca kapının yukarı kalktığı mesafe")]
        [SerializeField] float openHeight = 6f;
        [Tooltip("Bu dünyadaki bölgede başlayan düşmanlar sayılır: x en az, x en çok")]
        [SerializeField] Vector2 enemyRangeX;
        [SerializeField] Vector2 enemyRangeY;
        [SerializeField] Color burstColor = new Color32(0xF4, 0xD3, 0x5E, 0xFF);

        public bool IsLocked => locked && !cleared;
        public bool IsCleared => cleared;

        readonly List<EnemyBase> enemies = new List<EnemyBase>();
        CeilingGate entranceGate, exitGate;
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
            entranceGate = GateOf(entrance);
            exitGate = GateOf(exit);
            entranceGate.Snap(true); // giriş başta açık
            exitGate.Snap(false);
            PlayerHealth.PlayerRespawned += ResetRoom;
        }

        CeilingGate GateOf(Transform door)
        {
            // Eski kurulmuş sahnelerde kapıda bileşen yok: efektsiz de olsa çalışsın.
            var gate = door.GetComponent<CeilingGate>();
            if (gate == null)
                gate = door.gameObject.AddComponent<CeilingGate>();
            gate.SetOpenHeight(openHeight);
            return gate;
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
                PixelBurst.Spawn(exit.position + Vector3.down * (openHeight / 2f), burstColor, 14, 5f, 0.6f);
                entranceGate.Open();
                exitGate.Open();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!locked && other.GetComponentInParent<PlayerHealth>() != null)
            {
                locked = true;
                entranceGate.Close();
            }
        }

        void ResetRoom()
        {
            locked = false;
            cleared = false;
            entranceGate.Snap(true);
            exitGate.Snap(false);
        }
    }
}
