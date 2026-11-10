using DesertGecko.Effects;
using DesertGecko.Levels;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Baca boyunca yükselen kaynar çamur. Oyuncu tetik hizasını geçince kabarmaya başlar; değerse can yakar ve oyuncu
    /// checkpoint'e döner, çamur da dibine çekilir. Oyuncu çok önden gidiyorsa hızlanır, baskı kaybolmasın.
    /// </summary>
    public class RisingMud : MonoBehaviour
    {
        [SerializeField] float speed = 1.5f;
        [Tooltip("Oyuncu bu kadar yukarıdaysa çamur hızlanır")]
        [SerializeField] float farAhead = 6f;
        [SerializeField] float catchUpFactor = 1.9f;
        [SerializeField] float startY, topY;
        [SerializeField] Vector2 triggerX;
        [SerializeField] float triggerY;
        [Tooltip("Ayakların çamura bu kadar girince yanar")]
        [SerializeField] float sinkDepth = 0.35f;
        [SerializeField] Color bubbleColor = new Color32(0xF0, 0x78, 0x28, 0xFF);

        PlayerHealth player;
        bool running;
        float nextBubble;
        public bool IsRunning => running;
        public float SurfaceY => transform.position.y;

        public void Configure(float bottom, float top, Vector2 triggerRangeX, float triggerAtY, float riseSpeed)
        {
            startY = bottom;
            topY = top;
            triggerX = triggerRangeX;
            triggerY = triggerAtY;
            speed = riseSpeed;
        }

        void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
            if (player != null)
                player.ReturnedToCheckpoint += ResetMud;
        }

        void OnDestroy()
        {
            if (player != null)
                player.ReturnedToCheckpoint -= ResetMud;
        }

        void ResetMud()
        {
            running = false;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, startY, p.z);
        }

        void Update()
        {
            if (player == null || !player.IsAlive)
                return;
            Vector2 at = player.transform.position;
            if (!running)
            {
                if (at.x >= triggerX.x && at.x <= triggerX.y && at.y >= triggerY && transform.position.y < topY)
                    running = true;
                return;
            }

            float y = transform.position.y;
            if (y < topY)
            {
                float rate = at.y - y > farAhead ? speed * catchUpFactor : speed;
                y = Mathf.Min(topY, y + rate * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, y, transform.position.z);
            }

            if (Time.time >= nextBubble)
            {
                nextBubble = Time.time + 0.12f;
                PixelBurst.Spawn(new Vector2(Mathf.Lerp(triggerX.x, triggerX.y, Random.value), y + 0.1f), bubbleColor, 2, 2.5f, 0.4f);
            }

            if (at.x >= triggerX.x - 1f && at.x <= triggerX.y + 1f && at.y < y - sinkDepth && LevelManager.Current != null)
                LevelManager.Current.PlayerFell();
        }
    }
}
