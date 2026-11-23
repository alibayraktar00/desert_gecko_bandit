using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Film usulü tren yolculuğu. Tren dünyada yerinde durur; Gila vagonlardan birine binince düdük öter, tren
    /// hızlanır ve dünya geriye akmaya başlar: peron geride kalır, ray yatağı ve direkler akar, arka plan
    /// katmanları uzaklıklarına göre kayar (TrainScroll), tekerlekler döner, vagonlar ray eklerinde zıplar.
    /// Kaza sahnesi (TrainCrash) devralınca hızı o sürer.
    /// </summary>
    public class TrainRide : MonoBehaviour
    {
        [Header("Hız")]
        [SerializeField] float topSpeed = 16f;
        [SerializeField] float accelTime = 5f;

        [Header("Biniş")]
        [Tooltip("Trenin kapladığı dünya x aralığı: Gila bunun içinde ve tabanın üstünde yere basınca tren kalkar")]
        [SerializeField] float trainMinX;
        [SerializeField] float trainMaxX;
        [SerializeField] float floorY = 0.7f;
        [Tooltip("Kalkışta oyuncunun dönüş noktası buraya taşınır (peron geride kalacak)")]
        [SerializeField] Vector2 boardCheckpoint;

        [Header("Ses")]
        [SerializeField] AudioSource railLoop;
        [SerializeField] AudioClip whistle;

        PlayerController player;
        PlayerHealth health;
        float departedAt = -1f;

        public bool Departed => departedAt >= 0f;
        public float TopSpeed => topSpeed;
        public float TrainMinX => trainMinX;
        public float TrainMaxX => trainMaxX;
        /// <summary>Kaza sahnesi hızı kendisi sürer.</summary>
        public bool Controlled { get; set; }

        void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player != null)
                health = player.GetComponent<PlayerHealth>();
            if (railLoop != null)
            {
                railLoop.loop = true;
                railLoop.volume = 0f;
                railLoop.Play();
            }
        }

        void Update()
        {
            if (!Departed && player != null && ShouldDepart())
                Depart();

            if (Departed && !Controlled)
            {
                float k = Mathf.Clamp01((Time.time - departedAt) / accelTime);
                TrainScroll.Speed = topSpeed * k * k * (3f - 2f * k); // yavaş kalkar, sonra açılır
            }
            TrainScroll.Advance(Time.deltaTime);

            if (railLoop != null)
            {
                float s = Mathf.Clamp01(TrainScroll.Speed / topSpeed);
                railLoop.volume = s * 0.55f;
                railLoop.pitch = 0.55f + 0.6f * s;
            }
        }

        bool ShouldDepart()
        {
            Vector2 p = player.transform.position;
            return player.IsGrounded && p.x > trainMinX && p.x < trainMaxX && p.y > floorY - 0.1f;
        }

        void Depart()
        {
            departedAt = Time.time;
            if (whistle != null)
                Effects.OneShotSound.Play(whistle);
            if (health != null)
                health.SetCheckpoint(boardCheckpoint);
        }

        /// <summary>Kaza sonrası: tren durdu, ses kesildi, dünya akmıyor.</summary>
        public void Halt()
        {
            Controlled = true;
            TrainScroll.Speed = 0f;
            if (railLoop != null)
                railLoop.Stop();
        }
    }
}
