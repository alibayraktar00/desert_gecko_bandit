using System;
using DesertGecko.Core;
using DesertGecko.Environment;
using DesertGecko.Player;
using DesertGecko.UI;
using UnityEngine;

namespace DesertGecko.Levels
{
    /// <summary>
    /// Her level sahnesinde bir tane: uçuruma düşmeyi yönetir, level bitince ilerlemeyi kaydeder
    /// ve level sonu ekranını açar. Hangi level olduğunu sahne adından (LevelCatalog) bulur.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] CameraFollow cameraFollow;
        [SerializeField] LevelCompleteUI completeUI;
        [Tooltip("Oyuncu bu yüksekliğin altına düşerse uçuruma düşmüş sayılır")]
        [SerializeField] float killY = -14f;
        [SerializeField] int pitDamage = 2;

        public static LevelManager Current { get; private set; }

        public int LevelIndex { get; private set; } = -1;
        public bool IsComplete { get; private set; }
        public PlayerController Player => player;

        public event Action Completed;

        PlayerHealth health;
        PlayerWallet wallet;

        void Awake()
        {
            Current = this;
            LevelIndex = LevelCatalog.IndexOf(gameObject.scene.name);
            if (LevelIndex < 0)
                Debug.LogError($"[DesertGecko] '{gameObject.scene.name}' sahnesi LevelCatalog'da yok; ilerleme kaydedilmeyecek.");
            if (player != null)
            {
                health = player.GetComponent<PlayerHealth>();
                wallet = player.GetComponent<PlayerWallet>();
            }
        }

        void OnEnable()
        {
            if (health != null)
                health.Respawned += SnapCamera;
        }

        void OnDisable()
        {
            if (health != null)
                health.Respawned -= SnapCamera;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        void Start()
        {
            if (LevelIndex >= 0)
                GameProgress.LastPlayed = LevelIndex;
        }

        void Update()
        {
            if (player != null && player.transform.position.y < killY)
                PlayerFell();
        }

        /// <summary>Oyuncu uçuruma düştü: can yakar, ölmediyse son checkpoint'e döner.</summary>
        public void PlayerFell()
        {
            if (IsComplete || player == null || health == null)
                return;

            bool wasAlive = health.IsAlive;
            health.FallIntoPit(pitDamage);
            if (wasAlive && health.IsAlive)
                SnapCamera();
        }

        /// <summary>Bitiş noktasına ulaşıldı: oyuncuyu durdur, kaydet, level sonu ekranını göster.</summary>
        /// <returns>Level gerçekten şimdi tamamlandıysa true.</returns>
        public bool CompleteLevel()
        {
            if (IsComplete || player == null || (health != null && !health.IsAlive))
                return false;
            IsComplete = true;

            player.MovementLocked = true;
            player.Frozen = true; // düşmanlar artık dokunamaz
            // Saldırı coroutine'i bitince hareket kilidini açıyor; girdiyi ve saldırıları tamamen kapat.
            SetEnabled<PlayerInputReader>(false);
            SetEnabled<TongueAttack>(false);
            SetEnabled<TailAttack>(false);

            int crystals = wallet != null ? wallet.Crystals : 0;
            int previousBest = LevelIndex >= 0 ? GameProgress.BestCrystals(LevelIndex) : 0;
            if (LevelIndex >= 0)
                GameProgress.CompleteLevel(LevelIndex, crystals);

            Completed?.Invoke();
            if (completeUI != null)
                completeUI.Show(LevelIndex, crystals, previousBest > 0 && crystals > previousBest);
            return true;
        }

        void SetEnabled<T>(bool value) where T : Behaviour
        {
            var component = player.GetComponent<T>();
            if (component != null)
                component.enabled = value;
        }

        void SnapCamera()
        {
            if (cameraFollow != null)
                cameraFollow.SnapToTarget();
        }
    }
}
