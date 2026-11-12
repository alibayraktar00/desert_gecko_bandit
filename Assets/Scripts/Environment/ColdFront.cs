using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Arkadan gelen kum fırtınası: oyuncu başlangıç çizgisini geçince ilerler. İlk yakalayışta canın yarısı gider,
    /// fırtına biraz geri çekilir ve kaçış sürer; ikinci yakalayış öldürür, oyuncu checkpoint'e döner. Oyuncu açılırsa hızlanır, yakınsa biraz yavaşlar: hep ensede ama adil.
    /// Bitiş çizgisinde (büyük ateş) dağılır.
    ///
    /// Görünüş: arkada koyu toz gövdesi, önünde kat kat kıvrılan bulut sıraları. Sıralar ekranın dibinden tepesine
    /// dizilir ve yerdeki öne taşar; fırtına dikey bir duvar değil, üstüne yuvarlanan bir bulut gibi görünür.
    /// Önünde sağa savrulan kum taneleri hızını gösterir.
    /// </summary>
    public class ColdFront : MonoBehaviour
    {
        [SerializeField] float speed = 5f;
        [SerializeField] float farLead = 10f;
        [SerializeField] float farFactor = 1.7f;
        [SerializeField] float closeLead = 4.5f;
        [SerializeField] float closeFactor = 0.85f;
        [Tooltip("Yeniden doğunca fırtına oyuncunun bu kadar gerisinden başlar")]
        [SerializeField] float respawnLead = 13f;
        [SerializeField] float respawnDelay = 1.2f;
        [Tooltip("Yakaladığında can yarıya iner, fırtına oyuncunun bu kadar gerisine çekilir ve kısa süre bekler")]
        [SerializeField] float catchPushback = 6f;
        [SerializeField] float catchPause = 0.8f;
        [SerializeField] float startX, triggerX, endX;
        [SerializeField] float fadeTime = 2.5f;

        [Header("Görünüş")]
        [Tooltip("Aşağıdan yukarı bulut sıraları; kıvrılmayı her birinin SpriteLoop'u oynatır")]
        [SerializeField] Transform[] rows;
        [Tooltip("Sıranın sağ kenarı ön yüzün bu kadar önünde (yerdeki en önde)")]
        [SerializeField] float[] rowLead;
        [SerializeField] float rowSpacing = 1.9f;
        [SerializeField] Transform body;

        [Header("Savrulan kum")]
        [SerializeField] int grainCount = 46;
        [SerializeField] float grainReach = 12f;
        [SerializeField] Color grainColor = new Color(0.84f, 0.72f, 0.56f, 0.85f);

        PlayerHealth player;
        SpriteRenderer[] renderers;
        Color[] baseColors;
        bool running, ended;
        float waitUntil, fade = 1f;
        Transform[] grains;
        SpriteRenderer[] grainRenderers;
        float[] grainSeed;

        public bool IsRunning => running && !ended;
        public bool HasEnded => ended;
        public float FrontX => transform.position.x;

        public void Configure(float behindX, float startLineX, float finishX, float frontSpeed)
        {
            startX = behindX;
            triggerX = startLineX;
            endX = finishX;
            speed = frontSpeed;
        }

        public void SetArt(Transform[] cloudRows, float[] leads, Transform dustBody)
        {
            rows = cloudRows;
            rowLead = leads;
            body = dustBody;
        }

        void Awake()
        {
            BuildGrains();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].color;
            Show(false);
        }

        void BuildGrains()
        {
            if (grainCount <= 0)
                return;
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            var root = new GameObject("Kum").transform;
            root.SetParent(transform, false);
            var template = rows != null && rows.Length > 0 && rows[0] != null ? rows[0].GetComponent<SpriteRenderer>() : null;
            grains = new Transform[grainCount];
            grainRenderers = new SpriteRenderer[grainCount];
            grainSeed = new float[grainCount];
            for (int i = 0; i < grainCount; i++)
            {
                var sr = new GameObject("Tane").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.sprite = pixel;
                if (template != null)
                {
                    sr.sharedMaterial = template.sharedMaterial;
                    sr.sortingOrder = template.sortingOrder + 5;
                }
                sr.transform.localScale = new Vector3(0.12f + (i % 4) * 0.1f, 0.07f, 1f);
                sr.color = grainColor;
                grains[i] = sr.transform;
                grainRenderers[i] = sr;
                grainSeed[i] = i * 0.6180339f % 1f;
            }
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
            Animate();
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

            if (lead <= 0f)
                Catch();
        }

        // Yakalanan oyuncu canının yarısını kaybeder ve kaçmaya devam eder; fırtına bir an geri çekilir.
        // Can zaten yarıdaysa bu darbe öldürür, oyuncu checkpoint'te doğar (OnPlayerReturned fırtınayı geri alır).
        void Catch()
        {
            if (player.IsInvincible)
                return;
            player.LoseHealthQuietly(Mathf.Max(1, player.MaxHealth / 2));
            if (!player.IsAlive)
                return;
            transform.position = new Vector3(player.transform.position.x - catchPushback, transform.position.y, transform.position.z);
            waitUntil = Time.time + catchPause;
        }

        // Sıralar ve gövde ekranın dibine göre dizilir: fırtına hangi yükseklikte olursa olsun ekranı kaplar.
        void Animate()
        {
            Camera cam = Camera.main;
            float bottom = cam != null ? cam.transform.position.y - cam.orthographicSize : 0f;
            float height = cam != null ? cam.orthographicSize * 2f : 12f;
            float x = transform.position.x;
            float t = Time.time;

            if (rows != null)
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] == null)
                        continue;
                    float roll = Mathf.Sin(t * (0.8f + i * 0.17f) + i * 1.7f) * 0.5f; // kabarıp geri çekilme
                    float bob = Mathf.Sin(t * (1.1f + i * 0.23f) + i) * 0.18f;
                    var sr = rows[i].GetComponent<SpriteRenderer>();
                    float halfWidth = sr != null && sr.sprite != null ? sr.sprite.bounds.size.x * rows[i].localScale.x / 2f : 4f;
                    float lead = rowLead != null && i < rowLead.Length ? rowLead[i] : 0f;
                    rows[i].position = new Vector3(x + lead - halfWidth + roll, bottom - 0.6f + i * rowSpacing + bob, 0f);
                }

            if (body != null)
                body.position = new Vector3(body.position.x, bottom + height / 2f, 0f);

            if (grains == null)
                return;
            for (int i = 0; i < grains.Length; i++)
            {
                // Her tane fırtınadan çıkıp sağa uçar, uzaklaştıkça solar; konum sadece zamandan hesaplanır.
                float g = (t * (0.6f + grainSeed[i] * 0.6f) + grainSeed[i] * 7f) % 1f;
                float gx = x - 1f + g * grainReach;
                float gy = bottom + (i * 0.7548777f % 1f) * height + Mathf.Sin(g * 6f + i) * 0.35f - g * 0.6f;
                grains[i].position = new Vector3(gx, gy, 0f);
                Color c = grainColor;
                c.a *= (1f - g) * fade;
                grainRenderers[i].color = c;
            }
        }

        void FadeOut()
        {
            fade = Mathf.Max(0f, fade - Time.deltaTime / fadeTime);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (grainRenderers != null && System.Array.IndexOf(grainRenderers, renderers[i]) >= 0)
                    continue; // tanelerin rengini Animate yönetir
                Color c = baseColors[i];
                c.a *= fade;
                renderers[i].color = c;
            }
            if (fade <= 0f)
                gameObject.SetActive(false);
        }
    }
}
