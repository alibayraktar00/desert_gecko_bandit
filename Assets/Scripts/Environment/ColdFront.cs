using DesertGecko.Levels;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Arkadan gelen ayaz fırtınası: oyuncu başlangıç çizgisini geçince duvar halinde ilerler, yakalarsa can yakar ve
    /// oyuncu checkpoint'e döner. Oyuncu açılırsa hızlanır, yakınsa yavaşlar: hep ensede ama adil.
    /// Bitiş çizgisinde (büyük ateş) dağılır.
    /// </summary>
    public class ColdFront : MonoBehaviour
    {
        [SerializeField] float speed = 3.9f;
        [SerializeField] float farLead = 15f;
        [SerializeField] float farFactor = 1.55f;
        [SerializeField] float closeLead = 5.5f;
        [SerializeField] float closeFactor = 0.72f;
        [Tooltip("Yeniden doğunca fırtına oyuncunun bu kadar gerisinden başlar")]
        [SerializeField] float respawnLead = 13f;
        [SerializeField] float respawnDelay = 1.2f;
        [SerializeField] float startX, triggerX, endX;
        [SerializeField] float fadeTime = 2.5f;
        [SerializeField] SpriteRenderer[] billows;
        [SerializeField] float billowScroll = 2.2f;

        PlayerHealth player;
        SpriteRenderer[] renderers;
        Color[] baseColors;
        bool running, ended;
        float waitUntil, fade = 1f;
        float[] billowBaseY;

        public bool IsRunning => running && !ended;
        public bool HasEnded => ended;
        public float FrontX => transform.position.x;

        public void Configure(float behindX, float startLineX, float finishX, SpriteRenderer[] scrolling, float frontSpeed)
        {
            startX = behindX;
            triggerX = startLineX;
            endX = finishX;
            billows = scrolling;
            speed = frontSpeed;
        }

        void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].color;
            billowBaseY = new float[billows != null ? billows.Length : 0];
            for (int i = 0; i < billowBaseY.Length; i++)
                billowBaseY[i] = billows[i].transform.localPosition.y;
            Show(false);
        }

        // Başlamadan önce görünmez: geride durduğu yerde yer altı bölümünü kaplamasın.
        void Show(bool visible)
        {
            foreach (SpriteRenderer sr in renderers)
                sr.enabled = visible;
        }

        void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
            if (player != null)
                player.ReturnedToCheckpoint += OnPlayerReturned;
        }

        void OnDestroy()
        {
            if (player != null)
                player.ReturnedToCheckpoint -= OnPlayerReturned;
        }

        void OnPlayerReturned()
        {
            if (ended)
                return;
            Vector2 at = player.CheckpointPosition;
            running = at.x >= triggerX;
            Show(running);
            float x = running ? at.x - respawnLead : startX;
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            waitUntil = Time.time + respawnDelay;
        }

        void Update()
        {
            ScrollBillows();
            if (player == null)
                return;
            if (ended)
            {
                FadeOut();
                return;
            }

            float px = player.transform.position.x;
            if (!running)
            {
                if (px >= triggerX)
                {
                    running = true;
                    waitUntil = Time.time;
                    Show(true);
                }
                return;
            }
            if (px >= endX)
            {
                ended = true;
                return;
            }
            if (!player.IsAlive || Time.time < waitUntil)
                return;

            float lead = px - FrontX;
            float rate = lead > farLead ? speed * farFactor : lead < closeLead ? speed * closeFactor : speed;
            transform.position += Vector3.right * (rate * Time.deltaTime);

            if (lead <= 0f && LevelManager.Current != null)
                LevelManager.Current.PlayerFell();
        }

        void ScrollBillows()
        {
            if (billows == null)
                return;
            for (int i = 0; i < billows.Length; i++)
            {
                if (billows[i] == null)
                    continue;
                float tile = billows[i].sprite.bounds.size.y;
                Vector3 p = billows[i].transform.localPosition;
                float direction = i % 2 == 0 ? 1f : -0.7f;
                p.y = billowBaseY[i] + Mathf.Repeat(Time.time * billowScroll * direction, tile);
                p.x = Mathf.Sin(Time.time * (0.9f + i * 0.3f) + i) * 0.45f + BaseX(i);
                billows[i].transform.localPosition = p;
            }
        }

        // Katmanlar duvarın ön yüzünden geriye dizilidir.
        static float BaseX(int i) => -i * 1.6f;

        void FadeOut()
        {
            fade = Mathf.Max(0f, fade - Time.deltaTime / fadeTime);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = baseColors[i];
                c.a *= fade;
                renderers[i].color = c;
            }
            if (fade <= 0f)
                gameObject.SetActive(false);
        }
    }
}
